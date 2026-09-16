// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;

namespace Eternal.PackageCheck
{
	public class LicenseDetails
	{
		public required string SpdxCode;
		public required string LicenseVersion;
		public required string DefaultUrl;
		public required string StartLicenseText;
		public required string EndLicenseText;
		public required List<string> Identifiers;
	}

	internal static class LicenseIdentifier
	{
		private static readonly Dictionary<string, LicenseDetails>? LicenseBoilerPlate;

		static LicenseIdentifier()
		{
			string licenses_path = Path.Combine( AppContext.BaseDirectory, "LicenseBoilerPlate.json" );
			LicenseBoilerPlate = JsonHelper.ReadJsonFile<Dictionary<string, LicenseDetails>>( licenses_path );
			if( LicenseBoilerPlate == null || LicenseBoilerPlate.Count == 0 )
			{
				ConsoleLogger.Error( $"Failed to load LicenseBoilerPlate.json!" );
				LicenseBoilerPlate = null;
			}
		}

		/// <summary>
		/// Fills in a fallback opensource.org license URL for any license that has a recognized SPDX code but no URI set yet (e.g. because repo scraping found no
		/// license file). Licenses without a recognized SPDX code are left untouched, since there's no valid template URL to build for them.
		/// </summary>
		/// <param name="licenses">The licenses to fill in default URLs for, updated in place.</param>
		public static void SetLicenseUrlsToDefault( List<LicenseInfo> licenses )
		{
			if( LicenseBoilerPlate == null )
			{
				return;
			}

			foreach( LicenseInfo license_info in licenses )
			{
				if( license_info.IsSpdxCode )
				{
					if( !LicenseBoilerPlate.TryGetValue( license_info.SpdxCode, out LicenseDetails? license_details ) )
					{
						ConsoleLogger.Warning( $"No metadata found for SPDX code '{license_info.SpdxCode}'" );
					}
					else
					{
						if( string.IsNullOrEmpty( license_info.HtmlLicenseUri ) )
						{
							ConsoleLogger.Warning( $"No URI for the '{license_info.SpdxCode}' license -> defaulting to template." );
							license_info.HtmlLicenseUri = license_details.DefaultUrl;
						}

						license_info.LicenseVersion = license_details.LicenseVersion;
					}
				}
			}
		}

		/// <summary>
		/// Checks whether the given file's content matches one of <paramref name="licenses"/>' known SPDX boilerplate: the license's start/end marker text must both be present,
		/// and every one of its distinguishing identifier phrases must also appear. On a match, sets that license's HTML URI and content and stops looking at further
		/// licenses/identifiers.
		/// </summary>
		/// <param name="licenses">The candidate licenses (by SPDX code) to try matching against.</param>
		/// <param name="htmlUrl">The human-viewable URL of the license file, recorded on a match.</param>
		/// <param name="downloadUrl">The raw download URL of the license file (currently unused, reserved for future use).</param>
		/// <param name="licenseContent">The license file's raw text content.</param>
		/// <returns>true if the content matched one of the candidate licenses' boilerplate; otherwise false.</returns>
		public static bool IdentifyLicense( List<LicenseInfo> licenses, string htmlUrl, string licenseContent )
		{
			if( LicenseBoilerPlate == null )
			{
				return false;
			}

			string clean_license_content = PackageCheck.SanitizeString( licenseContent );
			foreach( LicenseInfo license_info in licenses )
			{
				// Get the metadata for the license
				if( LicenseBoilerPlate.TryGetValue( license_info.SpdxCode, out LicenseDetails? license_details ) )
				{
					bool start_end_match = false;
					bool has_start_end_text = ( !string.IsNullOrEmpty( license_details.StartLicenseText ) && !string.IsNullOrEmpty( license_details.EndLicenseText ) );
					if( has_start_end_text )
					{
						int start_of_license = clean_license_content.IndexOf( license_details.StartLicenseText, StringComparison.OrdinalIgnoreCase );
						int end_of_license = clean_license_content.IndexOf( license_details.EndLicenseText, StringComparison.OrdinalIgnoreCase );
						if( start_of_license > -1 && end_of_license > -1 )
						{
							start_end_match = true;
							ConsoleLogger.Log( $" .. start and end sections match for '{license_info.SpdxCode}'" );
						}
						else
						{
							ConsoleLogger.Warning( $"License file at '{license_details.DefaultUrl}' does not contain expected start or end text from license boilerplate." );
						}
					}

					bool identifiers_match = false;
					if( license_details.Identifiers.Count > 0 )
					{
						if( license_details.Identifiers.All( identifier => clean_license_content.Contains( identifier, StringComparison.OrdinalIgnoreCase ) ) )
						{
							ConsoleLogger.Log( $" .. identifiers match for '{license_info.SpdxCode}'" );
							identifiers_match = true;
						}
						else
						{
							ConsoleLogger.Warning( $"License file at '{license_details.DefaultUrl}' does not contain identifying text from license boilerplate." );
						}
					}

					if( start_end_match && identifiers_match )
					{
						license_info.HtmlLicenseUri = htmlUrl;
						license_info.LicenseContent = licenseContent;
						ConsoleLogger.Log( $"Found start text, end text, and identifying text -> using '{htmlUrl}'" );
					}
					else if( !has_start_end_text && identifiers_match )
					{
						license_info.HtmlLicenseUri = htmlUrl;
						license_info.LicenseContent = licenseContent;
						ConsoleLogger.Log( $"Found identifying text, and there is no start or end text to find -> using '{htmlUrl}'" );
					}
				}
				else
				{
					ConsoleLogger.Warning( $"Missing license metadata for '{license_info.SpdxCode}'" );
				}
			}

			return false;
		}

		/// <summary>
		/// If no copyright message is set yet, scans each license's already-fetched content for a "Copyright" line (the text between "Copyright" and the license's
		/// known boilerplate start marker) and uses the first one found. Does nothing if
		/// <paramref name="copyright"/> is already set.
		/// </summary>
		/// <param name="licenses">The licenses whose content to scan; each must already have its HtmlLicenseUri/LicenseContent populated to be scanned.</param>
		/// <param name="copyright">The copyright message to fill in if empty; left unchanged if a message is already set or none could be found.</param>
		public static void ScrapeCopyrightFromLicenses( List<LicenseInfo> licenses, ref string copyright )
		{
			if( LicenseBoilerPlate == null )
			{
				return;
			}

			if( !string.IsNullOrEmpty( copyright ) )
			{
				return;
			}

			foreach( LicenseInfo license_info in licenses )
			{
				if( string.IsNullOrEmpty( license_info.HtmlLicenseUri ) )
				{
					ConsoleLogger.Warning( $"No license URL set for '{license_info.SpdxCode}'" );
					continue;
				}

				if( !LicenseBoilerPlate.TryGetValue( license_info.SpdxCode, out LicenseDetails? license_details ) )
				{
					ConsoleLogger.Warning( $"Failed to retrieve internal license details for '{license_info.SpdxCode}'" );
					continue;
				}

				if( string.IsNullOrEmpty( license_details.StartLicenseText ) || string.IsNullOrEmpty( license_details.EndLicenseText ) )
				{
					ConsoleLogger.Warning( $"There are no start or end text phrases to key off for  '{license_info.SpdxCode}'" );
					continue;
				}

				string license_content = PackageCheck.SanitizeString( license_info.LicenseContent );
				if( string.IsNullOrEmpty( license_content ) )
				{
					ConsoleLogger.Warning( $"There is no license content for '{license_info.HtmlLicenseUri}'" );
					continue;
				}

				int start_license = license_content.IndexOf( license_details.StartLicenseText, StringComparison.OrdinalIgnoreCase );
				int end_license = license_content.IndexOf( license_details.EndLicenseText, StringComparison.OrdinalIgnoreCase );
				if( start_license < 0 || end_license < 0 )
				{
					ConsoleLogger.Warning( $"Failed to find start or end of '{license_info.SpdxCode}' license text in '{license_info.HtmlLicenseUri}'" );
					continue;
				}

				int copyright_index = license_content.IndexOf( "Copyright", StringComparison.OrdinalIgnoreCase );
				if( copyright_index < 0 || ( copyright_index >= start_license && copyright_index < ( end_license + license_details.EndLicenseText.Length ) ) )
				{
					ConsoleLogger.Warning( $"Failed to find 'Copyright' outside the license in '{license_info.HtmlLicenseUri}'" );
					continue;
				}

				string copyright_message = license_content.Substring( copyright_index, start_license - copyright_index );
				copyright = PackageCheck.SanitizeString( copyright_message );
				ConsoleLogger.Log( $" .. found copyright message in license -> using '{copyright}'" );
			}
		}
	}
}
