// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;

namespace Eternal.PackageCheck
{
	internal class CargoPublishedBy
	{
		public string login = string.Empty;
		public string? name = string.Empty;
	}

	internal class CargoCrateVersionRaw
	{
		public string num = string.Empty;
		public string? license = string.Empty;
		public bool yanked = false;
		public DateTime created_at = DateTime.MinValue;
		public DateTime updated_at = DateTime.MinValue;
		public string dl_path = string.Empty;
		public long crate_size = 0;
		public string? checksum = string.Empty;
		public string? rust_version = string.Empty;
		public string? description = string.Empty;
		public string? homepage = string.Empty;
		public string? documentation = string.Empty;
		public string? repository = string.Empty;
		public CargoPublishedBy? published_by = null;
	}

	internal class CargoCrateRaw
	{
		public string name = string.Empty;
		public string? description = string.Empty;
		public string? homepage = string.Empty;
		public string? documentation = string.Empty;
		public string? repository = string.Empty;
		public string? default_version = string.Empty;
		public string? max_stable_version = string.Empty;
		public string? newest_version = string.Empty;
		public DateTime created_at = DateTime.MinValue;
		public DateTime updated_at = DateTime.MinValue;
	}

	internal class CargoCrateResponse
	{
		public CargoCrateRaw? crate = null;
		public List<CargoCrateVersionRaw>? versions = new List<CargoCrateVersionRaw>();
	}

	internal class CargoVersionResponse
	{
		public CargoCrateVersionRaw? version = null;
	}

	internal class CargoDependencyRaw
	{
		public string crate_id = string.Empty;
		public string req = string.Empty;
		public bool optional = false;
		public string kind = string.Empty;
	}

	internal class CargoDependenciesResponse
	{
		public List<CargoDependencyRaw>? dependencies = new List<CargoDependencyRaw>();
	}

	internal class CargoPackage
		: GenericPackage
	{
		/// <summary>
		/// The canonical name of this package type. Lower cased it is the command line verb, and upper cased it is the deps.dev system name, so neither is stored
		/// separately. It is also the display name and the ecosystem component of the .tps file name.
		/// </summary>
		internal const string PackageType = "Cargo";

		/// <summary>The ecosystem name used by OSV, from https://ossf.github.io/osv-schema/#defined-ecosystems. OSV rejects anything else with an HTTP 400.</summary>
		internal const string OSVEcosystem = "crates.io";

		/// <summary>Empty because Snyk's crates.io catalog only lists crates that already have an advisory, so it 404s for most of them; deps.dev is used instead.</summary>
		internal const string SnykEcosystem = "";

		private void AddLicense( LicenseInfo licenseInfo )
		{
			Licenses.Add( licenseInfo );
		}

		/// <summary>
		/// Delegates to the base class's license scraping and copyright cleanup; crates.io packages need no additional fallback data beyond what the registry provides.
		/// </summary>
		internal void CleanupData()
		{
			base.CleanupData( PackageType, OSVEcosystem, SnykEcosystem );
		}

		/// <summary>
		/// Maps a crates.io dependency kind onto the generic dependency type. Cargo has three kinds: "normal" dependencies are linked into the crate and ship with it,
		/// while "dev" dependencies (tests, examples, benchmarks) and "build" dependencies (build scripts) are only needed to build and test the crate and are not
		/// redistributed, so both are recorded as development dependencies.
		/// </summary>
		/// <param name="kind">The "kind" field from the crates.io dependencies endpoint.</param>
		/// <returns>The dependency type to record.</returns>
		private static EDependencyType GetDependencyType( string kind )
		{
			return kind switch
			{
				"normal" => EDependencyType.Production,
				"dev" => EDependencyType.Development,
				"build" => EDependencyType.Build,
				_ => EDependencyType.Unknown
			};
		}

		/// <summary>
		/// Records every dependency of the resolved version. Optional dependencies are included as they are still shipped whenever the feature that gates them is
		/// enabled, so they remain relevant to a license review.
		/// </summary>
		/// <param name="dependencies">The dependencies returned by crates.io for the resolved version.</param>
		private void AddAllDependencies( List<CargoDependencyRaw> dependencies )
		{
			foreach( CargoDependencyRaw dependency in dependencies )
			{
				PackageDependency package_dependency = new PackageDependency()
				{
					Name = dependency.crate_id,
					Version = dependency.req,
					DependencyType = GetDependencyType( dependency.kind )
				};

				AddSingleDependency( package_dependency );
			}
		}

		/// <summary>
		/// Fetches the dependency list for a resolved version of a crate. crates.io serves dependencies from their own endpoint rather than inlining them in the
		/// version payload.
		/// </summary>
		/// <param name="crateName">The crate name.</param>
		/// <param name="version">The resolved version number.</param>
		/// <returns>The dependencies, or an empty list if they could not be retrieved.</returns>
		private static List<CargoDependencyRaw> GetDependencies( string crateName, string version )
		{
			string get_request = $"https://crates.io/api/v1/crates/{Uri.EscapeDataString( crateName )}/{Uri.EscapeDataString( version )}/dependencies";
			string content = PackageCheck.Request( get_request ).Result;
			if( string.IsNullOrEmpty( content ) )
			{
				ConsoleLogger.Warning( $"Failed to retrieve dependencies for the '{crateName}' crate from crates.io" );
				return [];
			}

			CargoDependenciesResponse? response = JsonHelper.ReadJson<CargoDependenciesResponse>( content );
			return response?.dependencies ?? [];
		}

		/// <summary>
		/// Picks the version of a crate to use from its published versions.
		/// </summary>
		/// <param name="crateName">The crate name, used for logging.</param>
		/// <param name="crate">The crate level metadata, used for its registry computed latest version as a fallback.</param>
		/// <param name="versions">The published versions of the crate.</param>
		/// <param name="requirements">Blank for the latest stable version, a published version number, or Cargo constraints such as "^1.2" or "&gt;=1.2, &lt;1.5".</param>
		/// <returns>The version number to use, or null if no version could be found.</returns>
		private static string? SelectVersion( string crateName, CargoCrateRaw? crate, List<CargoCrateVersionRaw> versions, string requirements )
		{
			string trimmed_requirements = requirements.Trim();

			if( trimmed_requirements.Length == 0 )
			{
				// The registry lists its versions in publication order rather than version order, and a patch to an older release line can be published after a
				// newer one, so the highest version is found by comparing them rather than by taking the first entry.
				CargoVersion? latest_version = CargoVersionMatcher.SelectMatchingVersion( crateName, versions, [] );
				if( latest_version != null )
				{
					return latest_version.ToString();
				}

				ConsoleLogger.Warning( $"No stable version found for the '{crateName}' crate; falling back to the registry's latest." );
				string? registry_version = crate?.max_stable_version;
				if( string.IsNullOrEmpty( registry_version ) )
				{
					registry_version = crate?.default_version;
				}

				if( string.IsNullOrEmpty( registry_version ) )
				{
					ConsoleLogger.Error( $"No valid versions found." );
					return null;
				}

				return registry_version;
			}

			// A published version number is used as is, which also allows a prerelease or a yanked version to be requested by name. Note that Cargo itself would
			// read a bare "1.2.3" in a Cargo.toml as the caret requirement "^1.2.3"; an exact version is honoured here so that asking this tool to check a specific
			// version reports that version rather than a later compatible one.
			CargoCrateVersionRaw? exact_version = versions.Find( x => x.num == trimmed_requirements );
			if( exact_version != null )
			{
				return exact_version.num;
			}

			CargoVersion? matching_version = CargoVersionMatcher.SelectMatchingVersion( crateName, versions, CargoVersionMatcher.SplitRequirements( trimmed_requirements ) );
			return matching_version?.ToString();
		}

		/// <summary>
		/// Picks the best available source for a value that crates.io publishes both per version and per crate, preferring the version's own value.
		/// </summary>
		private static string FirstNonEmpty( string? preferred, string? fallback )
		{
			if( !string.IsNullOrEmpty( preferred ) )
			{
				return preferred;
			}

			return fallback ?? string.Empty;
		}

		/// <summary>
		/// Records the crate's licenses. crates.io stores a single SPDX expression rather than a list, so a dual licensed crate such as "MIT OR Apache-2.0" is split
		/// into one entry per license and the usual license priority ordering then picks the preferred one.
		/// </summary>
		/// <param name="licenseExpression">The raw "license" field from crates.io.</param>
		private void AddLicensesFromExpression( string? licenseExpression )
		{
			if( string.IsNullOrEmpty( licenseExpression ) )
			{
				ConsoleLogger.Warning( "No license expression published for this crate." );
				return;
			}

			foreach( string spdx_code in LicenseChecker.ParseLicenseExpression( licenseExpression ) )
			{
				AddLicense( new LicenseInfo( spdx_code ) );
			}
		}

		/// <summary>
		/// Looks up a crates.io package's metadata: resolves the version to use from <paramref name="requirements"/>, then fetches that version's full metadata -
		/// publisher, licenses, homepage, repository, and download URI.
		/// </summary>
		/// <param name="crateName">The crate name, e.g. "serde".</param>
		/// <param name="requirements">
		/// Blank to use the highest stable, non-yanked version; a specific version number such as "1.0.228" (a prerelease can be requested this way); or Cargo
		/// constraints such as "^1.2", "~1.2.3", "1.*", or "&gt;=1.2, &lt;1.5", which select the highest version that satisfies all of them.
		/// </param>
		/// <param name="isRootPackage">True if this is the package being checked, rather than one of its dependencies, so its dependencies are collected.</param>
		/// <returns>The populated crate, or null if the crate, its versions, a version satisfying the requirements, or that version's metadata could not be retrieved.</returns>
		internal static CargoPackage? GetCargoPackage( string crateName, string requirements, bool isRootPackage )
		{
			string get_request = $"https://crates.io/api/v1/crates/{Uri.EscapeDataString( crateName )}";
			string content = PackageCheck.Request( get_request ).Result;
			if( string.IsNullOrEmpty( content ) )
			{
				ConsoleLogger.Error( $"Failed to retrieve versioning data for the '{crateName}' crate from crates.io" );
				return null;
			}

			CargoCrateResponse? crate_response = JsonHelper.ReadJson<CargoCrateResponse>( content );
			if( crate_response == null || crate_response.versions == null )
			{
				ConsoleLogger.Error( $"Failed to deserialize versioning data for the '{crateName}' crate from crates.io" );
				return null;
			}

			string? version = SelectVersion( crateName, crate_response.crate, crate_response.versions, requirements );
			if( version == null )
			{
				return null;
			}

			get_request = $"https://crates.io/api/v1/crates/{Uri.EscapeDataString( crateName )}/{Uri.EscapeDataString( version )}";
			content = PackageCheck.Request( get_request ).Result;
			CargoVersionResponse? version_response = JsonHelper.ReadJson<CargoVersionResponse>( content );
			if( version_response == null || version_response.version == null )
			{
				ConsoleLogger.Error( $"Failed to retrieve metadata for version '{version}' of the '{crateName}' crate from crates.io" );
				return null;
			}

			CargoCrateVersionRaw raw_version = version_response.version;
			CargoCrateRaw? raw_crate = crate_response.crate;

			if( raw_version.yanked )
			{
				ConsoleLogger.Warning( $"Version '{version}' of the '{crateName}' crate has been yanked." );
			}

			CargoPackage cargo_package = new CargoPackage
			{
				Name = raw_crate?.name ?? crateName,
				Version = raw_version.num,
				VersionCreatedAt = raw_version.created_at,
				Authors = FirstNonEmpty( raw_version.published_by?.name, raw_version.published_by?.login ),
				Copyright = string.Empty,
				HomepageUri = FirstNonEmpty( FirstNonEmpty( raw_version.homepage, raw_crate?.homepage ), $"https://crates.io/crates/{crateName}" ),
				SourceCodeUri = FirstNonEmpty( raw_version.repository, raw_crate?.repository ),
				DownloadUri = $"https://crates.io/api/v1/crates/{Uri.EscapeDataString( crateName )}/{Uri.EscapeDataString( raw_version.num )}/download",
				Description = PackageCheck.SanitizeString( FirstNonEmpty( raw_version.description, raw_crate?.description ) )
			};

			cargo_package.AddLicensesFromExpression( raw_version.license );

			if( isRootPackage )
			{
				cargo_package.AddAllDependencies( GetDependencies( crateName, raw_version.num ) );
			}

			return cargo_package;
		}

		protected override void UpdateDependency( PackageDependency dependency )
		{
			CargoPackage? cargo_package = GetCargoPackage( dependency.Name, dependency.Version, false );
			if( cargo_package != null )
			{
				cargo_package.CleanupDependencyData();
				cargo_package.SetDependencyLicense( dependency );
			}
		}

		/// <summary>
		/// Looks up the given crates.io package, cleans up its data (licenses, copyright, fallback URIs), and prints its details to the console. Does nothing if the
		/// crate or the requested version could not be found.
		/// </summary>
		/// <param name="crateName">The crate name, e.g. "serde".</param>
		/// <param name="version">The specific version to check, or "" to use the highest stable, non-yanked version.</param>
		public static void CheckCargoPackage( string crateName, string version )
		{
			CargoPackage? cargo_package = GetCargoPackage( crateName, version, true );
			if( cargo_package != null )
			{
				cargo_package.CleanupData();
				cargo_package.PrintDetails( PackageType );
			}
		}
	}
}
