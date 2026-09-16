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
		private void AddLicense( LicenseInfo licenseInfo )
		{
			Licenses.Add( licenseInfo );
		}

		/// <summary>
		/// Delegates to the base class's license scraping and copyright cleanup; RubyGems packages need no additional fallback data beyond what the registry provides.
		/// </summary>
		internal void CleanupData()
		{
			base.CleanupData( "RubyGems" );
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
		/// Looks up a RubyGems package's metadata: resolves the requested version (or the most recent non-prerelease "ruby" platform version if <paramref name="version"/>
		/// is empty), then fetches that version's full metadata - authors, copyright, licenses, homepage, source code URI, and gem download URI.
		/// </summary>
		/// <param name="gemName">The gem name, e.g. "rails".</param>
		/// <param name="version">The specific version to fetch, or "" to use the latest non-prerelease "ruby" platform version.</param>
		/// <returns>The populated gem, or null if the gem, its versions, or the requested version's metadata could not be retrieved.</returns>
		internal static RubyGem? GetRubyGem( string gemName, string version, bool isRootPackage )
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

			RubyGemVersionRaw? gem_version;
			if( string.IsNullOrEmpty( version ) )
			{
				gem_version = versions.FirstOrDefault( x => !x.prerelease && x.platform == "ruby" );
				if( gem_version == null )
				{
					ConsoleLogger.Warning( $"No non-prerelease version found; defaulting to most recent." );
					gem_version = versions.FirstOrDefault( x => x.platform == "ruby" );
					if( gem_version == null )
					{
						ConsoleLogger.Error( $"No valid versions found." );
						return null;
					}
				}

				version = gem_version.number;
			}
			else
			{
				gem_version = versions.Find( x => x.number == version && x.platform == "ruby" );
				if( gem_version == null )
				{
					ConsoleLogger.Error( $"Version '{version}' not found found for Gem '{gemName}'" );
					return null;
				}

				version = gem_version.number;
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
			RubyGem? gem = GetRubyGem( dependency.Name, "", false );
			if( gem == null )
			{
				return;
			}

			gem.CleanupDependencyData( "RubyGems" );

			base.UpdateDependency( dependency );
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
			if( gem == null )
			{
				return;
			}

			gem.CleanupData();

			gem.PrintDetails( "RubyGems" );
		}
	}
}
