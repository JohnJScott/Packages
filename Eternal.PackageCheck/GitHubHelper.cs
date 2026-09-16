// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Eternal.PackageCheck
{
	internal class GitHubLicense
	{
		public string name { get; set; } = string.Empty;
		public string path { get; set; } = string.Empty;
		public long size { get; set; }
		public string url { get; set; } = string.Empty;
		public string html_url { get; set; } = string.Empty;
		public string git_url { get; set; } = string.Empty;
		public string download_url { get; set; } = string.Empty;
		public string type { get; set; } = string.Empty;

		public GitHubLicenseDetail? license { get; set; }
	}

	internal class GitHubLicenseDetail
	{
		public string key { get; set; } = string.Empty;
		public string name { get; set; } = string.Empty;
		public string spdx_id { get; set; } = string.Empty;
		public string url { get; set; } = string.Empty;
		public string node_id { get; set; } = string.Empty;
	}

	internal class GitHubFile
	{
		public string name { get; set; } = string.Empty;
		public string path { get; set; } = string.Empty;
		public string type { get; set; } = string.Empty;
		public long size { get; set; }
		public string download_url { get; set; } = string.Empty;
		public string url { get; set; } = string.Empty;
	}

	internal class GitHubHelper
	{
		private static readonly HttpClient HttpClientInstance = new HttpClient();

		static GitHubHelper()
		{
			// User-Agent (required by GitHub API)
			HttpClientInstance.DefaultRequestHeaders.UserAgent.Add( new ProductInfoHeaderValue( "Eternal.PackageCheck", "1.0" ) );

			// PAT goes in the Authorization header
			string? pat = Environment.GetEnvironmentVariable( "GITHUB_PAT" );
			if( !string.IsNullOrEmpty( pat ) )
			{
				HttpClientInstance.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Bearer", pat );
			}
		}

		/// <summary>
		/// Converts a GitHub raw-content download URL into the equivalent human-viewable blob URL, e.g. "https://raw.githubusercontent.com/{owner}/{repo}/{ref}/{path}"
		/// becomes "https://github.com/{owner}/{repo}/blob/{ref}/{path}".
		/// </summary>
		/// <param name="downloadUrl">The raw.githubusercontent.com download URL.</param>
		/// <param name="owner">The repository owner/organization.</param>
		/// <param name="repo">The repository name.</param>
		/// <returns>The equivalent github.com blob URL.</returns>
		public static string ConvertFromDownloadUrl( string downloadUrl, string owner, string repo )
		{
			// download_url: https://raw.githubusercontent.com/{owner}/{repo}/{ref}/{path}
			// html_url: https://github.com/{owner}/{repo}/blob/{ref}/{path}
			string result = downloadUrl.Replace( "raw.githubusercontent.com", "github.com" );
			return result.Replace( $"{owner}/{repo}/", $"{owner}/{repo}/blob/" );
		}

		/// <summary>
		/// Queries GitHub's dedicated license-detection endpoint ("/repos/{owner}/{repo}/license"), which GitHub itself has already matched
		/// against a known SPDX license, if it found one.
		/// </summary>
		/// <param name="owner">The repository owner/organization.</param>
		/// <param name="repo">The repository name.</param>
		/// <returns>The detected license file and its SPDX details, or null if none was found or the request failed.</returns>
		public static async Task<GitHubLicense?> GetRepositoryLicenseData( string owner, string repo )
		{
			GitHubLicense? license_data;
			try
			{
				string request_url = $"https://api.github.com/repos/{owner}/{repo}/license";

				HttpResponseMessage response = await HttpClientInstance.GetAsync( request_url );
				response.EnsureSuccessStatusCode();

				string json = await response.Content.ReadAsStringAsync();
				license_data = JsonSerializer.Deserialize<GitHubLicense>( json );
			}
			catch( HttpRequestException ex )
			{
				ConsoleLogger.Error( $"GitHub query failed with message '{ex.Message}'" );
				license_data = null;
			}

			return license_data;
		}

		/// <summary>
		/// Lists the immediate contents of a GitHub repository's root directory via the "contents" API, which returns a whole directory listing in a single response
		/// (unlike GitLab's paginated tree API).
		/// </summary>
		/// <param name="owner">The repository owner/organization.</param>
		/// <param name="repo">The repository name.</param>
		/// <returns>The files and directories at the repository root, or null if the request failed.</returns>
		public static async Task<List<GitHubFile>?> GetRepositoryFilesAsync( string owner, string repo )
		{
			List<GitHubFile>? files;
			try
			{
				string request_url = $"https://api.github.com/repos/{owner}/{repo}/contents/";

				HttpResponseMessage response = await HttpClientInstance.GetAsync( request_url );
				response.EnsureSuccessStatusCode();

				string json = await response.Content.ReadAsStringAsync();
				files = JsonSerializer.Deserialize<List<GitHubFile>>( json );
			}
			catch( HttpRequestException ex )
			{
				ConsoleLogger.Error( $"GitHub query failed with message '{ex.Message}'" );
				files = null;
			}

			return files;
		}
	}
}
