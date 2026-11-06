// Copyright Eternal Developments, LLC. All Rights Reserved.

using System.Net;
using Eternal.ConsoleUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Text;

namespace Eternal.PackageCheck
{
	internal class LicenseInfo( string spdxCode )
	{
		public string SpdxCode = spdxCode;
		public string HtmlLicenseUri = string.Empty;
		public string LicenseVersion = string.Empty;
		public string LicenseContent = string.Empty;
		public bool IsSpdxCode = false;
		public bool IsPermissive = false;
		public bool IsPreferred = false;
	}

	[JsonConverter( typeof( StringEnumConverter ) )]
	internal enum ESeverity
	{
		Unknown = -1,
		Critical,
		High,
		Medium,
		Low
	}

	internal class GenericCve
	{
		public string CveId = string.Empty;
		public ESeverity Severity = ESeverity.Unknown;
	}

	[JsonConverter( typeof( StringEnumConverter ) )]
	public enum EDependencyType
	{
		Unknown = -1,
		Direct,
		Production,
		Indirect,
		Development,
		Build,

		/// <summary>A dependency group that names no target framework, which any application may consume.</summary>
		AnyFramework,

		/// <summary>A group targeting a platform of a .NET release, e.g. "net8.0-ios18.0", which only an application for that platform consumes.</summary>
		PlatformSpecific,

		// .NET 5 and later, published under their short monikers
		net50,
		net60,
		net70,
		net80,
		net90,
		net100,
		net110,

		// .NET Core, before the move to the short monikers above
		NETCoreApp10,
		NETCoreApp11,
		NETCoreApp20,
		NETCoreApp21,
		NETCoreApp22,
		NETCoreApp30,
		NETCoreApp31,

		// .NET Framework
		NETFramework11,
		NETFramework20,
		NETFramework30,
		NETFramework35,
		NETFramework35Client,
		NETFramework40,
		NETFramework40Client,
		NETFramework403,
		NETFramework45,
		NETFramework451,
		NETFramework452,
		NETFramework46,
		NETFramework461,
		NETFramework462,
		NETFramework47,
		NETFramework471,
		NETFramework472,
		NETFramework48,
		NETFramework481,

		// .NET Standard. 2.1 is the last, and .NET Framework can consume no higher than 2.0
		NETStandard10,
		NETStandard11,
		NETStandard12,
		NETStandard13,
		NETStandard14,
		NETStandard15,
		NETStandard16,
		NETStandard20,
		NETStandard21,

		// Retained so that data written before these buckets existed still deserializes
		NETFramework60,
		NETFramework80,
		NETFramework100,
		Windows80
	}

	internal class PackageDependency
	{
		public string Name = string.Empty;
		public string Version = string.Empty;
		public string SpdxCode = string.Empty;
		public string HtmlLicenseUri = string.Empty;
		public EDependencyType DependencyType = EDependencyType.Unknown;
	}

	internal class GenericPackage
	{
		public string Name = string.Empty;
		public string Version = string.Empty;
		public DateTime VersionCreatedAt = DateTime.MinValue;
		public string Authors = string.Empty;
		public string Copyright = string.Empty;
		public string HomepageUri = string.Empty;
		public string SourceCodeUri = string.Empty;
		public string DownloadUri = string.Empty;
		public string Description = string.Empty;
		public string DependencyUri = string.Empty;
		public string VulnerabilityUri = string.Empty;
		public readonly List<LicenseInfo> Licenses = new List<LicenseInfo>();
		public List<GenericCve> CVEs = new List<GenericCve>();
		public Dictionary<EDependencyType, List<PackageDependency>> Dependencies = new Dictionary<EDependencyType, List<PackageDependency>>();

		internal static string GetTpsName( string ecosystem, string name, string version )
		{
			string tps_name = name + "." + version;
			tps_name = tps_name.Replace( '\\', '-' );
			tps_name = tps_name.Replace( '/', '-' );
			// A Debian version may carry an epoch, e.g. "1:2.3-4", and a colon is not valid in a Windows file name
			tps_name = tps_name.Replace( ':', '_' );
			tps_name = tps_name + "." + ecosystem + ".tps";

			return tps_name;
		}

		/// <summary>
		/// Write out this package as a json file
		/// </summary>
		/// <param name="ecosystem">The ecosystem name, which becomes part of the file name, e.g. "NuGet" in "Newtonsoft.Json.13.0.4.NuGet.tps".</param>
		internal void WriteTpsFile( string ecosystem )
		{
			string tps_name = GetTpsName( ecosystem, Name, Version );
			JsonHelper.WriteJsonFile( tps_name, this );

			ConsoleLogger.Log( $"Wrote {tps_name}" );
		}

		/// <summary>
		/// If there is no copyright set, but the authors are, set the copyright to 'Copyright Authors'
		/// </summary>
		private void FixupCopyright()
		{
			if( string.IsNullOrEmpty( Copyright ) )
			{
				if( string.IsNullOrEmpty( Authors ) )
				{
					ConsoleLogger.Warning( "No copyright or authors set." );
					return;
				}

				ConsoleLogger.Warning( $" -> no copyright message, but 'authors' is set. Using 'Copyright authors'" );
				Copyright = $"Copyright {Authors}";
			}
		}

		/// <summary>
		/// If the source code uri is null, but the homepage is a hosting site (such as GitHub), set the source code uri to the home page uri.
		/// </summary>
		private void FixupSourceCodeUri()
		{
			if( string.IsNullOrEmpty( SourceCodeUri ) )
			{
				ConsoleLogger.Warning( "'source_code_uri' is null" );
				if( HomepageUri!.Contains( "github.com", StringComparison.OrdinalIgnoreCase )
				    || HomepageUri!.Contains( "gitlab.com", StringComparison.OrdinalIgnoreCase ) )
				{
					ConsoleLogger.Log( $" -> 'homepage_uri' is a hosting site; using '{HomepageUri}'" );
					SourceCodeUri = HomepageUri;
				}
			}
		}

		/// <summary>
		/// Derive metadata related to the license for a dependent package.
		/// </summary>
		protected void CleanupDependencyData()
		{
			FixupSourceCodeUri();

			LicenseChecker.FixSpdxCodes( Licenses );
			LicenseChecker.ValidateLicenses( Licenses );
			LicenseChecker.ScrapeRepoForLicenses( SourceCodeUri, Licenses, Name, Version );
			LicenseIdentifier.SetLicenseUrlsToDefault( Licenses );
		}

		/// <summary>
		/// Records where a reviewer can see this package's own dependencies, which deps.dev lists for every supported ecosystem.
		/// </summary>
		/// <param name="packageType">The package type's canonical name, lower cased to form the deps.dev URL.</param>
		protected virtual void SetDependencyUri( string packageType )
		{
			// e.g. https://deps.dev/nuget/newtonsoft.json/13.0.4/dependencies
			DependencyUri = $"https://deps.dev/{packageType.ToLowerInvariant()}/{Uri.EscapeDataString( Name )}/{Uri.EscapeDataString( Version )}/dependencies";
		}

		/// <summary>
		/// Records where a reviewer can look this package up for known vulnerabilities.
		/// </summary>
		/// <param name="packageType">The package type's canonical name, lower cased to form the deps.dev URL when Snyk is not used.</param>
		/// <param name="snykEcosystem">The package type's Snyk path segment, or "" to use deps.dev instead.</param>
		protected virtual void SetVulnerabilityUri( string packageType, string snykEcosystem )
		{
			// Snyk does not have a usable catalog for every package type. Its crates.io coverage only contains crates that already have an advisory, so a Snyk link
			// 404s for the majority of crates, which are clean - and it misses some that are not, such as anyhow, whose only advisory is RUSTSEC-2026-0190 with no
			// CVE alias. An empty Snyk name opts out, and deps.dev, which carries every published package, is used in its place.
			if( snykEcosystem.Length == 0 )
			{
				VulnerabilityUri = $"https://deps.dev/{packageType.ToLowerInvariant()}/{Uri.EscapeDataString( Name )}/{Uri.EscapeDataString( Version )}";
				return;
			}

			VulnerabilityUri = $"https://security.snyk.io/package/{snykEcosystem}/{Uri.EscapeDataString( Name )}/{Version}";
		}

		/// <summary>
		/// Looks this package up in OSV for known vulnerabilities.
		/// </summary>
		/// <param name="osvEcosystem">The package type's OSV ecosystem name, e.g. "crates.io".</param>
		protected virtual void CheckCves( string osvEcosystem )
		{
			CveChecker.CheckCves( this, osvEcosystem );
		}

		/// <summary>
		/// Derive all data for a root package
		/// </summary>
		/// <param name="packageType">The package type's canonical name, e.g. "Cargo".</param>
		/// <param name="osvEcosystem">The package type's OSV ecosystem name, e.g. "crates.io".</param>
		/// <param name="snykEcosystem">The package type's Snyk path segment, e.g. "pip", or "" to fall back to deps.dev.</param>
		protected void CleanupData( string packageType, string osvEcosystem, string snykEcosystem )
		{
			CleanupDependencyData();

			LicenseIdentifier.ScrapeCopyrightFromLicenses( Licenses, ref Copyright );
			SetDependencyUri( packageType );
			SetVulnerabilityUri( packageType, snykEcosystem );
			CheckCves( osvEcosystem );
			FixupCopyright();

			UpdateDependencies();
		}

		/// <summary>
		/// Extracts the preferred license from the package and puts it in the passed-in dependency of the root package.
		/// </summary>
		/// <param name="dependency">Dependency of another package.</param>
		protected void SetDependencyLicense( PackageDependency dependency )
		{
			dependency.Version = Version;

			LicenseInfo? preferred_license = Licenses.FirstOrDefault( x => x.IsPreferred );
			if( preferred_license != null )
			{
				dependency.SpdxCode = preferred_license.SpdxCode;
				dependency.HtmlLicenseUri = preferred_license.HtmlLicenseUri;
			}
		}

		protected virtual void UpdateDependency( PackageDependency dependency )
		{
		}

		/// <summary>
		/// Interrogate the metadata for the dependent packages to get the license and license URL
		/// </summary>
		private void UpdateDependencies()
		{
			foreach( EDependencyType dependency_type in Dependencies.Keys )
			{
				foreach( PackageDependency dependency in Dependencies[dependency_type] )
				{
					UpdateDependency( dependency );
				}
			}
		}

		/// <summary>
		/// Add a single dependency to the list of dependencies
		/// </summary>
		/// <param name="packageDependency">Dependency to add</param>
		protected void AddSingleDependency( PackageDependency packageDependency )
		{
			if( Dependencies.ContainsKey( packageDependency.DependencyType ) )
			{
				Dependencies[packageDependency.DependencyType].Add( packageDependency );
			}
			else
			{
				Dependencies.Add( packageDependency.DependencyType, [packageDependency] );
			}
		}

		/// <summary>
		/// Prints all the details about the package
		/// </summary>
		/// <param name="ecosystem">The case-sensitive ecosystem from https://ossf.github.io/osv-schema/#defined-ecosystems</param>
		protected void PrintDetails( string ecosystem )
		{
			ConsoleLogger.Log( $"{ecosystem} package: '{Name} - {Version}' ({ConsoleLogger.TimeString( DateTime.UtcNow - VersionCreatedAt )} old)" );
			ConsoleLogger.Log( $"Copyright - '{Copyright}'" );

			ConsoleLogger.Log( $"License(s): " );
			foreach( LicenseInfo license in Licenses )
			{
				string preferred = license.IsPreferred ? "(Preferred)" : string.Empty;
				ConsoleLogger.Log( $" - '{license.SpdxCode} - {license.HtmlLicenseUri}' {preferred}" );
			}

			ConsoleLogger.Log( $"Download Uri - '{DownloadUri}'" );
			ConsoleLogger.Log( $"Project Uri - '{HomepageUri}'" );
			ConsoleLogger.Log( $"Source code - '{SourceCodeUri}'" );

			ConsoleLogger.Log( $"Description - '{Description}'" );
			ConsoleLogger.Log( $"Dependency uri - '{DependencyUri}'" );
			// Not always Snyk - Cargo packages point at deps.dev and Debian packages at the Debian security tracker instead - so both the field and the label stay neutral.
			ConsoleLogger.Log( $"Vulnerability uri - '{VulnerabilityUri}'" );

			if( Dependencies.Count == 0 )
			{
				ConsoleLogger.Log( "Package has no dependencies" );
			}
			else
			{
				foreach( EDependencyType dependency_type in Dependencies.Keys )
				{
					ConsoleLogger.Log( $"{dependency_type} dependencies ({Dependencies[dependency_type].Count}):" );
					foreach( PackageDependency dependency in Dependencies[dependency_type] )
					{
						ConsoleLogger.Log( $" .. {dependency.Name} {dependency.Version} - {dependency.SpdxCode} - {dependency.HtmlLicenseUri}" );
					}
				}
			}

			if( CVEs.Count == 0 )
			{
				ConsoleLogger.Log( "Package has no CVEs" );
			}
			else
			{
				ConsoleLogger.Log( $"Vulnerabilities ({CVEs.Count}):" );
				foreach( GenericCve cve in CVEs )
				{
					ConsoleLogger.Log( $" .. {cve.CveId} - {cve.Severity} - https://nvd.nist.gov/vuln/detail/{cve.CveId}" );
				}
			}

			if( !PackageCheck.RateLimitExceeded )
			{
				WriteTpsFile( ecosystem );
			}
		}
	}

	internal static class PackageCheck
	{
		/// <summary>
		/// Set once a registry or repository host has started throttling, which stops any further .tps files being written so that a run cut short by rate
		/// limiting cannot leave behind a partial, misleading set of approval data.
		/// </summary>
		public static bool RateLimitExceeded = false;
		private static readonly HttpClient HttpClientInstance = CreateHttpClient();

		/// <summary>
		/// Builds the shared HttpClient. A User-Agent identifying this tool is required rather than merely polite: crates.io rejects any request without one with a
		/// 403, and the GitHub and GitLab APIs ask for one too.
		/// </summary>
		private static HttpClient CreateHttpClient()
		{
			HttpClient http_client = new HttpClient();
			http_client.DefaultRequestHeaders.UserAgent.ParseAdd( "Eternal.PackageCheck (https://eternaldevelopments.com/packagedocs/)" );
			return http_client;
		}

		private static void CheckRateLimit( HttpResponseMessage response )
		{
			if( response.StatusCode == HttpStatusCode.Forbidden )
			{
				RateLimitExceeded = true;
				ConsoleLogger.Error( "Rate limit exceeded; aborting to avoid writing partial TPS files" );
			}
		}

		/// <summary>
		/// Sends a request and hands back the successful response for the caller to read. A non-success status is not an exception: the body is logged, because it
		/// carries the useful part of an API error such as GitHub's "API rate limit exceeded for ...", the rate limit is checked, and null is returned.
		/// </summary>
		/// <param name="httpClient">The client to send with, so that callers needing their own headers - a GitHub PAT, say - can supply their own.</param>
		/// <param name="request">The request to send. It is disposed before this returns.</param>
		/// <param name="failureContext">A line describing the request, logged if it throws.</param>
		/// <returns>The successful response, which the caller owns and must dispose, or null if the request failed.</returns>
		private static async Task<HttpResponseMessage?> SendRequest( HttpClient httpClient, HttpRequestMessage request, string failureContext )
		{
			HttpMethod method = request.Method;
			try
			{
				using( request )
				{
					HttpResponseMessage response = await httpClient.SendAsync( request );

					if( response.IsSuccessStatusCode )
					{
						return response;
					}

					string response_body = await response.Content.ReadAsStringAsync();
					ConsoleLogger.Error( $"Query returned '{response_body}'" );
					CheckRateLimit( response );
					response.Dispose();
				}
			}
			catch( Exception ex )
			{
				ConsoleLogger.Error( $"Exception during {method} request - {ex.Message}" );
				ConsoleLogger.Error( failureContext );
			}

			return null;
		}

		/// <summary>
		/// Issues an HTTP GET with the given client and hands back the successful response for the caller to read. Used by the callers that need more than the
		/// body: GitLab's paginated tree API reads the "X-Next-Page" response header, and the Go module proxy reads a zip archive as a stream.
		/// </summary>
		/// <param name="httpClient">The client to send with, so that callers needing their own headers - a GitHub PAT, say - can supply their own.</param>
		/// <param name="getRequest">The URL to request.</param>
		/// <returns>The successful response, which the caller owns and must dispose, or null if the request failed.</returns>
		internal static async Task<HttpResponseMessage?> RequestResponse( HttpClient httpClient, string getRequest )
		{
			HttpRequestMessage request = new HttpRequestMessage( HttpMethod.Get, getRequest );
			return await SendRequest( httpClient, request, $"Request: '{getRequest}'" );
		}

		/// <summary>
		/// Issues an HTTP POST with a JSON payload and hands back the successful response for the caller to read.
		/// </summary>
		/// <param name="httpClient">The client to send with.</param>
		/// <param name="url">The URL to post to.</param>
		/// <param name="payload">The JSON request body.</param>
		/// <returns>The successful response, which the caller owns and must dispose, or null if the request failed.</returns>
		private static async Task<HttpResponseMessage?> PostResponse( HttpClient httpClient, string url, string payload )
		{
			HttpRequestMessage request = new HttpRequestMessage( HttpMethod.Post, url )
			{
				// The content is owned by the request, so disposing the request disposes it too
				Content = new StringContent( payload, Encoding.UTF8, "application/json" )
			};

			return await SendRequest( httpClient, request, $"Payload: '{payload}'" );
		}

		/// <summary>
		/// Issues an HTTP GET with the given client and returns the response body, logging and returning an empty string on a non-success status code or any
		/// exception.
		/// </summary>
		/// <param name="httpClient">The client to send with, so that callers needing their own headers - a GitHub PAT, say - can supply their own.</param>
		/// <param name="getRequest">The URL to request.</param>
		/// <returns>The response body, or "" if the request failed.</returns>
		internal static async Task<string> Request( HttpClient httpClient, string getRequest )
		{
			using HttpResponseMessage? response = await RequestResponse( httpClient, getRequest );
			if( response == null )
			{
				return string.Empty;
			}

			return await response.Content.ReadAsStringAsync();
		}

		/// <summary>
		/// Issues an HTTP GET on the shared client and returns the response body, logging and returning an empty string on a non-success status code or any
		/// exception.
		/// </summary>
		/// <param name="getRequest">The URL to request.</param>
		/// <returns>The response body, or "" if the request failed.</returns>
		internal static async Task<string> Request( string getRequest )
		{
			return await Request( HttpClientInstance, getRequest );
		}

		/// <summary>
		/// Issues an HTTP POST with a JSON payload on the given client and returns the response body, logging and returning an empty string on a non-success
		/// status code or any exception.
		/// </summary>
		/// <param name="httpClient">The client to send with.</param>
		/// <param name="url">The URL to post to.</param>
		/// <param name="payload">The JSON request body.</param>
		/// <returns>The response body, or "" if the request failed.</returns>
		private static async Task<string> PostRequest( HttpClient httpClient, string url, string payload )
		{
			using HttpResponseMessage? response = await PostResponse( httpClient, url, payload );
			if( response == null )
			{
				return string.Empty;
			}

			return await response.Content.ReadAsStringAsync();
		}

		/// <summary>
		/// Issues an HTTP POST with a JSON payload on the shared client and returns the response body, logging and returning an empty string on a non-success
		/// status code or any exception.
		/// </summary>
		/// <param name="url">The URL to post to.</param>
		/// <param name="payload">The JSON request body.</param>
		/// <returns>The response body, or "" if the request failed.</returns>
		internal static async Task<string> PostRequest( string url, string payload )
		{
			return await PostRequest( HttpClientInstance, url, payload );
		}

		/// <summary>
		/// Prints the command line usage and a description of the output this tool produces.
		/// </summary>
		private static void PrintHelp()
		{
			ConsoleLogger.Log( "Usage:" );
			ConsoleLogger.Log( "PackageCheck <nuget|npm|rubygems|go|pypi|cargo|deb>) <packagename> [version]" );
			ConsoleLogger.Log( "PackageCheck <cyclonedx|spdx> <SBOM name>" );

			ConsoleLogger.Log( "Expected output:" );
			ConsoleLogger.Log( "<Ecosystem> Package: 'PackageName - Version' (package age)" );
			ConsoleLogger.Log( "Copyright - 'Copyright message'" );

			ConsoleLogger.Log( "License(s) - List of 'SPDX Code(s) or Custom - License URL'" );

			ConsoleLogger.Log( "Download Uri - 'Download URL'" );
			ConsoleLogger.Log( "Project Uri - 'Project Homepage'" );
			ConsoleLogger.Log( "Source Code Uri - 'Source code URL'" );

			ConsoleLogger.Log( "Description - 'Description of the package'" );
			ConsoleLogger.Log( "Dependency uri - 'Link to the dependencies of the package'" );
			ConsoleLogger.Log( "Vulnerability uri - 'Link to list of CVEs for this package'" );

			ConsoleLogger.Log( "Dependencies - sorted by type" );
			ConsoleLogger.Log( "CVEs - 'List of CVE ids, severity, and NVD links'" );
		}

		/// <summary>
		/// Normalizes free-text prose for display: converts smart quotes to plain ASCII quotes, collapses carriage returns/newlines/tabs to spaces, squashes repeated
		/// spaces down to one, and trims the result.
		/// </summary>
		/// <param name="prose">The raw text to clean up.</param>
		/// <returns>The cleaned-up text.</returns>
		internal static string SanitizeString( string prose )
		{
			// Replace smart quotes
			prose = prose.Replace( '\u201C', '\"' );
			prose = prose.Replace( '\u201D', '\"' );
			prose = prose.Replace( '\u2018', '\'' );
			prose = prose.Replace( '\u2019', '\'' );

			prose = prose.Replace( '\r', ' ' );
			prose = prose.Replace( '\n', ' ' );
			prose = prose.Replace( '\t', ' ' );
			while( prose.Contains( "  " ) )
			{
				prose = prose.Replace( "  ", " " );
			}

			return prose.Trim();
		}

		internal static bool ProcessPackage( string ecosystem, string packageName, string version )
		{
			bool processed = true;
			switch( ecosystem )
			{
				case "nuget":
					NuGetPackage.CheckNuGetPackage( packageName, version );
					break;

				case "go":
					GoPackage.CheckGoPackage( packageName, version );
					break;

				case "npm":
					NpmPackage.CheckNpmPackage( packageName, version );
					break;

				case "rubygems":
					RubyGem.CheckRubyGem( packageName, version );
					break;

				case "pypi":
					PythonPackage.CheckPythonPackage( packageName, version );
					break;

				case "cargo":
					CargoPackage.CheckCargoPackage( packageName, version );
					break;

				case "deb":
					DebPackage.CheckDebPackage( packageName, version );
					break;

				default:
					processed = false;
					break;
			}

			return processed;
		}

		private static bool ParseArguments( string[] args, out string ecosystem, out string packageName, out string version )
		{
			ecosystem = string.Empty;
			packageName = string.Empty;
			version = string.Empty;

			if( args.Length >= 2 )
			{
				ecosystem = args[0].ToLower();
				packageName = args[1];
				if( args.Length > 2 )
				{
					version = args[2];
				}

				return true;
			}

			ConsoleLogger.Error( "Too few arguments" );
			PrintHelp();
			return false;
		}

		private static int Main( string[] args )
		{
			ConsoleLogger.Title( "PackageCheck - Copyright Eternal Developments, LLC. All Rights Reserved." );
			DateTime start_time = DateTime.UtcNow;

			if( !ParseArguments( args, out string ecosystem, out string package_name, out string version ) )
			{
				return -1;
			}

			if( !ProcessPackage( ecosystem, package_name, version ) )
			{
				List<SbomPackage> packages = new List<SbomPackage>();
				if( ecosystem == "cyclonedx" )
				{
					packages = CycloneDX.ReadBomFile( package_name );
				}
				else if( ecosystem == "spdx" )
				{
					packages = SPDX.ReadBomFile( package_name );
				}
				else
				{
					ConsoleLogger.Warning( $"Invalid ecosystem - '{ecosystem}'" );
				}

				PackageUrl.GenerateTpsFiles( packages );
			}

			ConsoleLogger.Success( $"Completed in {ConsoleLogger.TimeString( DateTime.UtcNow - start_time )}" );
			return 0;
		}
	}
}
