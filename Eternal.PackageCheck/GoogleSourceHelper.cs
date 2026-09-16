// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;
using System.Text;

namespace Eternal.PackageCheck
{
	internal class GoogleSourceHelper
	{
		private static readonly HttpClient HttpClientInstance = new HttpClient();

		/// <summary>
		/// Converts a Go module proxy zip entry name into a viewable googlesource.com link,
		/// e.g. sourceCodeUri "https://go.googlesource.com/sys" and name
		/// "golang.org/x/sys@v0.47.0/LICENSE" become
		/// "https://go.googlesource.com/sys/+/refs/tags/v0.47.0/LICENSE".
		/// If the entry name has no "@version" segment, it's returned unchanged.
		/// </summary>
		/// <param name="sourceCodeUri">The module's googlesource.com repository URL.</param>
		/// <param name="name">The zip entry name, in "{module}@{version}/{path}" form.</param>
		/// <returns>The equivalent googlesource.com source link, or <paramref name="name"/> unchanged if it has no "@version" segment.</returns>
		public static string ConvertToSourceLink( string sourceCodeUri, string name )
		{
			// sourceCodeUri: "https://go.googlesource.com/sys"
			// name: "golang.org/x/sys@v0.47.0/LICENSE"
			// Dest: "https://go.googlesource.com/sys/+/refs/tags/v0.47.0/LICENSE"

			string result = name;
			string[] parts = name.Split( '@' );
			if( parts.Length == 2 )
			{
				result = sourceCodeUri + "/+/refs/tags/" + parts[1];
			}

			return result;
		}

		private static string CanonizeString( string uri )
		{
			StringBuilder encoded = new StringBuilder( uri.Length );
			foreach( char character in uri )
			{
				if( char.IsUpper( character ) )
				{
					encoded.Append( '!' );
					encoded.Append( char.ToLowerInvariant( character ) );
				}
				else
				{
					encoded.Append( character );
				}
			}

			return encoded.ToString();
		}

		/// <summary>
		/// Downloads a Go module's source archive from the Go module proxy (proxy.golang.org), applying the proxy's uppercase-letter escaping rule to the
		/// module path before requesting it.
		/// </summary>
		/// <param name="name">The module path, e.g. "golang.org/x/sys".</param>
		/// <param name="version">The module version to download, e.g. "v0.47.0".</param>
		/// <returns>A seekable, position-0 stream containing the zip archive, or null if the download failed.</returns>
		public static async Task<MemoryStream?> GetPackageZipAsync( string name, string version )
		{
			MemoryStream? zip_stream = null;
			try
			{
				string fixed_name = CanonizeString( name );
				string zip_request = $"https://proxy.golang.org/{fixed_name}/@v/{version}.zip";

				HttpResponseMessage response = await HttpClientInstance.GetAsync( zip_request );
				response.EnsureSuccessStatusCode();

				zip_stream = new MemoryStream();
				await response.Content.CopyToAsync( zip_stream );
				zip_stream.Position = 0;
			}
			catch( HttpRequestException ex )
			{
				ConsoleLogger.Error( $"GoogleSource query failed with message '{ex.Message}'" );
				zip_stream = null;
			}

			return zip_stream;
		}
	}
}
