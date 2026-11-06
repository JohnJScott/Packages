// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Eternal.PackageCheck
{
	internal static class LicenseChecker
	{
		private static readonly Dictionary<string, int>? LicensePriorities;

		static LicenseChecker()
		{
			string licenses_path = Path.Combine( AppContext.BaseDirectory, "LicensePriorities.json" );
			LicensePriorities = JsonHelper.ReadJsonFile<Dictionary<string, int>>( licenses_path );
			if( LicensePriorities == null || LicensePriorities.Count == 0 )
			{
				ConsoleLogger.Error( $"Failed to load LicensePriorities.json!" );
				LicensePriorities = null;
			}
		}

		private static readonly List<string> PotentialLicenseNames =
		[
			"license",
			"licence",
			"copying",
			"copyright",
			"bsdl"
		];

		private static bool IsPotentialLicenseFile( string fileName )
		{
			foreach( string license_name in PotentialLicenseNames )
			{
				if( fileName.Contains( license_name, StringComparison.OrdinalIgnoreCase ) )
				{
					return true;
				}
			}

			return false;
		}

		private static bool LicenseUrisAreSet( List<LicenseInfo> licenses )
		{
			if( licenses.Count == 0 )
			{
				return false;
			}

			bool license_uri_set = true;
			foreach( LicenseInfo license_info in licenses )
			{
				license_uri_set &= !string.IsNullOrEmpty( license_info.HtmlLicenseUri );
			}

			return license_uri_set;
		}

		private static ( string owner, string repo ) GetRepoSegments( string sourceCodeUri )
		{
			( string owner, string repo ) result = ( string.Empty, string.Empty );

			bool is_git_repo = sourceCodeUri.Contains( ".git", StringComparison.OrdinalIgnoreCase );
			if( is_git_repo )
			{
				// "git://gitlab.com/gitlab-org/gitlab-foss.git"
				// "git+https://gitlab.com/gitlab-org/gitaly.git#main"
				// "git@gitlab.com:gitlab-org/gitlab-runner.git"
				// "https://gitlab.com/cznic/fileutil.git"
				string[] git_prefixes =
				[
					"git://",
					"https://",
					"http://",
					"git+ssh://",
					"git+https://",
					"git+http://",
					"git@"
				];

				bool has_valid_prefix = git_prefixes.Any( prefix => sourceCodeUri.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) );
				if( !has_valid_prefix )
				{
					ConsoleLogger.Warning( $"git repo detected without an expected prefix: '{sourceCodeUri}'" );
				}

				int index = sourceCodeUri.IndexOf( ".com", StringComparison.OrdinalIgnoreCase );
				if( index != -1 )
				{
					string repo_path = sourceCodeUri[( index + 5 )..];
					index = repo_path.IndexOf( '#' );
					if( index != -1 )
					{
						repo_path = repo_path[..index];
					}

					if( repo_path.EndsWith( ".git" ) )
					{
						repo_path = repo_path[..^4];
					}

					string[] repo_segments = repo_path.Split( "/", StringSplitOptions.RemoveEmptyEntries );
					if( repo_segments.Length > 1 )
					{
						result.owner = repo_segments[0];
						result.repo = repo_segments[1];
					}
				}
			}
			else
			{
				// "https://gitlab.com/gitlab-org/gitlab-foss/-/tree/master/doc"
				string[] web_prefixes =
				[
					"https://",
					"http://"
				];

				bool has_valid_prefix = web_prefixes.Any( prefix => sourceCodeUri.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) );
				if( !has_valid_prefix )
				{
					ConsoleLogger.Warning( $"Web repo detected without an expected prefix: '{sourceCodeUri}'" );
				}

				int index = sourceCodeUri.IndexOf( ".com", StringComparison.OrdinalIgnoreCase );
				if( index != -1 && ( index + 5 < sourceCodeUri.Length ) )
				{
					string repo_path = sourceCodeUri[( index + 5 )..];
					string[] repo_segments = repo_path.Split( "/", StringSplitOptions.RemoveEmptyEntries );
					if( repo_segments.Length > 1 )
					{
						result.owner = repo_segments[0];
						result.repo = repo_segments[1];
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Splits an SPDX license expression into its individual license codes, discarding the operators. "AND" and "OR" are dropped, and the id following a "WITH"
		/// is dropped too because it names an SPDX exception rather than a license.
		/// </summary>
		/// <param name="licenseExpression">The expression, e.g. "Apache-2.0 OR BSD-3-Clause" or "GPL-2.0-only WITH Classpath-exception-2.0".</param>
		/// <returns>The distinct SPDX license codes found in the expression.</returns>
		internal static List<string> ParseLicenseExpression( string licenseExpression )
		{
			List<string> spdx_codes = new List<string>();

			string[] tokens = licenseExpression.Split( [' ', '\t', '(', ')'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
			for( int token_index = 0; token_index < tokens.Length; token_index++ )
			{
				string token = tokens[token_index];
				if( string.Equals( token, "AND", StringComparison.OrdinalIgnoreCase ) || string.Equals( token, "OR", StringComparison.OrdinalIgnoreCase ) )
				{
					continue;
				}

				if( string.Equals( token, "WITH", StringComparison.OrdinalIgnoreCase ) )
				{
					// Skip the exception id that always follows a WITH.
					token_index++;
					continue;
				}

				if( !spdx_codes.Contains( token, StringComparer.OrdinalIgnoreCase ) )
				{
					spdx_codes.Add( token );
				}
			}

			return spdx_codes;
		}

		/// <summary>
		/// Matches a GNU license name that is not a current SPDX code, capturing the family, the version, and any "or later" plus: a Debian short name such as "GPL-2",
		/// "LGPL-2.1+", or "GFDL-1.3+", or a deprecated SPDX code such as "GPL-2.0" or "GPL-2.0+".
		/// </summary>
		private static readonly Regex GnuLicenseName = new Regex( @"^(A?GPL|LGPL|GFDL)-(\d+(?:\.\d+)?)(\+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled );

		/// <summary>
		/// Converts any misnamed licenses to the appropriate SPDX code. Debian copyright files name licenses with their own short names, which mostly but not
		/// entirely agree with SPDX - see https://www.debian.org/doc/packaging-manuals/copyright-format/1.0/#license-short-name - so those that differ are converted
		/// here too.
		/// </summary>
		/// <param name="licenses">The licenses to fix, updated in place.</param>
		public static void FixSpdxCodes( List<LicenseInfo> licenses )
		{
			foreach( LicenseInfo license_info in licenses )
			{
				switch( license_info.SpdxCode )
				{
					case "Apache 2":
					case "Apache-2":
						license_info.SpdxCode = "Apache-2.0";
						break;

					case "Ruby":
						license_info.SpdxCode = "BSD-2-Clause";
						break;

					case "Expat":
						license_info.SpdxCode = "MIT";
						break;

					case "BSD-2-clause":
						license_info.SpdxCode = "BSD-2-Clause";
						break;

					case "BSD-3-clause":
						license_info.SpdxCode = "BSD-3-Clause";
						break;

					case "BSD-4-clause":
						license_info.SpdxCode = "BSD-4-Clause";
						break;

					case "Artistic":
						license_info.SpdxCode = "Artistic-1.0";
						break;

					case "CC0":
						license_info.SpdxCode = "CC0-1.0";
						break;

					case "PSF-2":
						license_info.SpdxCode = "PSF-2.0";
						break;

					default:
						Match match = GnuLicenseName.Match( license_info.SpdxCode );
						if( match.Success )
						{
							// SPDX always spells out the minor version, and makes the "only" explicit
							string family = match.Groups[1].Value.ToUpperInvariant();
							string version = match.Groups[2].Value;
							if( !version.Contains( '.' ) )
							{
								version += ".0";
							}

							string suffix = match.Groups[3].Success ? "or-later" : "only";
							license_info.SpdxCode = $"{family}-{version}-{suffix}";
						}

						break;
				}
			}
		}

		/// <summary>
		/// Cross-references each license's SPDX code against the permissiveness table loaded from "licenses.json" (next to the running assembly), marking each as
		/// permissive or not and flagging the single most permissive license as preferred. A license whose SPDX code isn't found in the table is left unmarked (neither
		/// permissive nor SPDX-recognized) with a warning logged.
		/// </summary>
		/// <param name="licenses">The licenses to validate, updated in place.</param>
		public static void ValidateLicenses( List<LicenseInfo> licenses )
		{
			if( LicensePriorities == null )
			{
				return;
			}

			int best_license = 1000;
			int best_license_index = -1;
			for( int license_index = 0; license_index < licenses.Count; license_index++ )
			{
				LicenseInfo license_info = licenses[license_index];
				if( !LicensePriorities.TryGetValue( license_info.SpdxCode, out int permissiveness ) )
				{
					ConsoleLogger.Warning( $"License '{license_info.SpdxCode}' is not a valid SPDX code" );
				}
				else
				{
					license_info.IsSpdxCode = true;
					license_info.IsPermissive = ( permissiveness < 100 );

					if( permissiveness < best_license )
					{
						best_license = permissiveness;
						best_license_index = license_index;
					}
				}
			}

			if( best_license_index < 0 )
			{
				ConsoleLogger.Warning( "No valid license found." );
			}
			else
			{
				LicenseInfo best_license_info = licenses[best_license_index];
				best_license_info.IsPreferred = true;
				if( !best_license_info.IsPermissive )
				{
					ConsoleLogger.Warning( $"Best found license ({best_license_info.SpdxCode}) is not permissive" );
				}
			}
		}

		private static void ScrapeGitHubForLicenses( string sourceCodeUri, List<LicenseInfo> licenses )
		{
			string? pat = Environment.GetEnvironmentVariable( "GITHUB_PAT" );
			if( string.IsNullOrEmpty( pat ) )
			{
				ConsoleLogger.Warning( "Can not scrape GitHub repo for licenses without a Personal Access Token" );
				ConsoleLogger.Warning( "Set your environment variable 'GITHUB_PAT' to the token received from GitHub.com" );
				return;
			}

			( string owner, string repo ) = GetRepoSegments( sourceCodeUri );
			if( string.IsNullOrEmpty( owner ) || string.IsNullOrEmpty( repo ) )
			{
				ConsoleLogger.Error( $"Failed to extract owner and repo from '{sourceCodeUri}'" );
				return;
			}

			// Use explicit API call
			GitHubLicense? license_data = GitHubHelper.GetRepositoryLicenseData( owner, repo ).Result;
			if( license_data != null )
			{
				ConsoleLogger.Log( $" .... found potential license file '{license_data.name}' found" );
				foreach( LicenseInfo license_info in licenses )
				{
					if( license_info.SpdxCode == license_data.license?.spdx_id )
					{
						ConsoleLogger.Log( $" .. found license file with matching SPDX code from GitHub API -> using '{license_data.html_url}'" );
						license_info.HtmlLicenseUri = license_data.html_url;
						license_info.LicenseContent = PackageCheck.Request( license_data.download_url ).Result;
					}
				}
			}

			// Iterate over files
			List<GitHubFile>? github_files = GitHubHelper.GetRepositoryFilesAsync( owner, repo ).Result;
			if( github_files == null )
			{
				return;
			}

			foreach( GitHubFile github_file in github_files )
			{
				if( !string.IsNullOrEmpty( github_file.download_url ) )
				{
					if( IsPotentialLicenseFile( github_file.name ) )
					{
						ConsoleLogger.Log( $" .... found potential license file '{github_file.name}'" );
						string license_file_content = PackageCheck.Request( github_file.download_url ).Result;
						if( string.IsNullOrEmpty( license_file_content ) )
						{
							ConsoleLogger.Warning( $"Failed to download '{github_file.download_url}'" );
							continue;
						}

						string html_url = GitHubHelper.ConvertFromDownloadUrl( github_file.download_url, owner, repo );
						LicenseIdentifier.IdentifyLicense( licenses, html_url, license_file_content );
					}
				}
			}
		}

		private static void ScrapeGitLabForLicenses( string sourceCodeUri, List<LicenseInfo> licenses )
		{
			string? pat = Environment.GetEnvironmentVariable( "GITLAB_PAT" );
			if( string.IsNullOrEmpty( pat ) )
			{
				ConsoleLogger.Warning( "Can not scrape GitLab repo for licenses without a Personal Access Token" );
				ConsoleLogger.Warning( "Set your environment variable 'GITLAB_PAT' to the token received from GitLab.com" );
				return;
			}

			( string owner, string repo ) = GetRepoSegments( sourceCodeUri );
			if( string.IsNullOrEmpty( owner ) || string.IsNullOrEmpty( repo ) )
			{
				ConsoleLogger.Error( $"Failed to extract owner and repo from '{sourceCodeUri}'" );
				return;
			}

			// Use explicit API call
			GitLabLicense? license_data = GitLabHelper.GetRepositoryLicenseData( owner, repo ).Result;
			if( license_data != null && license_data.license != null && license_data.license_url != null )
			{
				ConsoleLogger.Log( $" .... found potential license file '{Path.GetFileName( license_data.license_url )}' found" );
				foreach( LicenseInfo license_info in licenses )
				{
					if( license_info.SpdxCode.ToLower() == license_data.license?.key )
					{
						ConsoleLogger.Log( $"Found license file with matching SPDX code from GitLab API -> using '{license_data.license_url}'" );
						license_info.HtmlLicenseUri = license_data.license_url;
						license_info.LicenseContent = PackageCheck.Request( GitLabHelper.ConvertFromHtmlUrl( license_data.license_url ) ).Result;
					}
				}
			}

			// Iterate over files
			// No specific ref known here - falls back to the project's default branch server-side.
			List<GitLabFile> gitlab_files = GitLabHelper.GetRepositoryFilesAsync( owner, repo ).Result;
			foreach( GitLabFile gitlab_file in gitlab_files )
			{
				if( !string.IsNullOrEmpty( gitlab_file.download_url ) )
				{
					if( IsPotentialLicenseFile( gitlab_file.name ) )
					{
						ConsoleLogger.Log( $" .... found potential license file '{gitlab_file.name}'" );
						string license_file_content = PackageCheck.Request( gitlab_file.download_url ).Result;
						if( string.IsNullOrEmpty( license_file_content ) )
						{
							ConsoleLogger.Warning( $"Failed to download '{gitlab_file.download_url}'" );
							continue;
						}

						// FIXME
						string html_url = gitlab_file.download_url;
						LicenseIdentifier.IdentifyLicense( licenses, html_url, license_file_content );
					}
				}
			}
		}

		private static void ScrapeGoogleSourceForLicenses( string sourceCodeUri, List<LicenseInfo> licenses, string packageName, string version )
		{
			MemoryStream? zip_stream = GoogleSourceHelper.GetPackageZipAsync( packageName, version ).Result;
			if( zip_stream == null )
			{
				ConsoleLogger.Error( $"Failed to download source code archive '{packageName}/{version}'" );
				return;
			}

			using ZipArchive archive = new ZipArchive( zip_stream, ZipArchiveMode.Read );
			foreach( ZipArchiveEntry entry in archive.Entries )
			{
				if( IsPotentialLicenseFile( entry.Name ) )
				{
					ConsoleLogger.Log( $" .... found potential license file '{entry.Name}'" );
					using Stream entry_stream = entry.Open();
					using StreamReader reader = new StreamReader( entry_stream );
					string license_file_content = reader.ReadToEnd();

					string html_url = GoogleSourceHelper.ConvertToSourceLink( sourceCodeUri, entry.FullName );
					LicenseIdentifier.IdentifyLicense( licenses, html_url, license_file_content );
				}
			}
		}

		/// <summary>
		/// If any license is still missing a URI, scrapes the package's source repository for a license file, dispatching to the GitHub, GitLab, or Google Source scraper
		/// based on what <paramref name="sourceCodeUri"/> points to. Does nothing if every license already has a URI, or if there's no source code URI to scrape.
		/// </summary>
		/// <param name="sourceCodeUri">The package's source repository URL.</param>
		/// <param name="licenses">The licenses to fill in URIs and content for, updated in place.</param>
		/// <param name="packageName">The package name, needed for the Google Source (Go module proxy) scraper.</param>
		/// <param name="version">The package version, needed for the Google Source (Go module proxy) scraper.</param>
		public static void ScrapeRepoForLicenses( string sourceCodeUri, List<LicenseInfo> licenses, string packageName, string version )
		{
			// License links are good
			if( LicenseUrisAreSet( licenses ) )
			{
				return;
			}

			ConsoleLogger.Log( $" .. scraping the repo at '{sourceCodeUri}' (check ReadMe.md for methodology)" );

			// No source code uri - nowhere to scrape from.
			if( string.IsNullOrEmpty( sourceCodeUri ) )
			{
				ConsoleLogger.Error( $"Can not scrape repo for license if there is no source code uri" );
				return;
			}

			if( sourceCodeUri.Contains( "github", StringComparison.OrdinalIgnoreCase ) )
			{
				ScrapeGitHubForLicenses( sourceCodeUri, licenses );
			}
			else if( sourceCodeUri.Contains( "gitlab", StringComparison.OrdinalIgnoreCase ) )
			{
				ScrapeGitLabForLicenses( sourceCodeUri, licenses );
			}
			else if( sourceCodeUri.Contains( "googlesource", StringComparison.OrdinalIgnoreCase ) )
			{
				ScrapeGoogleSourceForLicenses( sourceCodeUri, licenses, packageName, version );
			}
			else
			{
				ConsoleLogger.Error( $"Source code URI does not contain 'GitHub', 'GitLab', or 'GoogleSource'" );
			}
		}
	}
}
