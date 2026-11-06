// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;
using System.Text;

namespace Eternal.PackageCheck
{
	/// <summary>
	/// The deps.dev "versionKey" object identifying a package or a specific version of it. "version" is empty when used as a bare package key.
	/// </summary>
	public class GoPackageVersionKey
	{
		public string system = string.Empty;
		public string name = string.Empty;
		public string version = string.Empty;
	}

	/// <summary>
	/// One entry of the "versions" array returned by https://api.deps.dev/v3/systems/go/packages/{module}. "isDefault" marks
	/// the version deps.dev considers the latest stable release.
	/// </summary>
	public class GoPackageVersionSummary
	{
		public GoPackageVersionKey versionKey = new GoPackageVersionKey();
		public DateTime publishedAt = DateTime.MinValue;
		public bool isDefault = false;
	}

	/// <summary>
	/// The package-level response from deps.dev listing every known version.
	/// </summary>
	public class GoPackageMetadata
	{
		public GoPackageVersionKey packageKey = new GoPackageVersionKey();
		public List<GoPackageVersionSummary> versions = new List<GoPackageVersionSummary>();
	}

	/// <summary>
	/// A labelled link from the version-level response. Known labels include "SOURCE_REPO", "HOMEPAGE", "ORIGIN", and "ISSUE_TRACKER".
	/// </summary>
	public class GoPackageLink
	{
		public string label = string.Empty;
		public string url = string.Empty;
	}

	/// <summary>
	/// The version-level response from
	/// https://api.deps.dev/v3/systems/go/packages/{module}/versions/{version}, carrying the licenses and links for a single published version.
	/// </summary>
	public class GoPackageVersionDetails
	{
		public GoPackageVersionKey versionKey = new GoPackageVersionKey();
		public DateTime publishedAt = DateTime.MinValue;
		public bool isDefault = false;
		public List<string>? licenses = new List<string>();
		public List<GoPackageLink> links = new List<GoPackageLink>();
	}

	/// <summary>
	/// Identifies a project by its source-repository id, e.g. { "id": "github.com/disintegration/imaging" }.
	/// </summary>
	public class GoProjectKey
	{
		public string id = string.Empty;
	}

	/// <summary>
	/// The response from https://api.deps.dev/v3/projects/{source-repo-id}, e.g. https://api.deps.dev/v3/projects/github.com%2Fdisintegration%2Fimaging
	/// The id passed in the URL is the bare "host/owner/repo" form (no scheme), taken from a version response's relatedProjects[].projectKey.id or links[].url with the
	/// scheme stripped.
	/// </summary>
	public class GoPackageInfo
	{
		public GoProjectKey projectKey = new GoProjectKey();
		public string license = string.Empty;
		public string description = string.Empty;
		public string homepage = string.Empty;
	}

	public class GoDependency
	{
		public string name = string.Empty;
		public string requirement = string.Empty;
	}

	public class GoReplace
	{
		public GoDependency? original { get; set; }
		public GoDependency? replacement { get; set; }
	}

	public class GoRequirements
	{
		public List<GoDependency> directDependencies { get; set; } = new List<GoDependency>();
		public List<GoDependency> indirectDependencies { get; set; } = new List<GoDependency>();
		public List<GoReplace> replaces { get; set; } = new List<GoReplace>();
		public List<GoDependency> excludes { get; set; } = new List<GoDependency>();
	}

	public class GoPackageRequirements
	{
		public GoRequirements go = new GoRequirements();
	}

	internal class GoPackage
		: GenericPackage
	{
		/// <summary>
		/// The canonical name of this package type. Lower cased it is the command line verb, and upper cased it is the deps.dev system name, so neither is stored
		/// separately. It is also the display name and the ecosystem component of the .tps file name.
		/// </summary>
		internal const string PackageType = "Go";

		/// <summary>The ecosystem name used by OSV, from https://ossf.github.io/osv-schema/#defined-ecosystems. OSV rejects anything else with an HTTP 400.</summary>
		internal const string OSVEcosystem = "Go";

		/// <summary>The path segment used by security.snyk.io, which calls Go "golang".</summary>
		internal const string SnykEcosystem = "golang";

		private void AddLicense( LicenseInfo licenseInfo )
		{
			Licenses.Add( licenseInfo );
		}

		/// <summary>
		/// Applies the Go module proxy escaping rule (golang.org/x/mod/module.EscapePath): each uppercase ASCII letter is replaced with "!" followed by its lowercase form,
		/// so "github.com/BurntSushi/toml" becomes "github.com/!burnt!sushi/toml". The "!" character itself is not permitted in module paths, so the mapping is unambiguous.
		/// </summary>
		private static string EscapeGoModuleCase( string value )
		{
			StringBuilder builder = new StringBuilder( value.Length );

			foreach( char current in value )
			{
				if( current >= 'A' && current <= 'Z' )
				{
					builder.Append( '!' );
					builder.Append( ( char )( current - 'A' + 'a' ) );
				}
				else
				{
					builder.Append( current );
				}
			}

			return builder.ToString();
		}

		private static string NormalizedVersion( string version )
		{
			string normalized_version = version;
			if( !normalized_version.StartsWith( "v", StringComparison.Ordinal ) )
			{
				normalized_version = "v" + normalized_version;
			}

			return normalized_version;
		}

		private void SetDownloadUri()
		{
			if( string.IsNullOrEmpty( DownloadUri ) )
			{
				string escaped_path = EscapeGoModuleCase( Name );
				string escaped_version = EscapeGoModuleCase( NormalizedVersion( Version ) );

				DownloadUri = "https://proxy.golang.org/" + escaped_path + "/@v/" + escaped_version + ".zip";
			}
		}


		/// <summary>
		/// Fills in the Go-specific fallback homepage/download URIs before delegating to the base class's license scraping and copyright cleanup.
		/// </summary>
		internal void CleanupData()
		{
			SetDownloadUri();

			if( string.IsNullOrEmpty( HomepageUri ) )
			{
				HomepageUri = $"https://pkg.go.dev/{Name}@{Version}";
			}

			base.CleanupData( PackageType, OSVEcosystem, SnykEcosystem );
		}

		/// <summary>
		/// Records where a reviewer can see this package's own dependencies, which deps.dev lists for every supported ecosystem.
		/// </summary>
		/// <param name="packageType">The package type's canonical name, lower cased to form the deps.dev URL.</param>
		protected override void SetDependencyUri( string packageType )
		{
			// e.g. https://deps.dev/nuget/newtonsoft.json/13.0.4/requirements
			DependencyUri = $"https://deps.dev/{packageType.ToLowerInvariant()}/{Uri.EscapeDataString( Name )}/{Uri.EscapeDataString( Version )}/requirements";
		}

		private void AddDependencies( List<GoDependency> goDependencies, EDependencyType dependencyType )
		{
			foreach( GoDependency go_dependency in goDependencies )
			{
				PackageDependency package_dependency = new PackageDependency
				{
					Name = go_dependency.name,
					Version = go_dependency.requirement,
					DependencyType = dependencyType
				};

				AddSingleDependency( package_dependency );
			}
		}

		private void AddAllDependencies( string dependenciesRequest )
		{
			string dependencies = PackageCheck.Request( dependenciesRequest ).Result;
			GoPackageRequirements? requirements = JsonHelper.ReadJson<GoPackageRequirements>( dependencies );
			if( requirements != null )
			{
				AddDependencies( requirements.go.directDependencies, EDependencyType.Direct );
				AddDependencies( requirements.go.indirectDependencies, EDependencyType.Indirect );
			}
		}

		/// <summary>
		/// Picks the version of a module to use from its published versions. Go modules are always referenced by an exact version - go.mod has no range syntax - so this is
		/// close to a passthrough: a blank requirement takes the default (latest stable) version, and anything else must match a published version exactly.
		/// </summary>
		/// <param name="packageName">The module path, used for logging.</param>
		/// <param name="versions">The published versions of the module.</param>
		/// <param name="requirement">Blank for the default version, or an exact version such as "v1.8.0"; the leading "v" is optional.</param>
		/// <returns>The version number to use, or null if no version could be found.</returns>
		private static string? SelectVersion( string packageName, List<GoPackageVersionSummary> versions, string requirement )
		{
			string trimmed_requirement = requirement.Trim();

			if( trimmed_requirement.Length == 0 )
			{
				GoPackageVersionSummary? default_version = versions.FirstOrDefault( x => x.isDefault );
				if( default_version == null )
				{
					ConsoleLogger.Warning( $"No default version found for the '{packageName}' Go package; falling back to last." );
					default_version = versions.LastOrDefault();
				}

				if( default_version == null )
				{
					ConsoleLogger.Error( $"No published versions found for the '{packageName}' Go package on deps.dev" );
					return null;
				}

				return default_version.versionKey.version;
			}

			// go.mod records exact versions rather than ranges, so the requirement only needs normalizing before an equality match
			GoPackageVersionSummary? exact_version = versions.FirstOrDefault( x => x.versionKey.version == NormalizedVersion( trimmed_requirement ) );
			if( exact_version == null )
			{
				ConsoleLogger.Error( $"Version {trimmed_requirement} not found for the '{packageName}' Go package on deps.dev" );
				return null;
			}

			return exact_version.versionKey.version;
		}

		/// <summary>
		/// Looks up a Go module's metadata from deps.dev: resolves the requested version (or the default/latest one if <paramref name="version"/> is empty), fetches its
		/// licenses and links, and - for GitHub/GitLab-hosted source - the project's description via the deps.dev projects endpoint.
		/// </summary>
		/// <param name="packageName">The module path, e.g. "golang.org/x/sys".</param>
		/// <param name="version">The specific version to fetch, or "" to use the default/latest version.</param>
		/// <param name="isRootPackage">True if this is the package being checked, rather than one of its dependencies, so its dependencies are collected.</param>
		/// <returns>The populated package, or null if the module, version, or its details could not be retrieved.</returns>
		internal static GoPackage? GetGoPackage( string packageName, string version, bool isRootPackage )
		{
			// deps.dev requires the module path to be a single URL path segment,
			// so the "/" separators must be escaped (e.g. github.com%2Fgorilla%2Fmux).
			string package_request = $"https://api.deps.dev/v3/systems/go/packages/{Uri.EscapeDataString( packageName )}";
			string package_content = PackageCheck.Request( package_request ).Result;
			if( string.IsNullOrEmpty( package_content ) )
			{
				ConsoleLogger.Error( $"Failed to retrieve data about the '{packageName}' Go module from deps.dev" );
				return null;
			}

			GoPackageMetadata? metadata = JsonHelper.ReadJson<GoPackageMetadata>( package_content );
			if( metadata == null )
			{
				return null;
			}

			string? selected_version = SelectVersion( packageName, metadata.versions, version );
			if( selected_version == null )
			{
				return null;
			}

			string version_request = $"{package_request}/versions/{Uri.EscapeDataString( selected_version )}";
			string version_content = PackageCheck.Request( version_request ).Result;
			if( string.IsNullOrEmpty( version_content ) )
			{
				return null;
			}

			GoPackageVersionDetails? details = JsonHelper.ReadJson<GoPackageVersionDetails>( version_content );
			if( details == null )
			{
				return null;
			}

			string source_code_uri = string.Empty;
			string homepage_uri = string.Empty;
			string download_uri = string.Empty;
			foreach( GoPackageLink link in details.links )
			{
				if( link.label == "SOURCE_REPO" )
				{
					source_code_uri = link.url;
				}
				else if( link.label == "HOMEPAGE" )
				{
					homepage_uri = link.url;
				}
				else if( link.label == "ORIGIN" )
				{
					download_uri = link.url;
				}
			}

			// PackageInfo is only valid for GitHub and GitLab repos
			GoPackageInfo? go_package_info = null;
			if( !string.IsNullOrEmpty( source_code_uri ) )
			{
				if( source_code_uri.Contains( "github", StringComparison.OrdinalIgnoreCase )
				    || source_code_uri.Contains( "gitlab", StringComparison.OrdinalIgnoreCase ) )
				{
					string info_package_name = string.Empty;
					int slash_offset = source_code_uri.IndexOf( "//", StringComparison.OrdinalIgnoreCase );
					if( slash_offset > 0 )
					{
						info_package_name = source_code_uri.Substring( slash_offset + 2 );
					}

					string info_request = $"https://api.deps.dev/v3/projects/{Uri.EscapeDataString( info_package_name )}";
					string info_content = PackageCheck.Request( info_request ).Result;
					go_package_info = JsonHelper.ReadJson<GoPackageInfo>( info_content );
				}
			}

			GoPackage go_package = new GoPackage
			{
				Name = packageName,
				Version = details.versionKey.version,
				VersionCreatedAt = details.publishedAt,
				Authors = string.Empty,
				Copyright = string.Empty,
				HomepageUri = homepage_uri,
				SourceCodeUri = source_code_uri,
				DownloadUri = download_uri,
				Description = PackageCheck.SanitizeString( go_package_info?.description ?? string.Empty )
			};

			// Add in any licenses
			if( details.licenses != null )
			{
				foreach( string license in details.licenses )
				{
					go_package.AddLicense( new LicenseInfo( license ) );
				}
			}

			if( isRootPackage )
			{
				// Add in any dependencies
				string dependencies_request = $"{package_request}/versions/{Uri.EscapeDataString( selected_version )}:requirements";
				go_package.AddAllDependencies( dependencies_request );
			}

			return go_package;
		}

		protected override void UpdateDependency( PackageDependency dependency )
		{
			GoPackage? go_package = GetGoPackage( dependency.Name, "", false );
			if( go_package != null )
			{
				go_package.SetDownloadUri();
				go_package.CleanupDependencyData();
				go_package.SetDependencyLicense( dependency );
			}
		}

		/// <summary>
		/// Looks up the given Go module, cleans up its data (licenses, copyright, fallback URIs), and prints its details to the console. Does nothing if the module or the
		/// requested version could not be found.
		/// </summary>
		/// <param name="packageName">The module path, e.g. "golang.org/x/sys".</param>
		/// <param name="version">The specific version to check, or "" to use the default/latest version.</param>
		public static void CheckGoPackage( string packageName, string version )
		{
			GoPackage? go_package = GetGoPackage( packageName, version, true );
			if( go_package != null )
			{
				go_package.CleanupData();
				go_package.PrintDetails( PackageType );
			}
		}
	}
}
