// Copyright Eternal Developments, LLC. All Rights Reserved.

using System.Globalization;
using System.Text.RegularExpressions;
using Eternal.ConsoleUtilities;

namespace Eternal.PackageCheck
{
	internal class DebMadisonEntryRaw
	{
		public string component = string.Empty;
		public List<string>? architectures = new List<string>();
		public string source = string.Empty;
		public string source_version = string.Empty;
	}

	internal class DebSuiteRaw
	{
		public string name = string.Empty;
		public string? codename = string.Empty;
		public string? version = string.Empty;
	}

	internal class DebSnapshotBinaryRaw
	{
		public string binary_version = string.Empty;
		public string name = string.Empty;
		public string source = string.Empty;
		public string version = string.Empty;
	}

	internal class DebSnapshotBinaryResponse
	{
		public List<DebSnapshotBinaryRaw>? result = new List<DebSnapshotBinaryRaw>();
	}

	internal class DebSnapshotSourceVersionRaw
	{
		public string version = string.Empty;
	}

	internal class DebSnapshotSourceResponse
	{
		public List<DebSnapshotSourceVersionRaw>? result = new List<DebSnapshotSourceVersionRaw>();
	}

	internal class DebSnapshotFileRaw
	{
		public string archive_name = string.Empty;
		public string first_seen = string.Empty;
		public string name = string.Empty;
		public string path = string.Empty;
		public long size = 0;
	}

	internal class DebSnapshotSourceFilesResponse
	{
		public Dictionary<string, List<DebSnapshotFileRaw>>? fileinfo = new Dictionary<string, List<DebSnapshotFileRaw>>();
	}

	/// <summary>
	/// A version of a binary package, along with the source package and source version that built it. The two versions differ for a binary-only rebuild, such
	/// as "8.14.1-2+b1" built from source "8.14.1-2".
	/// </summary>
	internal class DebResolvedVersion
	{
		public string BinaryVersion = string.Empty;
		public string Source = string.Empty;
		public string SourceVersion = string.Empty;

		/// <summary>The suite of the current archive the version was found in, e.g. "stable", or "" if it was only found in snapshot.debian.org.</summary>
		public string Suite = string.Empty;
	}

	internal class DebPackage
		: GenericPackage
	{
		/// <summary>
		/// The canonical name of this package type. Lower cased it is the command line verb and the purl type. It is also the display name and the ecosystem
		/// component of the .tps file name.
		/// </summary>
		internal const string PackageType = "Deb";

		/// <summary>
		/// The ecosystem name used by OSV, from https://ossf.github.io/osv-schema/#defined-ecosystems. OSV records Debian advisories per release, e.g. "Debian:13",
		/// and without that suffix a query matches every release - including advisories that are still open in an older release but long fixed in the version
		/// being checked - so the release is appended whenever it is known.
		/// </summary>
		private const string OSVEcosystem = "Debian";

		/// <summary>Empty as neither Snyk nor deps.dev is used; the Debian security tracker is linked to instead.</summary>
		private const string SnykEcosystem = "";

		/// <summary>
		/// The suites to take the version from when none is requested, in order of preference. A package only in testing or unstable is new, or has been removed
		/// from stable, and is still worth reporting.
		/// </summary>
		private static readonly string[] PreferredSuites = ["stable", "testing", "unstable"];

		/// <summary>Matches a Debian license exception, "with OpenSSL exception", which SPDX spells "WITH OpenSSL".</summary>
		private static readonly Regex LicenseException = new Regex( @"\bwith\s+(\S+)\s+exception\b", RegexOptions.IgnoreCase | RegexOptions.Compiled );

		/// <summary>
		/// Matches the release number that a stable update or a backport carries in its version, e.g. the 13 in "8.14.1-2+deb13u5" or "8.20.0-5~bpo13+1".
		/// </summary>
		private static readonly Regex ReleaseSuffix = new Regex( @"[+~](?:deb|bpo)(\d+)", RegexOptions.Compiled );

		/// <summary>The release number of each suite in the current archive, e.g. "stable" to "13", fetched once on first use.</summary>
		private static readonly Lazy<Dictionary<string, string>> SuiteReleases = new Lazy<Dictionary<string, string>>( GetSuiteReleases );

		/// <summary>The source package that built this binary package, which is what OSV and the Debian security tracker key their data on.</summary>
		internal string SourceName { get; private set; } = string.Empty;

		/// <summary>The version of the source package that built this binary package.</summary>
		internal string SourceVersion { get; private set; } = string.Empty;

		/// <summary>The number of the Debian release this version belongs to, e.g. "13", or "" if it could not be determined.</summary>
		internal string DebianRelease { get; private set; } = string.Empty;

		private void AddLicense( LicenseInfo licenseInfo )
		{
			Licenses.Add( licenseInfo );
		}

		/// <summary>
		/// Delegates to the base class's license scraping and copyright cleanup; the licenses come from the package's machine-readable copyright file, so no
		/// additional fallback data is needed.
		/// </summary>
		internal void CleanupData()
		{
			base.CleanupData( PackageType, OSVEcosystem, SnykEcosystem );
		}

		/// <summary>
		/// deps.dev does not carry Debian packages, so a reviewer is sent to the Debian package search instead, which lists every suite's version of the package
		/// and links to its dependencies.
		/// </summary>
		protected override void SetDependencyUri( string packageType )
		{
			DependencyUri = $"https://packages.debian.org/search?keywords={Uri.EscapeDataString( Name )}&searchon=names&exact=1&suite=all&section=all";
		}

		/// <summary>
		/// Neither Snyk nor deps.dev has a usable page for a Debian package, so a reviewer is sent to the Debian security tracker, which tracks advisories by source
		/// package across every release.
		/// </summary>
		protected override void SetVulnerabilityUri( string packageType, string snykEcosystem )
		{
			VulnerabilityUri = $"https://security-tracker.debian.org/tracker/source-package/{Uri.EscapeDataString( SourceName )}";
		}

		/// <summary>
		/// Debian advisories are recorded against the source package, so OSV is asked about the source package and version rather than the binary, and only
		/// about the release the version belongs to when that is known.
		/// </summary>
		protected override void CheckCves( string osvEcosystem )
		{
			string release_ecosystem = osvEcosystem;
			if( DebianRelease.Length > 0 )
			{
				release_ecosystem = $"{osvEcosystem}:{DebianRelease}";
			}
			else
			{
				ConsoleLogger.Warning( $"The Debian release of '{Name} - {Version}' is unknown; vulnerabilities from every release will be reported." );
			}

			CveChecker.CheckCves( this, release_ecosystem, SourceName, SourceVersion );
		}

		/// <summary>
		/// Fetches the release number of each suite in the current archive. The numbered releases come straight from the archive - "13.7" for stable is release
		/// 13 - but testing is unnumbered until it is released, although OSV already files its advisories under the next number, so it is taken as one past stable.
		/// Unstable and experimental never get a number, so they are left out.
		/// </summary>
		/// <returns>A map of suite name to release number, which is empty if the archive could not be reached.</returns>
		private static Dictionary<string, string> GetSuiteReleases()
		{
			Dictionary<string, string> suite_releases = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );

			string content = PackageCheck.Request( "https://api.ftp-master.debian.org/suites" ).Result;
			List<DebSuiteRaw>? suites = JsonHelper.ReadJson<List<DebSuiteRaw>>( content );
			if( suites == null )
			{
				ConsoleLogger.Warning( "Failed to retrieve the Debian suites; the release will be taken from the version where possible." );
				return suite_releases;
			}

			foreach( DebSuiteRaw suite in suites )
			{
				// Point releases ("13.7") and update suites ("13-updates") both start with the release number
				Match match = Regex.Match( suite.version ?? string.Empty, @"^(\d+)" );
				if( match.Success )
				{
					suite_releases[suite.name] = match.Groups[1].Value;
				}
			}

			if( suite_releases.TryGetValue( "stable", out string? stable_release ) && int.TryParse( stable_release, out int stable_number ) )
			{
				suite_releases["testing"] = ( stable_number + 1 ).ToString();
			}

			return suite_releases;
		}

		/// <summary>
		/// Works out which Debian release a version belongs to. A version found in a numbered suite of the current archive is in that release. Otherwise a stable
		/// update or backport names its release in its version, as in "+deb12u5" or "~bpo12+1". An older version that carries neither cannot be placed.
		/// </summary>
		/// <param name="resolvedVersion">The resolved version, along with the suite it was found in.</param>
		/// <returns>The release number, e.g. "13", or "" if it could not be determined.</returns>
		private static string GetDebianRelease( DebResolvedVersion resolvedVersion )
		{
			if( resolvedVersion.Suite.Length > 0 && SuiteReleases.Value.TryGetValue( resolvedVersion.Suite, out string? suite_release ) )
			{
				return suite_release;
			}

			Match match = ReleaseSuffix.Match( resolvedVersion.BinaryVersion );
			if( match.Success )
			{
				return match.Groups[1].Value;
			}

			return string.Empty;
		}

		/// <summary>
		/// Splits a deb822 document - the format of a .dsc, a control file, and a machine-readable copyright file - into paragraphs of fields. A field continues
		/// onto every following line that starts with whitespace, and a continuation line of just "." stands for a blank line. Any OpenPGP signature is discarded.
		/// </summary>
		/// <param name="content">The document text.</param>
		/// <returns>The paragraphs, each a case insensitive map of field name to value, with continuation lines joined by newlines.</returns>
		private static List<Dictionary<string, string>> ParseDeb822( string content )
		{
			List<Dictionary<string, string>> paragraphs = new List<Dictionary<string, string>>();
			Dictionary<string, string> paragraph = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
			string current_field = string.Empty;

			string[] lines = content.Replace( "\r", string.Empty ).Split( '\n' );
			int line_index = 0;

			// A signed document has an armor header, then a blank line, before the content proper
			if( lines.Length > 0 && lines[0].StartsWith( "-----BEGIN PGP SIGNED MESSAGE-----" ) )
			{
				while( line_index < lines.Length && lines[line_index].Length > 0 )
				{
					line_index++;
				}
			}

			for( ; line_index < lines.Length; line_index++ )
			{
				string line = lines[line_index];
				if( line.StartsWith( "-----BEGIN PGP SIGNATURE-----" ) )
				{
					break;
				}

				if( line.StartsWith( '#' ) )
				{
					continue;
				}

				if( line.Trim().Length == 0 )
				{
					if( paragraph.Count > 0 )
					{
						paragraphs.Add( paragraph );
						paragraph = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
					}

					current_field = string.Empty;
					continue;
				}

				if( char.IsWhiteSpace( line[0] ) )
				{
					if( current_field.Length > 0 )
					{
						string continuation = line.Trim();
						if( continuation == "." )
						{
							continuation = string.Empty;
						}

						paragraph[current_field] = paragraph[current_field] + "\n" + continuation;
					}

					continue;
				}

				int colon_index = line.IndexOf( ':' );
				if( colon_index <= 0 )
				{
					continue;
				}

				current_field = line[..colon_index].Trim();
				paragraph[current_field] = line[( colon_index + 1 )..].Trim();
			}

			if( paragraph.Count > 0 )
			{
				paragraphs.Add( paragraph );
			}

			return paragraphs;
		}

		/// <summary>
		/// Splits a Debian license expression into the license short names it contains. Debian writes its operators in lower case, may separate clauses with commas,
		/// and spells an exception "with X exception", so the expression is normalized before the usual SPDX expression parsing.
		/// </summary>
		/// <param name="licenseExpression">The first line of a "License" field, e.g. "GPL-2+ or Artistic, and BSD-3-clause".</param>
		/// <returns>The distinct Debian short names in the expression, e.g. "GPL-2+", "Artistic", and "BSD-3-clause".</returns>
		internal static List<string> ParseLicenseExpression( string licenseExpression )
		{
			string normalized_expression = LicenseException.Replace( licenseExpression, "WITH $1" ).Replace( ',', ' ' );
			return LicenseChecker.ParseLicenseExpression( normalized_expression );
		}

		/// <summary>
		/// Records the licenses that cover the package as a whole from its machine-readable copyright file, along with its copyright holders. Only the "Files: *"
		/// paragraph is used: the others name the licenses of individual files, and taking one of those - an MIT licensed test script, say - as the package's
		/// preferred license would misrepresent it. The licenses are recorded under their Debian short names, e.g. "Expat" or "GPL-2+", which
		/// <see cref="LicenseChecker.FixSpdxCodes"/> converts to SPDX codes when the data is cleaned up.
		/// </summary>
		/// <param name="copyrightContent">The contents of the copyright file.</param>
		/// <param name="copyrightUri">Where the copyright file was downloaded from, which becomes each license's URI.</param>
		internal void AddLicensesFromCopyright( string copyrightContent, string copyrightUri )
		{
			List<Dictionary<string, string>> paragraphs = ParseDeb822( copyrightContent );
			if( paragraphs.Count == 0 || !paragraphs[0].TryGetValue( "Format", out string? format ) || !format.Contains( "copyright-format", StringComparison.OrdinalIgnoreCase ) )
			{
				ConsoleLogger.Warning( "The copyright file is not machine-readable; no licenses can be extracted from it." );
				return;
			}

			// A stand-alone license paragraph has a License field but no Files field, and holds the text of a license that a Files paragraph names
			Dictionary<string, string> license_texts = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
			Dictionary<string, string>? all_files_paragraph = null;
			foreach( Dictionary<string, string> paragraph in paragraphs.Skip( 1 ) )
			{
				if( !paragraph.TryGetValue( "License", out string? license_field ) )
				{
					continue;
				}

				if( paragraph.TryGetValue( "Files", out string? files ) )
				{
					string[] file_patterns = files.Split( [' ', '\n'], StringSplitOptions.RemoveEmptyEntries );
					if( all_files_paragraph == null && file_patterns.Contains( "*" ) )
					{
						all_files_paragraph = paragraph;
					}
				}
				else
				{
					int newline_index = license_field.IndexOf( '\n' );
					if( newline_index > 0 )
					{
						license_texts.TryAdd( license_field[..newline_index].Trim(), license_field[( newline_index + 1 )..] );
					}
				}
			}

			if( all_files_paragraph == null )
			{
				ConsoleLogger.Warning( "The copyright file has no 'Files: *' paragraph; no licenses can be extracted from it." );
				return;
			}

			// The first line of the License field is the expression, and anything after it is the license text inline
			string license_value = all_files_paragraph["License"];
			string license_expression = license_value;
			string inline_text = string.Empty;
			int expression_end = license_value.IndexOf( '\n' );
			if( expression_end >= 0 )
			{
				license_expression = license_value[..expression_end];
				inline_text = license_value[( expression_end + 1 )..];
			}

			foreach( string debian_name in ParseLicenseExpression( license_expression ) )
			{
				LicenseInfo license_info = new LicenseInfo( debian_name )
				{
					HtmlLicenseUri = copyrightUri
				};

				if( license_texts.TryGetValue( debian_name, out string? license_text ) )
				{
					license_info.LicenseContent = license_text;
				}
				else
				{
					license_info.LicenseContent = inline_text;
				}

				AddLicense( license_info );
			}

			if( all_files_paragraph.TryGetValue( "Copyright", out string? copyright ) )
			{
				string[] holders = copyright.Split( '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
				Copyright = PackageCheck.SanitizeString( $"Copyright {string.Join( "; ", holders )}" );
			}
		}

		/// <summary>
		/// Records the build dependencies of the source package. The run time dependencies of a binary package are only published in the archive's Packages
		/// indices and inside the .deb itself, neither of which is reachable through an API, so the source package's declared build dependencies are what is
		/// reported. Only the first of a set of alternatives is recorded, along with any version relation; architecture and build profile restrictions are dropped.
		/// </summary>
		/// <param name="dsc">The fields of the source package's .dsc.</param>
		private void AddBuildDependencies( Dictionary<string, string> dsc )
		{
			List<string> dependency_names = new List<string>();
			foreach( string field in new string[] { "Build-Depends", "Build-Depends-Arch", "Build-Depends-Indep" } )
			{
				if( !dsc.TryGetValue( field, out string? relations ) )
				{
					continue;
				}

				foreach( string relation in relations.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
				{
					string first_alternative = relation.Split( '|' )[0];
					first_alternative = Regex.Replace( first_alternative, @"\[[^\]]*\]|<[^>]*>", string.Empty );

					string requirement = string.Empty;
					Match version_match = Regex.Match( first_alternative, @"\(([^)]*)\)" );
					if( version_match.Success )
					{
						requirement = version_match.Groups[1].Value.Trim();
						first_alternative = first_alternative.Remove( version_match.Index, version_match.Length );
					}

					// An architecture qualifier such as "python3:native" names the same package
					string dependency_name = first_alternative.Split( ':' )[0].Trim();
					if( dependency_name.Length == 0 || dependency_names.Contains( dependency_name ) )
					{
						continue;
					}

					dependency_names.Add( dependency_name );

					PackageDependency package_dependency = new PackageDependency()
					{
						Name = dependency_name,
						Version = requirement,
						DependencyType = EDependencyType.Build
					};

					AddSingleDependency( package_dependency );
				}
			}
		}

		/// <summary>
		/// Fetches the versions of a package in the current Debian archive, by suite. The archive keeps every suite from oldoldstable to experimental, which covers
		/// what is installed on any supported Debian system.
		/// </summary>
		/// <param name="packageName">The binary or source package name.</param>
		/// <returns>A map of suite name to the versions in that suite, or an empty map if the package is not in the archive.</returns>
		private static Dictionary<string, Dictionary<string, DebMadisonEntryRaw>> GetArchiveVersions( string packageName )
		{
			string get_request = $"https://api.ftp-master.debian.org/madison?package={Uri.EscapeDataString( packageName )}&f=json";
			string content = PackageCheck.Request( get_request ).Result;
			if( string.IsNullOrEmpty( content ) )
			{
				return new Dictionary<string, Dictionary<string, DebMadisonEntryRaw>>();
			}

			List<Dictionary<string, Dictionary<string, Dictionary<string, DebMadisonEntryRaw>>>>? response = JsonHelper.ReadJson<List<Dictionary<string, Dictionary<string, Dictionary<string, DebMadisonEntryRaw>>>>>( content );
			if( response == null || response.Count == 0 || !response[0].TryGetValue( packageName, out Dictionary<string, Dictionary<string, DebMadisonEntryRaw>>? suites ) )
			{
				return new Dictionary<string, Dictionary<string, DebMadisonEntryRaw>>();
			}

			return suites;
		}

		/// <summary>
		/// Fetches every version of a package that has ever been in the Debian archive, along with the source that built each one, from snapshot.debian.org. The
		/// name is tried as a binary package first, as that is what a purl names, and then as a source package.
		/// </summary>
		/// <param name="packageName">The binary or source package name.</param>
		/// <returns>Every known version, or an empty list if the package is unknown.</returns>
		private static List<DebResolvedVersion> GetSnapshotVersions( string packageName )
		{
			string get_request = $"https://snapshot.debian.org/mr/binary/{Uri.EscapeDataString( packageName )}/";
			string content = PackageCheck.Request( get_request ).Result;
			DebSnapshotBinaryResponse? binary_response = JsonHelper.ReadJson<DebSnapshotBinaryResponse>( content );
			if( binary_response?.result != null && binary_response.result.Count > 0 )
			{
				return binary_response.result
					.Select( x => new DebResolvedVersion { BinaryVersion = x.binary_version, Source = x.source, SourceVersion = x.version } )
					.ToList();
			}

			ConsoleLogger.Log( $" .. '{packageName}' is not a binary package; trying it as a source package" );
			get_request = $"https://snapshot.debian.org/mr/package/{Uri.EscapeDataString( packageName )}/";
			content = PackageCheck.Request( get_request ).Result;
			DebSnapshotSourceResponse? source_response = JsonHelper.ReadJson<DebSnapshotSourceResponse>( content );
			if( source_response?.result != null )
			{
				return source_response.result
					.Select( x => new DebResolvedVersion { BinaryVersion = x.version, Source = packageName, SourceVersion = x.version } )
					.ToList();
			}

			return [];
		}

		/// <summary>
		/// Picks the version of a package to use. A version in the current archive is preferred - stable first - as that is what a system is most likely to have
		/// installed, and only when none satisfies the requirement is the full history in snapshot.debian.org consulted.
		/// </summary>
		/// <param name="packageName">The binary or source package name.</param>
		/// <param name="requirement">Blank for the version in the preferred suite, an exact version, or a relation such as "&gt;= 1.22.5".</param>
		/// <returns>The version and the source that built it, or null if no version could be found.</returns>
		private static DebResolvedVersion? SelectVersion( string packageName, string requirement )
		{
			Dictionary<string, Dictionary<string, DebMadisonEntryRaw>> suites = GetArchiveVersions( packageName );

			try
			{
				DebRequirement deb_requirement = new DebRequirement( requirement );
				foreach( string suite in PreferredSuites )
				{
					if( !suites.TryGetValue( suite, out Dictionary<string, DebMadisonEntryRaw>? suite_versions ) )
					{
						continue;
					}

					// A suite can briefly hold two versions while a new one migrates in, so the highest satisfying one is taken
					DebVersion? suite_version = suite_versions.Keys
						.Select( x => new DebVersion( x ) )
						.Where( x => deb_requirement.SatisfiedBy( x ) )
						.Max();

					if( suite_version != null )
					{
						DebMadisonEntryRaw entry = suite_versions[suite_version.ToString()];
						ConsoleLogger.Log( $"Package '{packageName}' matches '{requirement}' with version '{suite_version}' from {suite}" );
						return new DebResolvedVersion { BinaryVersion = suite_version.ToString(), Source = entry.source, SourceVersion = entry.source_version, Suite = suite };
					}
				}
			}
			catch( FormatException e )
			{
				ConsoleLogger.Error( $"Invalid version requirement '{requirement}' for '{packageName}' - {e.Message}" );
				return null;
			}

			List<DebResolvedVersion> snapshot_versions = GetSnapshotVersions( packageName );
			if( snapshot_versions.Count == 0 )
			{
				ConsoleLogger.Error( $"Failed to find the '{packageName}' package in the Debian archive" );
				return null;
			}

			DebVersion? matching_version = DebVersionMatcher.SelectMatchingVersion( packageName, snapshot_versions.Select( x => x.BinaryVersion ).Distinct().ToList(), requirement );
			if( matching_version == null )
			{
				return null;
			}

			DebResolvedVersion? resolved_version = snapshot_versions.Find( x => x.BinaryVersion == matching_version.ToString() );
			if( resolved_version != null )
			{
				resolved_version.Suite = FindNumberedSuite( suites, resolved_version.BinaryVersion );
			}

			return resolved_version;
		}

		/// <summary>
		/// Finds a suite of a numbered release that a version outside the preferred suites belongs to: one that still carries it, such as oldstable, or failing that
		/// one that carries a stable update built on top of it, as "8.14.1-2+deb13u5" in stable is of the original "8.14.1-2" release.
		/// </summary>
		/// <param name="suites">The versions of the package in each suite of the current archive.</param>
		/// <param name="version">The binary version.</param>
		/// <returns>The suite name, or "" if no numbered suite has the version or an update to it.</returns>
		private static string FindNumberedSuite( Dictionary<string, Dictionary<string, DebMadisonEntryRaw>> suites, string version )
		{
			List<string> numbered_suites = suites.Keys.Where( x => SuiteReleases.Value.ContainsKey( x ) ).ToList();

			string? suite = numbered_suites.Find( x => suites[x].ContainsKey( version ) );
			if( suite == null )
			{
				suite = numbered_suites.Find( x => suites[x].Keys.Any( y => y.StartsWith( version + "+deb" ) || y.StartsWith( version + "~deb" ) ) );
			}

			return suite ?? string.Empty;
		}

		/// <summary>
		/// Finds the .dsc of a source package version in snapshot.debian.org, which also says where in the archive's pool the package lives and when it was first
		/// uploaded.
		/// </summary>
		/// <param name="source">The source package name.</param>
		/// <param name="sourceVersion">The source package version.</param>
		/// <param name="dscHash">Receives the SHA-1 snapshot.debian.org files the .dsc under.</param>
		/// <returns>The .dsc's file details, or null if it could not be found.</returns>
		private static DebSnapshotFileRaw? GetDscFile( string source, string sourceVersion, out string dscHash )
		{
			dscHash = string.Empty;

			string get_request = $"https://snapshot.debian.org/mr/package/{Uri.EscapeDataString( source )}/{Uri.EscapeDataString( sourceVersion )}/srcfiles?fileinfo=1";
			string content = PackageCheck.Request( get_request ).Result;
			DebSnapshotSourceFilesResponse? response = JsonHelper.ReadJson<DebSnapshotSourceFilesResponse>( content );
			if( response?.fileinfo == null )
			{
				return null;
			}

			foreach( KeyValuePair<string, List<DebSnapshotFileRaw>> file_info in response.fileinfo )
			{
				// The same file is listed once for every archive that carries it; the main archive is preferred as it is the one with the earliest upload
				DebSnapshotFileRaw? dsc_file = file_info.Value.Find( x => x.name.EndsWith( ".dsc" ) && x.archive_name == "debian" )
				                               ?? file_info.Value.Find( x => x.name.EndsWith( ".dsc" ) );
				if( dsc_file != null )
				{
					dscHash = file_info.Key;
					return dsc_file;
				}
			}

			return null;
		}

		/// <summary>
		/// Downloads the copyright file for a source package version from the archive's metadata mirror. The mirror only keeps the versions currently in the
		/// archive, so for an older version the copyright file of the current stable or unstable version is used instead, with a warning.
		/// </summary>
		/// <param name="poolPath">The package's directory in the pool, e.g. "/pool/main/c/curl".</param>
		/// <param name="source">The source package name.</param>
		/// <param name="sourceVersion">The source package version.</param>
		/// <param name="copyrightUri">Receives the URI the copyright file was downloaded from.</param>
		/// <returns>The copyright file contents, or "" if none could be found.</returns>
		private static string GetCopyright( string poolPath, string source, string sourceVersion, out string copyrightUri )
		{
			string directory = "https://metadata.ftp-master.debian.org/changelogs" + poolPath.Replace( "/pool", string.Empty );

			// The epoch is not part of any file name in the archive
			string version_without_epoch = sourceVersion[( sourceVersion.IndexOf( ':' ) + 1 )..];
			copyrightUri = $"{directory}/{Uri.EscapeDataString( $"{source}_{version_without_epoch}_copyright" )}";
			string content = PackageCheck.Request( copyrightUri ).Result;
			if( !string.IsNullOrEmpty( content ) )
			{
				return content;
			}

			foreach( string suite in new string[] { "stable", "unstable" } )
			{
				copyrightUri = $"{directory}/{suite}_copyright";
				content = PackageCheck.Request( copyrightUri ).Result;
				if( !string.IsNullOrEmpty( content ) )
				{
					ConsoleLogger.Warning( $"No copyright file for version '{sourceVersion}' of '{source}'; using the one from {suite}." );
					return content;
				}
			}

			copyrightUri = string.Empty;
			ConsoleLogger.Warning( $"No copyright file found for version '{sourceVersion}' of '{source}'." );
			return string.Empty;
		}

		/// <summary>
		/// Strips the email addresses from a list of maintainers, e.g. "Debian Curl Maintainers &lt;team+curl@tracker.debian.org&gt;" to "Debian Curl Maintainers".
		/// </summary>
		private static string GetMaintainerNames( string maintainers )
		{
			string names = Regex.Replace( maintainers, @"<[^>]*>", string.Empty );
			return string.Join( ", ", names.Split( [',', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) );
		}

		/// <summary>
		/// Looks up a Debian package's metadata: resolves the version to use from <paramref name="requirement"/> and the source package that built it, then reads
		/// that source package's .dsc for its maintainers, homepage, and repository, and its copyright file for its licenses and copyright holders.
		/// </summary>
		/// <param name="packageName">The binary package name, e.g. "libcurl4t64", or a source package name such as "curl".</param>
		/// <param name="requirement">Blank to use the version in stable (or failing that testing, then unstable); an exact version such as "8.14.1-2"; or a relation such as "&gt;= 8.0".</param>
		/// <param name="isRootPackage">True if this is the package being checked, rather than one of its dependencies, so its dependencies are collected.</param>
		/// <returns>The populated package, or null if the package, a version satisfying the requirement, or its source package could not be found.</returns>
		internal static DebPackage? GetDebPackage( string packageName, string requirement, bool isRootPackage )
		{
			DebResolvedVersion? resolved_version = SelectVersion( packageName, requirement );
			if( resolved_version == null )
			{
				return null;
			}

			DebSnapshotFileRaw? dsc_file = GetDscFile( resolved_version.Source, resolved_version.SourceVersion, out string dsc_hash );
			if( dsc_file == null )
			{
				ConsoleLogger.Error( $"Failed to find the source of version '{resolved_version.SourceVersion}' of '{resolved_version.Source}' in snapshot.debian.org" );
				return null;
			}

			string dsc_content = PackageCheck.Request( $"https://snapshot.debian.org/file/{dsc_hash}" ).Result;
			List<Dictionary<string, string>> dsc_paragraphs = ParseDeb822( dsc_content );
			Dictionary<string, string> dsc = dsc_paragraphs.Count > 0 ? dsc_paragraphs[0] : new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
			if( dsc.Count == 0 )
			{
				ConsoleLogger.Warning( $"Failed to read the .dsc of version '{resolved_version.SourceVersion}' of '{resolved_version.Source}'" );
			}

			DateTime.TryParseExact( dsc_file.first_seen, "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime first_seen );

			string maintainers = dsc.GetValueOrDefault( "Maintainer", string.Empty );
			if( dsc.TryGetValue( "Uploaders", out string? uploaders ) )
			{
				maintainers += ", " + uploaders;
			}

			string source_code_uri = dsc.GetValueOrDefault( "Vcs-Browser", string.Empty );
			if( source_code_uri.Length == 0 )
			{
				source_code_uri = dsc.GetValueOrDefault( "Vcs-Git", string.Empty );
			}

			DebPackage deb_package = new DebPackage
			{
				Name = packageName,
				Version = resolved_version.BinaryVersion,
				SourceName = resolved_version.Source,
				SourceVersion = resolved_version.SourceVersion,
				DebianRelease = GetDebianRelease( resolved_version ),
				VersionCreatedAt = first_seen,
				Authors = GetMaintainerNames( maintainers ),
				Copyright = string.Empty,
				HomepageUri = dsc.GetValueOrDefault( "Homepage", $"https://tracker.debian.org/pkg/{resolved_version.Source}" ),
				SourceCodeUri = source_code_uri,
				DownloadUri = $"https://snapshot.debian.org/package/{Uri.EscapeDataString( resolved_version.Source )}/{Uri.EscapeDataString( resolved_version.SourceVersion )}/",
				// The description belongs to the binary package, and is only published in the archive's Packages indices, which have no API
				Description = string.Empty
			};

			string copyright_content = GetCopyright( dsc_file.path, resolved_version.Source, resolved_version.SourceVersion, out string copyright_uri );
			if( copyright_content.Length > 0 )
			{
				deb_package.AddLicensesFromCopyright( copyright_content, copyright_uri );
			}

			if( isRootPackage )
			{
				deb_package.AddBuildDependencies( dsc );
			}

			return deb_package;
		}

		protected override void UpdateDependency( PackageDependency dependency )
		{
			DebPackage? deb_package = GetDebPackage( dependency.Name, dependency.Version, false );
			if( deb_package != null )
			{
				deb_package.CleanupDependencyData();
				deb_package.SetDependencyLicense( dependency );
			}
		}

		/// <summary>
		/// Looks up the given Debian package, cleans up its data (licenses, copyright, fallback URIs), and prints its details to the console. Does nothing if the
		/// package or the requested version could not be found.
		/// </summary>
		/// <param name="packageName">The binary package name, e.g. "libcurl4t64", or a source package name such as "curl".</param>
		/// <param name="version">The specific version to check, or "" to use the version in stable.</param>
		public static void CheckDebPackage( string packageName, string version )
		{
			DebPackage? deb_package = GetDebPackage( packageName, version, true );
			if( deb_package != null )
			{
				deb_package.CleanupData();
				deb_package.PrintDetails( PackageType );
			}
		}
	}
}
