// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;
using System.Text;
using YamlDotNet.Core;

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

			base.CleanupData( "Go" );
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
		/// Looks up a Go module's metadata from deps.dev: resolves the requested version (or the default/latest one if <paramref name="version"/> is empty), fetches its
		/// licenses and links, and - for GitHub/GitLab-hosted source - the project's description via the deps.dev projects endpoint.
		/// </summary>
		/// <param name="packageName">The module path, e.g. "golang.org/x/sys".</param>
		/// <param name="version">The specific version to fetch, or "" to use the default/latest version.</param>
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

			GoPackageVersionSummary? requested_version;
			if( string.IsNullOrEmpty( version ) )
			{
				requested_version = metadata.versions.FirstOrDefault( x => x.isDefault );
				if( requested_version == null )
				{
					ConsoleLogger.Warning( $"No default version found for the '{packageName}' Go package; falling back to last." );
					requested_version = metadata.versions.LastOrDefault();
				}

				if( requested_version == null )
				{
					ConsoleLogger.Error( $"No published versions found for the '{packageName}' Go package on deps.dev" );
					return null;
				}
			}
			else
			{
				requested_version = metadata.versions.FirstOrDefault( x => x.versionKey.version == NormalizedVersion( version ) );
				if( requested_version == null )
				{
					ConsoleLogger.Error( $"Version {version} not found for the '{packageName}' Go package on deps.dev" );
					return null;
				}
			}

			string version_request = $"{package_request}/versions/{Uri.EscapeDataString( requested_version.versionKey.version )}";
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
				string dependencies_request = $"{package_request}/versions/{Uri.EscapeDataString( requested_version.versionKey.version )}:requirements";
				go_package.AddAllDependencies( dependencies_request );
			}

			return go_package;
		}

		protected override void UpdateDependency( PackageDependency dependency )
		{
			GoPackage? go_package = GetGoPackage( dependency.Name, "", false );
			if( go_package == null )
			{
				return;
			}

			go_package.SetDownloadUri();
			go_package.CleanupDependencyData( "Go" );

			base.UpdateDependency( dependency );
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
			if( go_package == null )
			{
				return;
			}

			go_package.CleanupData();

			go_package.PrintDetails( "Go" );
		}
	}
}
