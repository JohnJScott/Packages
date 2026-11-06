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

		/// <summary>
		/// The vulnerabilities this one was derived from. A Debian advisory (a DSA or DLA) covers several CVEs at once and lists them here rather than as aliases,
		/// as it is a distinct advisory rather than another name for any one of them.
		/// </summary>
		public List<string>? upstream { get; set; }

		public List<OsvReference>? references { get; set; }

		/// <summary>
		/// Finds this vulnerability's CVE identifiers, first checking <see cref="aliases"/> and <see cref="upstream"/> for entries starting with "CVE-", then
		/// falling back to scanning <see cref="references"/> for a URL containing one.
		/// </summary>
		/// <returns>The distinct CVE ids (e.g. "CVE-2021-12345"), or an empty list if none could be found.</returns>
		public List<string> FindCveIds()
		{
			List<string> cve_ids = new List<string>();
			foreach( List<string>? identifiers in new List<string>?[] { aliases, upstream } )
			{
				if( identifiers == null )
				{
					continue;
				}

				foreach( string identifier in identifiers )
				{
					if( identifier.StartsWith( "CVE-" ) && !cve_ids.Contains( identifier ) )
					{
						cve_ids.Add( identifier );
					}
				}
			}

			if( cve_ids.Count > 0 )
			{
				return cve_ids;
			}

			if( references != null )
			{
				foreach( OsvReference reference in references )
				{
					int offset = reference.url.IndexOf( "CVE-", StringComparison.OrdinalIgnoreCase );
					if( offset > -1 )
					{
						cve_ids.Add( reference.url.Substring( offset ) );
						break;
					}
				}
			}

			return cve_ids;
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
			CheckCves( package, ecosystem, package.Name, package.Version );
		}

		/// <summary>
		/// Queries OSV.dev for known vulnerabilities affecting a package under a different name and version than it is published as - a Debian binary package,
		/// whose advisories are recorded against the source package that built it - and records them against the given package.
		/// </summary>
		/// <param name="package">The package to record the vulnerabilities against.</param>
		/// <param name="ecosystem">The case-sensitive ecosystem from https://ossf.github.io/osv-schema/#defined-ecosystems</param>
		/// <param name="packageName">The name OSV knows the package by.</param>
		/// <param name="version">The version OSV knows the package by.</param>
		internal static void CheckCves( GenericPackage package, string ecosystem, string packageName, string version )
		{
			OsvQueryRequest request = new OsvQueryRequest( version, packageName, ecosystem );
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

			// The same CVE can be reached through several advisories - Debian publishes both a DSA covering a batch of CVEs and a DEBIAN-CVE record for each one -
			// so each is only looked up and recorded once
			List<string> cve_ids = new List<string>();
			foreach( OsvVulnerability vulnerability in osv_response.vulns )
			{
				List<string> vulnerability_cve_ids = vulnerability.FindCveIds();
				if( vulnerability_cve_ids.Count == 0 )
				{
					ConsoleLogger.Warning( $"Failed to find CVE id for vulnerability '{vulnerability.id}'" );
					continue;
				}

				foreach( string cve_id in vulnerability_cve_ids )
				{
					if( !cve_ids.Contains( cve_id ) )
					{
						cve_ids.Add( cve_id );
					}
				}
			}

			foreach( string cve_id in cve_ids )
			{
				AddCve( package, cve_id );
			}
		}

		/// <summary>
		/// Fetches the severity of a CVE from the NVD and records it against the package. Nothing is recorded if the NVD could not be reached or does not know
		/// the CVE.
		/// </summary>
		/// <param name="package">The package to record the CVE against.</param>
		/// <param name="cveId">The CVE id, e.g. "CVE-2021-12345".</param>
		private static void AddCve( GenericPackage package, string cveId )
		{
			string nvd_url = $"https://services.nvd.nist.gov/rest/json/cves/2.0?cveId={Uri.EscapeDataString( cveId )}";
			string nvd_content = PackageCheck.Request( nvd_url ).Result;
			if( string.IsNullOrEmpty( nvd_content ) )
			{
				ConsoleLogger.Warning( $"Failed to get CVE details from NVD for '{cveId}'" );
				return;
			}

			NvdCveResponse? nvd_response = JsonHelper.ReadJson<NvdCveResponse>( nvd_content );
			if( nvd_response == null || nvd_response.vulnerabilities == null )
			{
				ConsoleLogger.Error( $"Failed to deserialize response to '{nvd_url}'" );
				return;
			}

			if( nvd_response.vulnerabilities.Count > 0 )
			{
				NvdCve? cve = nvd_response.vulnerabilities[0].cve;
				if( cve != null )
				{
					GenericCve generic_cve = new GenericCve()
					{
						CveId = cveId,
						Severity = cve.GetSeverity()
					};

					package.CVEs.Add( generic_cve );
				}
			}
		}
	}
}
