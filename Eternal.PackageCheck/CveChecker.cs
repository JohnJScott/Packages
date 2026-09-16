// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;

namespace Eternal.PackageCheck
{
	internal class OsvPackage( string packageName, string usedEcosystem )
	{
		public string name { get; set; } = packageName;

		public string ecosystem { get; set; } = usedEcosystem;
	}

	internal class OsvQueryRequest( string usedVersion, string packageName, string usedEcosystem )
	{
		public string version { get; set; } = usedVersion;

		public OsvPackage package { get; set; } = new OsvPackage( packageName, usedEcosystem );

		/// <summary>
		/// Formats this request as "{packageName}@{version}", used for logging.
		/// </summary>
		/// <returns>The "{packageName}@{version}" string.</returns>
		public override string ToString()
		{
			return packageName + "@" + version;
		}
	}

	internal class OsvReference
	{
		
		public string url { get; set; } = string.Empty;
	}

	internal class OsvVulnerability
	{
		public string id { get; set; } = string.Empty;

		public List<string>? aliases { get; set; }
		
		public List<OsvReference>? references { get; set; }

		/// <summary>
		/// Finds this vulnerability's CVE identifier, first checking <see cref="aliases"/> for an entry starting with "CVE-", then falling back to scanning
		/// <see cref="references"/> for a URL containing one.
		/// </summary>
		/// <returns>The CVE id (e.g. "CVE-2021-12345"), or null if none could be found.</returns>
		public string? FindCveId()
		{
			if( aliases != null )
			{
				foreach( string alias in aliases )
				{
					if( alias.StartsWith( "CVE-" ) )
					{
						return alias;
					}
				}
			}

			if( references != null )
			{
				foreach( OsvReference reference in references )
				{
					int offset = reference.url.IndexOf( "CVE-", StringComparison.OrdinalIgnoreCase );
					if( offset > -1 )
					{
						return reference.url.Substring( offset );
					}
				}
			}

			return null;
		}
	}

	internal class OsvQueryResponse
	{
		public List<OsvVulnerability>? vulns { get; set; }
	}

	internal class NvdCvssData
	{
		public string? baseSeverity { get; set; }
	}

	internal class NvdCvssMetric
	{
		public string type { get; set; } = string.Empty;

		public NvdCvssData? cvssData { get; set; }

		public string? baseSeverity { get; set; }
	}

	internal class NvdMetrics
	{
		public List<NvdCvssMetric>? cvssMetricV31 { get; set; }

		public List<NvdCvssMetric>? cvssMetricV30 { get; set; }

		public List<NvdCvssMetric>? cvssMetricV2 { get; set; }
	}

	internal class NvdCve
	{
		public NvdMetrics? metrics { get; set; }

		private static NvdCvssMetric? PickPrimary( List<NvdCvssMetric>? candidates )
		{
			if( candidates == null )
			{
				return null;
			}

			return candidates.FirstOrDefault( m => m.type == "Primary" ) ?? candidates.FirstOrDefault();
		}

		/// <summary>
		/// Picks the "Primary" CVSS metric (preferring v3.1, then v3.0, then v2, and falling back to the first entry in each list if none is marked "Primary") and
		/// maps its base severity string to an <see cref="ESeverity"/> value.
		/// </summary>
		/// <returns>The parsed severity, or <see cref="ESeverity.Unknown"/> if no metric or severity string could be found.</returns>
		public ESeverity GetSeverity()
		{
			if( metrics == null )
			{
				return ESeverity.Unknown;
			}

			NvdCvssMetric? metric = PickPrimary( metrics.cvssMetricV31 )
			                        ?? PickPrimary( metrics.cvssMetricV30 )
			                        ?? PickPrimary( metrics.cvssMetricV2 );

			if( metric == null )
			{
				return ESeverity.Unknown;
			}

			string severity = metric.cvssData?.baseSeverity ?? metric.baseSeverity ?? string.Empty;
			switch( severity.ToLower() )
			{
				case "low":
					return ESeverity.Low;

				case "medium":
					return ESeverity.Medium;

				case "high":
					return ESeverity.High;

				case "critical":
					return ESeverity.Critical;

				default:
					break;
			}

			return ESeverity.Unknown;
		}
	}

	internal class NvdVulnerability
	{
		public NvdCve? cve { get; set; }
	}

	internal class NvdCveResponse
	{
		public List<NvdVulnerability>? vulnerabilities { get; set; }
	}

	internal static class CveChecker
	{
		/// <summary>
		/// Queries OSV.dev for known vulnerabilities affecting the given package/version, then for each one found, looks up its CVE id and fetches the corresponding
		/// severity from the NVD, printing a summary line to the console for each.
		/// </summary>
		/// <param name="ecosystem">The case-sensitive ecosystem from https://ossf.github.io/osv-schema/#defined-ecosystems</param>
		/// <param name="package">The package data ecosystem.</param>
		internal static void CheckCves( GenericPackage package, string ecosystem )
		{
			OsvQueryRequest request = new OsvQueryRequest( package.Version, package.Name, ecosystem );
			string json_payload = JsonHelper.WriteJson( request );
			string osv_content = PackageCheck.PostRequest( "https://api.osv.dev/v1/query", json_payload ).Result;
			if( string.IsNullOrEmpty( osv_content ) )
			{
				ConsoleLogger.Error( $"Failed to retrieve CVEs for '{request}'" );
				return;
			}

			OsvQueryResponse? osv_response = JsonHelper.ReadJson<OsvQueryResponse>( osv_content );
			if( osv_response == null )
			{
				ConsoleLogger.Error( $"Failed to deserialize response to '{request}'" );
				return;
			}

			if( osv_response.vulns == null )
			{
				return;
			}

			ConsoleLogger.Log( $" .. found {osv_response.vulns.Count} vulnerabilitie(s)" );
			foreach( OsvVulnerability vulnerability in osv_response.vulns )
			{
				string? cve_id = vulnerability.FindCveId();
				if( cve_id == null )
				{
					ConsoleLogger.Warning( $"Failed to find CVE id for vulnerability '{vulnerability.id}'" );
					continue;
				}

				string nvd_url = $"https://services.nvd.nist.gov/rest/json/cves/2.0?cveId={Uri.EscapeDataString( cve_id )}";
				string nvd_content = PackageCheck.Request( nvd_url ).Result;
				if( string.IsNullOrEmpty( nvd_content ) )
				{
					ConsoleLogger.Warning( $"Failed to get CVE details from NVD for '{cve_id}'" );
					continue;
				}

				NvdCveResponse? nvd_response = JsonHelper.ReadJson<NvdCveResponse>( nvd_content );
				if( nvd_response == null || nvd_response.vulnerabilities == null )
				{
					ConsoleLogger.Error( $"Failed to deserialize response to '{nvd_url}'" );
					continue;
				}

				if( nvd_response.vulnerabilities.Count > 0 )
				{
					NvdCve? cve = nvd_response.vulnerabilities[0].cve;
					if( cve != null )
					{
						GenericCve generic_cve = new GenericCve()
						{
							CveId = cve_id,
							Severity = cve.GetSeverity()
						};

						package.CVEs.Add( generic_cve );
					}
				}
			}
		}
	}
}
