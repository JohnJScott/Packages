// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Text;

namespace Eternal.PackageCheck
{
	internal class LicenseInfo( string spdxCode )
	{
		public readonly string SpdxCode = spdxCode;
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
		net80,
		net100,
		NETFramework461,
		NETFramework472,
		NETStandard20
	}

	internal class PackageDependency
	{
		public string Name = string.Empty;
		public string Version = string.Empty;
		public string License = string.Empty;
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
		public string SnykUri = string.Empty;
		public readonly List<LicenseInfo> Licenses = new List<LicenseInfo>();
		public List<GenericCve> CVEs = new List<GenericCve>();
		public Dictionary<EDependencyType, List<PackageDependency>> Dependencies = new Dictionary<EDependencyType, List<PackageDependency>>();

		private string GetCleanName()
		{
			string tps_name = Name + "." + Version;
			tps_name = tps_name.Replace( '\\', '-' );
			tps_name = tps_name.Replace( '/', '-' );
			return tps_name;
		}

		/// <summary>
		/// Write out this package as a json file
		/// </summary>
		/// <param name="ecosystem"></param>
		internal void WriteTpsFile( string ecosystem )
		{
			string tps_name = GetCleanName() + "." + ecosystem + ".tps";
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
		/// <param name="ecosystem"></param>
		protected void CleanupDependencyData( string ecosystem )
		{
			FixupSourceCodeUri();

			LicenseChecker.ValidateLicenses( Licenses );
			LicenseChecker.ScrapeRepoForLicenses( SourceCodeUri, Licenses, Name, Version );
			LicenseIdentifier.SetLicenseUrlsToDefault( Licenses );
		}

		private void SetSnykUri( string ecosystem )
		{
			if( ecosystem.ToLowerInvariant() == "go" )
			{
				ecosystem = "golang";
			}

			SnykUri = $"https://security.snyk.io/package/{ecosystem.ToLowerInvariant()}/{Uri.EscapeDataString( Name )}/{Version}";
		}

		/// <summary>
		/// Derive all data for a root package
		/// </summary>
		/// <param name="ecosystem"></param>
		protected void CleanupData( string ecosystem )
		{
			CleanupDependencyData( ecosystem );

			LicenseIdentifier.ScrapeCopyrightFromLicenses( Licenses, ref Copyright );
			SetSnykUri( ecosystem );
			CveChecker.CheckCves( this, ecosystem );
			FixupCopyright();

			UpdateDependencies();
		}

		/// <summary>
		/// Extracts the preferred license from the package and puts it in the passed-in dependency of the root package.
		/// </summary>
		/// <param name="dependency">Dependency of another package.</param>
		protected virtual void UpdateDependency( PackageDependency dependency )
		{
			LicenseInfo? preferred_license = Licenses.FirstOrDefault( x => x.IsPreferred );
			if( preferred_license != null )
			{
				dependency.License = preferred_license.SpdxCode;
				dependency.HtmlLicenseUri = preferred_license.HtmlLicenseUri;
			}
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
			ConsoleLogger.Log( $"Snyk uri - '{SnykUri}'" );

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
						ConsoleLogger.Log( $" .. {dependency.Name} {dependency.Version} - {dependency.License} - {dependency.HtmlLicenseUri}" );
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

			WriteTpsFile( ecosystem );
		}
	}

	internal static class PackageCheck
	{
		private static string Ecosystem = string.Empty;
		private static string PackageName = string.Empty;
		private static string Version = string.Empty;

		private static readonly HttpClient HttpClientInstance = new HttpClient();

		/// <summary>
		/// Issues an HTTP GET and returns the response body, logging and returning an empty string on a non-success status code or any exception.
		/// </summary>
		/// <param name="getRequest">The URL to request.</param>
		/// <returns>The response body, or "" if the request failed.</returns>
		internal static async Task<string> Request( string getRequest )
		{
			string response_body;
			try
			{
				using HttpRequestMessage request = new HttpRequestMessage( HttpMethod.Get, getRequest );
				using HttpResponseMessage response = await HttpClientInstance.SendAsync( request );
				response_body = await response.Content.ReadAsStringAsync();

				if( !response.IsSuccessStatusCode )
				{
					ConsoleLogger.Error( $"Query returned '{response_body}'" );
					response_body = string.Empty;
				}
			}
			catch( Exception ex )
			{
				ConsoleLogger.Error( $"Exception during Get request - {ex.Message}" );
				ConsoleLogger.Error( $"Request: '{getRequest}'" );
				response_body = string.Empty;
			}

			return response_body;
		}

		/// <summary>
		/// Issues an HTTP POST with a JSON payload and returns the response body, logging and returning an empty string on a non-success status code or any exception.
		/// </summary>
		/// <param name="url">The URL to post to.</param>
		/// <param name="payload">The JSON request body.</param>
		/// <returns>The response body, or "" if the request failed.</returns>
		internal static async Task<string> PostRequest( string url, string payload )
		{
			string response_body;
			try
			{
				using StringContent content = new StringContent( payload, Encoding.UTF8, "application/json" );
				using HttpResponseMessage response = await HttpClientInstance.PostAsync( url, content );
				response_body = await response.Content.ReadAsStringAsync();

				if( !response.IsSuccessStatusCode )
				{
					ConsoleLogger.Error( $"Query returned '{response_body}'" );
					response_body = string.Empty;
				}
			}
			catch( Exception ex )
			{
				ConsoleLogger.Error( $"Exception during Post request - {ex.Message}" );
				ConsoleLogger.Error( $"Payload: '{payload}'" );
				response_body = string.Empty;
			}

			return response_body;
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

		private static void PrintHelp()
		{
			ConsoleLogger.Log( "Usage: PackageCheck <nuget|mpm|rubygems|go|pypi> <packagename> [version]" );

			ConsoleLogger.Log( "Expected output:" );
			ConsoleLogger.Log( "<Ecosystem> Package: 'PackageName - Version' (package age)" );
			ConsoleLogger.Log( "Copyright - 'Copyright message'" );

			ConsoleLogger.Log( "License(s) - List of 'SPDX Code(s) or Custom - License URL'" );

			ConsoleLogger.Log( "Download Uri - 'Download URL'" );
			ConsoleLogger.Log( "Project Uri - 'Project Homepage'" );
			ConsoleLogger.Log( "Source Code Uri - 'Source code URL'" );

			ConsoleLogger.Log( "Description - 'Description of the package'" );

			ConsoleLogger.Log( "Snyk uri - 'Link to Snyk.io for this package to show CVEs'" );
			ConsoleLogger.Log( "Dependencies - sorted by type" );
			ConsoleLogger.Log( "CVEs - 'List of CVE ids, severity, and NVD links'" );
		}

		private static bool ParseArguments( string[] args )
		{
			if( args.Length >= 2 )
			{
				Ecosystem = args[0].ToLower();
				PackageName = args[1];
				if( args.Length > 2 )
				{
					Version = args[2];
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

			if( !ParseArguments( args ) )
			{
				return -1;
			}

			switch( Ecosystem )
			{
				case "nuget":
					Ecosystem = "NuGet";
					NuGetPackage.CheckNuGetPackage( PackageName, Version );
					break;

				case "go":
					Ecosystem = "Go";
					GoPackage.CheckGoPackage( PackageName, Version );
					break;

				case "npm":
					Ecosystem = "npm";
					NpmPackage.CheckNpmPackage( PackageName, Version );
					break;

				case "rubygems":
					Ecosystem = "RubyGems";
					RubyGem.CheckRubyGem( PackageName, Version );
					break;

				case "pypi":
					Ecosystem = "PyPI";
					PythonPackage.CheckPythonPackage( PackageName, Version );
					break;

				default:
					ConsoleLogger.Warning( $"Invalid ecosystem - '{Ecosystem}'" );
					break;
			}

			ConsoleLogger.Success( $"Completed in {ConsoleLogger.TimeString( DateTime.UtcNow - start_time )}" );
			return 0;
		}
	}
}
