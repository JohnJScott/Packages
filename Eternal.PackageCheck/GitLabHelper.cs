// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Eternal.PackageCheck
{
	internal class GitLabLicenseDetail
	{
		public string key { get; set; } = string.Empty;
		public string name { get; set; } = string.Empty;
		public string? nickname { get; set; }
		public string html_url { get; set; } = string.Empty;
		public string source_url { get; set; } = string.Empty;
	}

	internal class GitLabLicense
	{
		public long id { get; set; }
		public string name { get; set; } = string.Empty;
		public string? license_url { get; set; }
		public GitLabLicenseDetail? license { get; set; }
	}

	internal class GitLabFile
	{
		public string name { get; set; } = string.Empty;
		public string path { get; set; } = string.Empty;

		// GitLab values: "blob" (file) or "tree" (directory) - not GitHub's "file"/"dir".
		public string type { get; set; } = string.Empty;

		public string download_url { get; set; } = string.Empty;
		public string url { get; set; } = string.Empty;
	}

	internal class GitLabHelper
	{
		private static readonly HttpClient HttpClientInstance = new HttpClient();

		static GitLabHelper()
		{
			// User-Agent (required by GitLab's API, same as GitHub's)
			HttpClientInstance.DefaultRequestHeaders.UserAgent.Add( new ProductInfoHeaderValue( "Eternal.PackageCheck", "1.0" ) );

			// PAT goes in the Authorization header. GitLab accepts personal
			// access tokens as OAuth-style bearer tokens, not just PRIVATE-TOKEN.
			string? pat = Environment.GetEnvironmentVariable( "GITLAB_PAT" );
			if( !string.IsNullOrEmpty( pat ) )
			{
				HttpClientInstance.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Bearer", pat );
			}
		}

		/// <summary>
		/// Converts a GitLab human-viewable blob URL into the equivalent raw-content download URL, e.g. ".../-/blob/{ref}/{path}" becomes ".../-/raw/{ref}/{path}".
		/// </summary>
		/// <param name="htmlUrl">The gitlab.com blob URL.</param>
		/// <returns>The equivalent raw-content download URL.</returns>
		public static string ConvertFromHtmlUrl( string htmlUrl )
		{
			// html_url( blob ):  https://gitlab.com/{namespace}/{project}/-/blob/{ref}/{path}
			// download_url( raw ): https://gitlab.com/{namespace}/{project}/-/raw/{ref}/{path}
			return htmlUrl.Replace( "/-/blob/", "/-/raw/" );
		}

		/// <summary>
		/// Queries GitLab's project API with "?license=true", which returns GitLab's own license detection for the repository, if it found one.
		/// </summary>
		/// <param name="owner">The namespace (user or group) the project belongs to.</param>
		/// <param name="repo">The project name.</param>
		/// <returns>The detected license details, or null if none was found or the request failed.</returns>
		public static async Task<GitLabLicense?> GetRepositoryLicenseData( string owner, string repo )
		{
			GitLabLicense? license_data;
			string request_url = $"https://gitlab.com/api/v4/projects/{Uri.EscapeDataString( $"{owner}/{repo}" )}?license=true";

			try
			{
				HttpResponseMessage response = await HttpClientInstance.GetAsync( request_url );
				response.EnsureSuccessStatusCode();

				string json = await response.Content.ReadAsStringAsync();
				license_data = JsonSerializer.Deserialize<GitLabLicense>( json );
			}
			catch( Exception ex )
			{
				ConsoleLogger.Error( $"GitLab query failed with message '{ex.Message}'" );
				license_data = null;
			}

			return license_data;
		}

		/// <summary>
		/// Lists the immediate contents the root directory in a GitLab repository, mirroring GitHubHelper.GetRepositoryFilesAsync's.
		/// GitLab's tree API paginates (unlike GitHub's "contents" endpoint, which returns a whole directory in one response),
		/// so every page is walked via the "X-Next-Page" response header.
		/// </summary>
		/// <param name="owner">The repository owner/organization.</param>
		/// <param name="repo">The repository name.</param>
		/// <returns>The files and directories at the repository root, or null if the request failed.</returns>
		public static async Task<List<GitLabFile>> GetRepositoryFilesAsync( string owner, string repo )
		{
			string tree_url = $"https://gitlab.com/api/v4/projects/{Uri.EscapeDataString( $"{owner}/{repo}" )}/repository/tree";

			List<GitLabFile> files = new List<GitLabFile>();
			int page = 1;

			try
			{
				while( true )
				{
					string request_url = $"{tree_url}?per_page=100&$page={page}";

					HttpResponseMessage response = await HttpClientInstance.GetAsync( request_url );
					response.EnsureSuccessStatusCode();

					string json = await response.Content.ReadAsStringAsync();
					List<GitLabFile>? page_files = JsonSerializer.Deserialize<List<GitLabFile>>( json );
					if( page_files == null || page_files.Count == 0 )
					{
						break;
					}

					foreach( GitLabFile file in page_files )
					{
						// Only files ("blob") have downloadable content; directories ("tree") are left without a download_url, matching GitHub's
						// contents API returning download_url: null for directories.
						if( file.type == "blob" )
						{
							string encoded_file_path = Uri.EscapeDataString( file.path );
							file.url = tree_url.Replace( "/repository/tree", $"/repository/files/{encoded_file_path}" );
							file.download_url = $"{file.url}/raw";
						}

						files.Add( file );
					}

					// GitLab returns an empty "X-Next-Page" header once the last page is reached.
					string next_page = response.Headers.TryGetValues( "X-Next-Page", out IEnumerable<string>? values ) ? values.FirstOrDefault() ?? string.Empty : string.Empty;
					if( string.IsNullOrEmpty( next_page ) )
					{
						break;
					}

					page = int.Parse( next_page );
				}
			}
			catch( HttpRequestException ex )
			{
				ConsoleLogger.Error( $"GitLab query failed with message '{ex.Message}'" );
				files = new List<GitLabFile>();
			}

			return files;
		}
	}
}
