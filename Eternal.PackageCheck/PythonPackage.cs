// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;
using System.Text;

namespace Eternal.PackageCheck
{
	/// <summary>
	/// The "info" object of a PyPI JSON API response (https://pypi.org/pypi/{package}/json). Only the members needed to describe the package are read; the
	/// download counts, docs/bugtrack URLs, long description, and the "releases" map of every historical version are ignored.
	/// </summary>
	internal class PythonPackageInfoRaw
	{
		public string name = string.Empty;
		public string version = string.Empty;
		public string? summary = string.Empty;
		public string? author = string.Empty;
		public string? author_email = string.Empty;
		public string? maintainer = string.Empty;
		public string? maintainer_email = string.Empty;

		/// <summary>The legacy free-text "License" core metadata field. May hold an SPDX code, a vague label such as "Apache 2.0", or the entire license text.</summary>
		public string? license = string.Empty;

		/// <summary>The PEP 639 "License-Expression" field; a valid SPDX expression, e.g. "MIT" or "Apache-2.0 OR BSD-3-Clause". Preferred over <see cref="license"/>.</summary>
		public string? license_expression = string.Empty;

		public string? home_page = string.Empty;
		public string? package_url = string.Empty;
		public string? project_url = string.Empty;

		/// <summary>The PEP 621 "Project-URL" labels, e.g. { "Homepage": "...", "Source": "..." }. Labels are author-chosen, so their casing is inconsistent.</summary>
		public Dictionary<string, string>? project_urls = new Dictionary<string, string>();

		/// <summary>The trove classifiers. Packages predating PEP 639 declare their license here, e.g. "License :: OSI Approved :: MIT License".</summary>
		public List<string>? classifiers = new List<string>();

		/// <summary>The "Requires-Dist" entries as PEP 508 requirement strings, e.g. "urllib3&lt;3,&gt;=1.26" or "PySocks!=1.5.7,&gt;=1.5.6; extra == socks".</summary>
		public List<string>? requires_dist = new List<string>();

		public string? requires_python = string.Empty;
		public bool yanked = false;
		public string? yanked_reason = null;
	}

	/// <summary>
	/// One published file of a single version, from the "urls" array. A version normally has one "sdist" plus one "bdist_wheel" per supported platform, so
	/// there can be dozens of these. The checksums, download counts, and signature members are ignored.
	/// </summary>
	internal class PythonPackageFileRaw
	{
		public string filename = string.Empty;

		/// <summary>The distribution kind, "sdist" for a source distribution or "bdist_wheel" for a wheel.</summary>
		public string packagetype = string.Empty;

		public string url = string.Empty;
		public DateTime upload_time_iso_8601 = DateTime.MinValue;
		public long size = 0;
		public bool yanked = false;
	}

	/// <summary>
	/// The PyPI JSON API response. "urls" holds the files of the resolved version only; the "releases" and "vulnerabilities" members are ignored, the latter
	/// because CVEs are looked up from OSV by <see cref="CveChecker"/>.
	/// </summary>
	internal class PythonPackageRaw
	{
		public PythonPackageInfoRaw info = new PythonPackageInfoRaw();
		public List<PythonPackageFileRaw>? urls = new List<PythonPackageFileRaw>();
	}

	internal class PythonPackage
		: GenericPackage
	{
		/// <summary>
		/// Maps the trove license classifiers to their SPDX code, for packages that predate PEP 639's "License-Expression" field. Several classifiers name a
		/// license family rather than a specific license ("BSD License", "Apache Software License"); those are mapped to the overwhelmingly common member of
		/// the family, and the boilerplate match performed by <see cref="LicenseIdentifier.IdentifyLicense"/> during the repo scrape warns if the guess is wrong.
		/// </summary>
		private static readonly Dictionary<string, string> ClassifierToSpdxCode = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase )
		{
			{ "License :: OSI Approved :: MIT License", "MIT" },
			{ "License :: OSI Approved :: MIT No Attribution License (MIT-0)", "MIT-0" },
			{ "License :: OSI Approved :: ISC License (ISCL)", "ISC" },
			{ "License :: OSI Approved :: BSD License", "BSD-3-Clause" },
			{ "License :: OSI Approved :: Apache Software License", "Apache-2.0" },
			{ "License :: OSI Approved :: Mozilla Public License 2.0 (MPL 2.0)", "MPL-2.0" },
			{ "License :: OSI Approved :: Python Software Foundation License", "PSF-2.0" },
			{ "License :: OSI Approved :: Zope Public License", "ZPL-2.1" },
			{ "License :: OSI Approved :: The Unlicense (Unlicense)", "Unlicense" },
			{ "License :: OSI Approved :: Universal Permissive License (UPL)", "UPL-1.0" },
			{ "License :: OSI Approved :: zlib/libpng License", "Zlib" },
			{ "License :: OSI Approved :: Boost Software License 1.0 (BSL-1.0)", "BSL-1.0" },
			{ "License :: OSI Approved :: Eclipse Public License 2.0 (EPL-2.0)", "EPL-2.0" },
			{ "License :: OSI Approved :: Artistic License", "Artistic-2.0" },
			{ "License :: OSI Approved :: Academic Free License (AFL)", "AFL-3.0" },
			{ "License :: CC0 1.0 Universal (CC0 1.0) Public Domain Dedication", "CC0-1.0" },
			{ "License :: OSI Approved :: GNU General Public License v2 (GPLv2)", "GPL-2.0-only" },
			{ "License :: OSI Approved :: GNU General Public License v2 or later (GPLv2+)", "GPL-2.0-or-later" },
			{ "License :: OSI Approved :: GNU General Public License v3 (GPLv3)", "GPL-3.0-only" },
			{ "License :: OSI Approved :: GNU General Public License v3 or later (GPLv3+)", "GPL-3.0-or-later" },
			{ "License :: OSI Approved :: GNU Library or Lesser General Public License (LGPL)", "LGPL-2.1" },
			{ "License :: OSI Approved :: GNU Lesser General Public License v2 or later (LGPLv2+)", "LGPL-2.1-or-later" },
			{ "License :: OSI Approved :: GNU Lesser General Public License v3 (LGPLv3)", "LGPL-3.0-only" },
			{ "License :: OSI Approved :: GNU Lesser General Public License v3 or later (LGPLv3+)", "LGPL-3.0-or-later" }
		};

		/// <summary>
		/// Maps the common non-SPDX spellings found in the legacy free-text "License" metadata field to their SPDX code.
		/// </summary>
		private static readonly Dictionary<string, string> LegacyLicenseToSpdxCode = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase )
		{
			{ "MIT License", "MIT" },
			{ "ISC License", "ISC" },
			{ "Apache 2.0", "Apache-2.0" },
			{ "Apache-2", "Apache-2.0" },
			{ "Apache License 2.0", "Apache-2.0" },
			{ "Apache License, Version 2.0", "Apache-2.0" },
			{ "Apache Software License", "Apache-2.0" },
			{ "BSD", "BSD-3-Clause" },
			{ "BSD License", "BSD-3-Clause" },
			{ "BSD 3-Clause", "BSD-3-Clause" },
			{ "BSD 3-Clause License", "BSD-3-Clause" },
			{ "3-Clause BSD License", "BSD-3-Clause" },
			{ "New BSD License", "BSD-3-Clause" },
			{ "BSD 2-Clause", "BSD-2-Clause" },
			{ "2-Clause BSD License", "BSD-2-Clause" },
			{ "Simplified BSD License", "BSD-2-Clause" },
			{ "MPL 2.0", "MPL-2.0" },
			{ "MPL-2", "MPL-2.0" },
			{ "The Unlicense", "Unlicense" },
			{ "PSF", "PSF-2.0" },
			{ "PSFL", "PSF-2.0" },
			{ "PSF License", "PSF-2.0" },
			{ "Python Software Foundation License", "PSF-2.0" }
		};

		/// <summary>
		/// The longest legacy "License" field value still treated as a license name rather than as inlined license text. Projects that declare
		/// license = { file = "LICENSE" } have the whole license file inlined into this field, which runs to thousands of characters.
		/// </summary>
		private const int MaximumLicenseNameLength = 64;

		/// <summary>
		/// The license text inlined into the legacy "License" metadata field, if there was any. Kept so the copyright message can be scraped from it without
		/// having to reach the repo.
		/// </summary>
		private string InlineLicenseText = string.Empty;

		private void AddLicense( LicenseInfo licenseInfo )
		{
			Licenses.Add( licenseInfo );
		}

		/// <summary>
		/// Supplies the fallbacks PyPI does not provide - the project page as a homepage of last resort, and a copyright message scraped from any license text
		/// inlined into the metadata - then delegates to the base class's license scraping and copyright cleanup.
		/// </summary>
		internal void CleanupData()
		{
			if( string.IsNullOrEmpty( HomepageUri ) )
			{
				HomepageUri = $"https://pypi.org/project/{Name}/{Version}/";
			}

			ScrapeCopyrightFromInlineLicense();

			base.CleanupData( "PyPI" );
		}

		/// <summary>
		/// Pulls the copyright message out of the license text inlined into the legacy "License" metadata field. Does nothing if a copyright message is already
		/// set or no license text was inlined; the base class scrapes the repo's license file when this finds nothing.
		/// </summary>
		private void ScrapeCopyrightFromInlineLicense()
		{
			if( !string.IsNullOrEmpty( Copyright ) || string.IsNullOrEmpty( InlineLicenseText ) )
			{
				return;
			}

			string[] license_lines = InlineLicenseText.Split( ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
			foreach( string license_line in license_lines )
			{
				if( license_line.StartsWith( "Copyright", StringComparison.OrdinalIgnoreCase ) )
				{
					Copyright = PackageCheck.SanitizeString( license_line );
					ConsoleLogger.Log( $" .. found copyright message in the inlined license text -> using '{Copyright}'" );
					return;
				}
			}
		}

		/// <summary>
		/// Looks up the first of the given author-chosen "Project-URL" labels that is present. The labels are matched case-insensitively because their casing is
		/// entirely up to the package author (e.g. numpy publishes "homepage" and "source", Django publishes "Homepage" and "Source").
		/// </summary>
		/// <param name="projectUrls">The "project_urls" map from the metadata, which may be null.</param>
		/// <param name="labels">The labels to look for, in order of preference.</param>
		/// <returns>The matching URL, or "" if none of the labels are present.</returns>
		private static string FindProjectUrl( Dictionary<string, string>? projectUrls, string[] labels )
		{
			if( projectUrls == null )
			{
				return string.Empty;
			}

			foreach( string label in labels )
			{
				foreach( KeyValuePair<string, string> project_url in projectUrls )
				{
					if( string.Equals( project_url.Key, label, StringComparison.OrdinalIgnoreCase ) && !string.IsNullOrEmpty( project_url.Value ) )
					{
						return project_url.Value;
					}
				}
			}

			return string.Empty;
		}

		/// <summary>
		/// Strips the email addresses out of a PEP 621 author or maintainer field, which packs the name and address together as "Name &lt;address&gt;" and may hold a
		/// comma separated list of them. A display name that itself contains a comma is wrapped in double quotes per RFC 5322, so only the commas outside a quoted
		/// name separate one author from the next.
		/// </summary>
		/// <param name="emailField">The raw "author_email" or "maintainer_email" value.</param>
		/// <returns>The comma separated display names, or the field as-is if it holds nothing but a bare address.</returns>
		private static string ExtractDisplayNames( string? emailField )
		{
			if( string.IsNullOrEmpty( emailField ) )
			{
				return string.Empty;
			}

			List<string> names = new List<string>();
			StringBuilder name_builder = new StringBuilder();
			bool inside_address = false;
			bool inside_quotes = false;

			foreach( char character in emailField )
			{
				if( character == '"' )
				{
					inside_quotes = !inside_quotes;
				}
				else if( character == '<' && !inside_quotes )
				{
					inside_address = true;
				}
				else if( character == '>' && !inside_quotes )
				{
					inside_address = false;
				}
				else if( character == ',' && !inside_quotes && !inside_address )
				{
					names.Add( name_builder.ToString() );
					name_builder.Clear();
				}
				else if( !inside_address )
				{
					name_builder.Append( character );
				}
			}

			names.Add( name_builder.ToString() );
			names.RemoveAll( string.IsNullOrWhiteSpace );
			if( names.Count == 0 )
			{
				// The field held nothing but addresses, so use it verbatim rather than reporting no author at all.
				return emailField.Trim();
			}

			return string.Join( ", ", names.Select( x => x.Trim() ) );
		}

		/// <summary>
		/// Resolves the author of the package. PEP 621 packages usually leave "author" empty and pack the name into "author_email" instead, and a fair number of
		/// packages name only a maintainer.
		/// </summary>
		/// <param name="info">The package metadata to read the author from.</param>
		/// <returns>The author or maintainer, or "" if the package names neither.</returns>
		private static string ResolveAuthors( PythonPackageInfoRaw info )
		{
			if( !string.IsNullOrEmpty( info.author ) )
			{
				return PackageCheck.SanitizeString( info.author );
			}

			string author_names = ExtractDisplayNames( info.author_email );
			if( !string.IsNullOrEmpty( author_names ) )
			{
				return PackageCheck.SanitizeString( author_names );
			}

			if( !string.IsNullOrEmpty( info.maintainer ) )
			{
				return PackageCheck.SanitizeString( info.maintainer );
			}

			string maintainer_names = ExtractDisplayNames( info.maintainer_email );
			if( !string.IsNullOrEmpty( maintainer_names ) )
			{
				return PackageCheck.SanitizeString( maintainer_names );
			}

			return string.Empty;
		}

		/// <summary>
		/// Splits a PEP 639 SPDX license expression into its individual license codes, discarding the operators. "AND" and "OR" are dropped, and the id following
		/// a "WITH" is dropped too because it names an SPDX exception rather than a license.
		/// </summary>
		/// <param name="licenseExpression">The expression, e.g. "Apache-2.0 OR BSD-3-Clause" or "GPL-2.0-only WITH Classpath-exception-2.0".</param>
		/// <returns>The distinct SPDX license codes found in the expression.</returns>
		private static List<string> ParseLicenseExpression( string licenseExpression )
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
		/// Determines whether the legacy "License" metadata field holds a license name rather than the whole license text inlined into it, which is what projects
		/// declaring license = { file = "LICENSE" } end up publishing.
		/// </summary>
		/// <param name="license">The raw legacy "License" field value.</param>
		/// <returns>true if the value is short enough and single-line enough to be a license name.</returns>
		private static bool IsLicenseName( string license )
		{
			return license.Length <= MaximumLicenseNameLength && !license.Contains( '\n' ) && !license.Contains( '\r' );
		}

		/// <summary>The infix operators of an SPDX license expression, used to tell an expression apart from a bare license name.</summary>
		private static readonly string[] LicenseExpressionOperators = [" OR ", " AND ", " WITH "];

		/// <summary>
		/// Determines whether a license value is an SPDX expression rather than a single license id. Needed because a project can put a perfectly valid expression
		/// in the legacy free-text "License" field instead of in "License-Expression", as PySide6 does with "LGPL-3.0-only OR GPL-2.0-only OR GPL-3.0-only".
		/// </summary>
		/// <param name="license">The license value to test.</param>
		/// <returns>true if the value carries an SPDX expression operator.</returns>
		private static bool IsLicenseExpression( string license )
		{
			return LicenseExpressionOperators.Any( x => license.Contains( x, StringComparison.OrdinalIgnoreCase ) );
		}

		/// <summary>
		/// Records every license id found in an SPDX expression.
		/// </summary>
		/// <param name="licenseExpression">The expression to parse.</param>
		private void AddLicensesFromExpression( string licenseExpression )
		{
			foreach( string spdx_code in ParseLicenseExpression( licenseExpression ) )
			{
				AddLicense( new LicenseInfo( spdx_code ) );
			}
		}

		/// <summary>
		/// Records the package's licenses, preferring the most reliable source available: the PEP 639 "License-Expression" field, then the trove classifiers, then
		/// the legacy free-text "License" field. Any license text inlined into the legacy field is kept for <see cref="ScrapeCopyrightFromInlineLicense"/>.
		/// </summary>
		/// <param name="info">The package metadata to read the licenses from.</param>
		private void AddAllLicenses( PythonPackageInfoRaw info )
		{
			string legacy_license = ( info.license ?? string.Empty ).Trim();
			if( !string.IsNullOrEmpty( legacy_license ) && !IsLicenseName( legacy_license ) )
			{
				ConsoleLogger.Log( " .. the legacy 'License' metadata field holds inlined license text rather than a license name" );
				InlineLicenseText = legacy_license;
			}

			string license_expression = ( info.license_expression ?? string.Empty ).Trim();
			if( !string.IsNullOrEmpty( license_expression ) )
			{
				AddLicensesFromExpression( license_expression );
				return;
			}

			if( info.classifiers != null )
			{
				foreach( string classifier in info.classifiers )
				{
					if( ClassifierToSpdxCode.TryGetValue( classifier.Trim(), out string? spdx_code ) )
					{
						AddLicense( new LicenseInfo( spdx_code ) );
					}
					else if( classifier.StartsWith( "License ::", StringComparison.OrdinalIgnoreCase ) )
					{
						ConsoleLogger.Warning( $"No SPDX code known for the license classifier '{classifier}'" );
					}
				}
			}

			if( Licenses.Count > 0 )
			{
				return;
			}

			if( !string.IsNullOrEmpty( legacy_license ) && string.IsNullOrEmpty( InlineLicenseText ) )
			{
				// A project is free to put a full SPDX expression in the legacy field rather than in "License-Expression"; splitting it here stops the whole
				// expression being recorded as one nonexistent license id.
				if( IsLicenseExpression( legacy_license ) )
				{
					ConsoleLogger.Log( $" .. the legacy 'License' metadata field holds the SPDX expression '{legacy_license}'" );
					AddLicensesFromExpression( legacy_license );
					return;
				}

				if( LegacyLicenseToSpdxCode.TryGetValue( legacy_license, out string? spdx_code ) )
				{
					ConsoleLogger.Log( $" .. mapped the legacy license '{legacy_license}' to the SPDX code '{spdx_code}'" );
					AddLicense( new LicenseInfo( spdx_code ) );
				}
				else
				{
					AddLicense( new LicenseInfo( legacy_license ) );
				}

				return;
			}

			ConsoleLogger.Warning( $"Package '{info.name}' declares no license expression, license classifier, or license name" );
		}

		private void AddDependencies( List<string>? requiresDist )
		{
			if( requiresDist == null )
			{
				return;
			}

			foreach( string requirement in requiresDist )
			{
				PackageDependency? package_dependency = ParseRequirement( requirement );
				if( package_dependency != null )
				{
					AddSingleDependency( package_dependency );
				}
			}
		}

		/// <summary>
		/// Parses a single PEP 508 "Requires-Dist" requirement string into a dependency. Handles both the current spelling ("botocore[crt]&lt;2.0a0,&gt;=1.21.0; extra ==
		/// crt") and the metadata 2.1 and earlier spelling that parenthesized the version specifier ("charset-normalizer (&lt;4,&gt;=2)"). A requirement guarded by an
		/// "extra ==" marker is only installed when that extra is requested, so it is recorded as a development dependency; every other requirement, including one
		/// guarded by an environment marker such as sys_platform, is a production dependency.
		/// </summary>
		/// <param name="requirement">The raw requirement string.</param>
		/// <returns>The parsed dependency, or null if no package name could be read from it.</returns>
		private static PackageDependency? ParseRequirement( string requirement )
		{
			string remaining_requirement = requirement.Trim();
			if( string.IsNullOrEmpty( remaining_requirement ) )
			{
				return null;
			}

			// Split the environment marker off the end.
			string marker = string.Empty;
			int marker_index = remaining_requirement.IndexOf( ';' );
			if( marker_index >= 0 )
			{
				marker = remaining_requirement[( marker_index + 1 )..].Trim();
				remaining_requirement = remaining_requirement[..marker_index].Trim();
			}

			// The name is the leading run of PEP 508 name characters; the extras list and version specifier follow it.
			int name_length = 0;
			while( name_length < remaining_requirement.Length )
			{
				char character = remaining_requirement[name_length];
				if( !char.IsLetterOrDigit( character ) && character != '-' && character != '_' && character != '.' )
				{
					break;
				}

				name_length++;
			}

			if( name_length == 0 )
			{
				ConsoleLogger.Warning( $"Failed to parse a package name from the requirement '{requirement}'" );
				return null;
			}

			string name = remaining_requirement[..name_length];
			string version_specifier = remaining_requirement[name_length..].Trim();

			// Drop any extras requested of the dependency itself, e.g. the "[crt]" of "botocore[crt]<2.0a0,>=1.21.0".
			if( version_specifier.StartsWith( '[' ) )
			{
				int end_of_extras = version_specifier.IndexOf( ']' );
				if( end_of_extras >= 0 )
				{
					version_specifier = version_specifier[( end_of_extras + 1 )..].Trim();
				}
			}

			if( version_specifier.StartsWith( '(' ) && version_specifier.EndsWith( ')' ) )
			{
				version_specifier = version_specifier[1..^1].Trim();
			}

			EDependencyType dependency_type = EDependencyType.Production;
			if( marker.Contains( "extra ==", StringComparison.OrdinalIgnoreCase ) )
			{
				dependency_type = EDependencyType.Development;
			}

			return new PackageDependency()
			{
				Name = name,
				Version = version_specifier,
				DependencyType = dependency_type
			};
		}

		/// <summary>
		/// Picks the file to report as the package download and works out when the version was published. The source distribution is preferred over a wheel because
		/// it is the one artifact guaranteed to carry the license file, and a version can publish dozens of platform specific wheels. The publish date is the
		/// earliest upload across every file of the version, since the wheels are uploaded one by one after the release is cut.
		/// </summary>
		/// <param name="files">The "urls" array of the resolved version, which may be null or empty for a version with no files left published.</param>
		/// <returns>The download URL and the publish timestamp, either of which is empty/DateTime.MinValue if it could not be determined.</returns>
		private static ( string downloadUri, DateTime uploadedAt ) GetDistributionDetails( List<PythonPackageFileRaw>? files )
		{
			( string downloadUri, DateTime uploadedAt ) result = ( string.Empty, DateTime.MinValue );
			if( files == null || files.Count == 0 )
			{
				ConsoleLogger.Warning( "No published files found for this version" );
				return result;
			}

			PythonPackageFileRaw? chosen_file = files.Find( x => x.packagetype == "sdist" );
			if( chosen_file == null )
			{
				chosen_file = files.Find( x => x.packagetype == "bdist_wheel" );
				if( chosen_file == null )
				{
					chosen_file = files[0];
				}

				ConsoleLogger.Warning( $"No source distribution published; using '{chosen_file.filename}'" );
			}

			result.downloadUri = chosen_file.url;

			DateTime earliest_upload = DateTime.MaxValue;
			foreach( PythonPackageFileRaw file in files )
			{
				if( file.upload_time_iso_8601 > DateTime.MinValue && file.upload_time_iso_8601 < earliest_upload )
				{
					earliest_upload = file.upload_time_iso_8601;
				}
			}

			if( earliest_upload < DateTime.MaxValue )
			{
				result.uploadedAt = earliest_upload.ToUniversalTime();
			}

			return result;
		}

		/// <summary>
		/// Looks up a Python package's metadata from the PyPI JSON API: resolves the requested version (or the latest release if <paramref name="version"/> is empty),
		/// and reads its author, licenses, homepage, source repository, download URL, and dependencies.
		/// </summary>
		/// <param name="packageName">The PyPI project name, e.g. "requests". Does not need to be PEP 503 normalized; PyPI resolves case and '-'/'_'/'.' differences itself.</param>
		/// <param name="version">The specific version to fetch, or "" to use the latest release. PyPI only serves a pre-release as the latest if the project has published nothing else.</param>
		/// <returns>The populated package, or null if the package or the requested version could not be retrieved.</returns>
		internal static PythonPackage? GetPythonPackage( string packageName, string version, bool isRootPackage )
		{
			string get_request;
			if( string.IsNullOrEmpty( version ) )
			{
				get_request = $"https://pypi.org/pypi/{Uri.EscapeDataString( packageName )}/json";
			}
			else
			{
				get_request = $"https://pypi.org/pypi/{Uri.EscapeDataString( packageName )}/{Uri.EscapeDataString( version )}/json";
			}

			string content = PackageCheck.Request( get_request ).Result;
			if( string.IsNullOrEmpty( content ) )
			{
				ConsoleLogger.Error( $"Failed to retrieve data about the '{packageName}' package from PyPI.org" );
				return null;
			}

			PythonPackageRaw? raw_package = JsonHelper.ReadJson<PythonPackageRaw>( content );
			if( raw_package == null )
			{
				ConsoleLogger.Error( $"Failed to deserialize data for the '{packageName}' package from PyPI.org" );
				return null;
			}

			PythonPackageInfoRaw info = raw_package.info;
			if( string.IsNullOrEmpty( info.name ) || string.IsNullOrEmpty( info.version ) )
			{
				ConsoleLogger.Error( $"No valid metadata found for package '{packageName}'" );
				return null;
			}

			if( info.yanked )
			{
				ConsoleLogger.Warning( $"Version '{info.version}' of '{info.name}' has been yanked - '{info.yanked_reason ?? "no reason given"}'" );
			}

			( string downloadUri, DateTime uploadedAt ) distribution = GetDistributionDetails( raw_package.urls );

			// PyPI leaves "home_page" empty for anything built to PEP 621, which publishes the homepage as a "Project-URL" label instead.
			string homepage_uri = info.home_page ?? string.Empty;
			if( string.IsNullOrEmpty( homepage_uri ) )
			{
				homepage_uri = FindProjectUrl( info.project_urls, ["Homepage", "Home", "Home Page", "Documentation"] );
			}

			string source_code_uri = FindProjectUrl( info.project_urls, ["Source", "Source Code", "SourceCode", "Repository", "Code", "GitHub"] );

			PythonPackage python_package = new PythonPackage
			{
				Name = info.name,
				Version = info.version,
				VersionCreatedAt = distribution.uploadedAt,
				Authors = ResolveAuthors( info ),
				Copyright = string.Empty,
				HomepageUri = homepage_uri,
				SourceCodeUri = source_code_uri,
				DownloadUri = distribution.downloadUri,
				Description = PackageCheck.SanitizeString( info.summary ?? string.Empty )
			};

			// Add licenses
			python_package.AddAllLicenses( info );

			if( isRootPackage )
			{
				// Add dependencies
				python_package.AddDependencies( info.requires_dist );
			}

			return python_package;
		}

		protected override void UpdateDependency( PackageDependency dependency )
		{
			PythonPackage? python_package = GetPythonPackage( dependency.Name, "", false );
			if( python_package == null )
			{
				return;
			}

			python_package.CleanupDependencyData( "PyPI" );

			base.UpdateDependency( dependency );
		}

		/// <summary>
		/// Looks up the given Python package, cleans up its data (licenses, copyright, fallback URIs), and prints its details to the console. Does nothing if the
		/// package or the requested version could not be found.
		/// </summary>
		/// <param name="packageName">The PyPI project name, e.g. "requests".</param>
		/// <param name="version">The specific version to check, or "" to use the latest release.</param>
		public static void CheckPythonPackage( string packageName, string version )
		{
			PythonPackage? python_package = GetPythonPackage( packageName, version, true );
			if( python_package == null )
			{
				return;
			}

			python_package.CleanupData();

			python_package.PrintDetails( "PyPI" );
		}
	}
}
