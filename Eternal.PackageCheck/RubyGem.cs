// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;

namespace Eternal.PackageCheck
{
	internal class RubyGemVersionRaw
	{
		public string number = string.Empty;
		public bool prerelease = false;
		public DateTime built_at = DateTime.MinValue;
		public DateTime created_at = DateTime.MinValue;
		public string authors = string.Empty;
		public string summary = string.Empty;
		public string description = string.Empty;
		public string platform = string.Empty;
		public string rubygems_version = string.Empty;
		public string? ruby_version = string.Empty;
		public string? ruby_abi = null;
		public long downloads_count = 0;
		public List<string>? licenses = new List<string>();
		public List<object>? requirements = new List<object>();
		public Dictionary<string, string>? metadata = new Dictionary<string, string>();
	}

	internal class RubyGemDependency
	{
		public string name = string.Empty;
		public string requirements = string.Empty;
	}

	internal class RubyGemDependencies
	{
		public List<RubyGemDependency> development = new List<RubyGemDependency>();
		public List<RubyGemDependency> runtime = new List<RubyGemDependency>();
	}

	internal class RubyGemRaw
	{
		public string name = string.Empty;
		public string version = string.Empty;
		public string copyright = string.Empty;
		public DateTime version_created_at = DateTime.MinValue;
		public string? authors = string.Empty;
		public List<string>? licenses = new List<string>();
		public string? info = string.Empty;
		public string? homepage_uri = string.Empty;
		public string? project_uri = string.Empty;
		public string? source_code_uri = string.Empty;
		public string gem_uri = string.Empty;
		public RubyGemDependencies? dependencies = new RubyGemDependencies();
	}

	internal class RubyGem
		: GenericPackage
	{
		/// <summary>
		/// The canonical name of this package type. Lower cased it is the command line verb, and upper cased it is the deps.dev system name, so neither is stored
		/// separately. It is also the display name and the ecosystem component of the .tps file name.
		/// </summary>
		internal const string PackageType = "RubyGems";

		/// <summary>The ecosystem name used by OSV, from https://ossf.github.io/osv-schema/#defined-ecosystems. OSV rejects anything else with an HTTP 400.</summary>
		internal const string OSVEcosystem = "RubyGems";

		/// <summary>The path segment used by security.snyk.io, which names several ecosystems differently to OSV.</summary>
		internal const string SnykEcosystem = "rubygems";

		private void AddLicense( LicenseInfo licenseInfo )
		{
			Licenses.Add( licenseInfo );
		}

		/// <summary>
		/// Delegates to the base class's license scraping and copyright cleanup; RubyGems packages need no additional fallback data beyond what the registry provides.
		/// </summary>
		internal void CleanupData()
		{
			base.CleanupData( PackageType, OSVEcosystem, SnykEcosystem );
		}

		private void AddDependencies( List<RubyGemDependency> dependencies, EDependencyType dependencyType )
		{
			foreach( RubyGemDependency dependency in dependencies )
			{
				PackageDependency package_dependency = new PackageDependency()
				{
					Name = dependency.name,
					Version = dependency.requirements,
					DependencyType = dependencyType
				};

				AddSingleDependency( package_dependency );
			}
		}

		private void AddAllDependencies( RubyGemRaw raw_gem )
		{
			if( raw_gem.dependencies != null )
			{
				AddDependencies( raw_gem.dependencies.runtime, EDependencyType.Production );
				AddDependencies( raw_gem.dependencies.development, EDependencyType.Development );
			}
		}

		/// <summary>
		/// Picks the version of a gem to use from its published versions. Only the "ruby" platform builds are considered, as the platform specific builds share their
		/// version numbers.
		/// </summary>
		/// <param name="gemName">The gem name, used for logging.</param>
		/// <param name="versions">The published versions of the gem.</param>
		/// <param name="requirements">Blank for the latest version, a published version number, or comma separated constraints such as ">= 1.6.0, &lt; 4".</param>
		/// <returns>The version number to use, or null if no version could be found.</returns>
		private static string? SelectVersion( string gemName, List<RubyGemVersionRaw> versions, string requirements )
		{
			List<RubyGemVersionRaw> ruby_versions = versions.Where( x => x.platform == "ruby" ).ToList();
			string trimmed_requirements = requirements.Trim();

			if( trimmed_requirements.Length == 0 )
			{
				RubyGemVersionRaw? latest_version = ruby_versions.FirstOrDefault( x => !x.prerelease );
				if( latest_version == null )
				{
					ConsoleLogger.Warning( $"No non-prerelease version found; defaulting to most recent." );
					latest_version = ruby_versions.FirstOrDefault();
					if( latest_version == null )
					{
						ConsoleLogger.Error( $"No valid versions found." );
						return null;
					}
				}

				return latest_version.number;
			}

			// A published version number is used as is, which also allows a prerelease to be requested by name
			RubyGemVersionRaw? exact_version = ruby_versions.Find( x => x.number == trimmed_requirements );
			if( exact_version != null )
			{
				return exact_version.number;
			}

			GemVersion? matching_version = RubyGemVersionMatcher.SelectMatchingVersion( gemName, ruby_versions, RubyGemVersionMatcher.SplitRequirements( trimmed_requirements ) );
			return matching_version?.ToString();
		}

		/// <summary>
		/// Looks up a RubyGems package's metadata: resolves the version to use from <paramref name="requirements"/>, then fetches that version's full metadata - authors,
		/// copyright, licenses, homepage, source code URI, and gem download URI.
		/// </summary>
		/// <param name="gemName">The gem name, e.g. "rails".</param>
		/// <param name="requirements">
		/// Blank to use the latest non-prerelease "ruby" platform version; a specific version number such as "3.1.22" (a prerelease can be requested this way); or
		/// comma separated constraints such as ">= 1.6.0, &lt; 4" or "~> 1.2", which select the highest non-prerelease version that satisfies all of them.
		/// </param>
		/// <param name="isRootPackage">True if this is the package being checked, rather than one of its dependencies, so its dependencies are collected.</param>
		/// <returns>The populated gem, or null if the gem, its versions, a version satisfying the requirements, or that version's metadata could not be retrieved.</returns>
		internal static RubyGem? GetRubyGem( string gemName, string requirements, bool isRootPackage )
		{
			string get_request = $"https://rubygems.org/api/v1/versions/{gemName}.json";
			string content = PackageCheck.Request( get_request ).Result;
			if( string.IsNullOrEmpty( content ) )
			{
				ConsoleLogger.Error( $"Failed to retrieve versioning data for the '{gemName}' gem from RubyGems.org" );
				return null;
			}

			List<RubyGemVersionRaw>? versions = JsonHelper.ReadJson<List<RubyGemVersionRaw>>( content );
			if( versions == null )
			{
				ConsoleLogger.Error( $"Failed to deserialize versioning data for the '{gemName}' gem from RubyGems.org" );
				return null;
			}

			string? version = SelectVersion( gemName, versions, requirements );
			if( version == null )
			{
				return null;
			}

			get_request = $"https://rubygems.org/api/v2/rubygems/{gemName}/versions/{version}.json";
			content = PackageCheck.Request( get_request ).Result;
			RubyGemRaw? raw_gem = JsonHelper.ReadJson<RubyGemRaw>( content );
			if( raw_gem == null )
			{
				return null;
			}

			RubyGem gem = new RubyGem
			{
				Name = raw_gem.name,
				Version = raw_gem.version,
				VersionCreatedAt = raw_gem.version_created_at,
				Authors = raw_gem.authors ?? string.Empty,
				Copyright = raw_gem.copyright,
				HomepageUri = raw_gem.homepage_uri ?? string.Empty,
				SourceCodeUri = raw_gem.source_code_uri ?? string.Empty,
				DownloadUri = raw_gem.gem_uri,
				Description = PackageCheck.SanitizeString( raw_gem.info ?? string.Empty )
			};

			if( raw_gem.licenses != null )
			{
				foreach( string license in raw_gem.licenses )
				{
					gem.AddLicense( new LicenseInfo( license ) );
				}
			}

			if( isRootPackage )
			{
				gem.AddAllDependencies( raw_gem );
			}

			return gem;
		}

		protected override void UpdateDependency( PackageDependency dependency )
		{
			RubyGem? gem = GetRubyGem( dependency.Name, dependency.Version, false );
			if( gem != null )
			{
				gem.CleanupDependencyData();
				gem.SetDependencyLicense( dependency );
			}
		}

		/// <summary>
		/// Looks up the given RubyGems package, cleans up its data (licenses, copyright, fallback URIs), and prints its details to the console. Does nothing if the gem
		/// or the requested version could not be found.
		/// </summary>
		/// <param name="gemName">The gem name, e.g. "rails".</param>
		/// <param name="version">The specific version to check, or "" to use the latest non-prerelease "ruby" platform version.</param>
		public static void CheckRubyGem( string gemName, string version )
		{
			RubyGem? gem = GetRubyGem( gemName, version, true );
			if( gem != null )
			{
				gem.CleanupData();
				gem.PrintDetails( PackageType );
			}
		}
	}
}
