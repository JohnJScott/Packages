// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Eternal.PackageCheck
{
	/// <summary>
	/// Normalizes the polymorphic "repository" field. npm accepts either a bare string (treated as the URL) or an object ( { "type": "git", "url": "..." } ).
	/// </summary>
	public sealed class NpmRepositoryConverter
		: JsonConverter<NpmRepository?>
	{
		/// <inheritdoc/>
		public override NpmRepository? Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
		{
			if( reader.TokenType == JsonTokenType.Null )
			{
				return null;
			}

			if( reader.TokenType == JsonTokenType.String )
			{
				return new NpmRepository( null, reader.GetString() );
			}

			if( reader.TokenType == JsonTokenType.StartObject )
			{
				using JsonDocument repository_document = JsonDocument.ParseValue( ref reader );
				JsonElement repository_object = repository_document.RootElement;

				string? type = null;
				if( repository_object.TryGetProperty( "type", out JsonElement type_element ) && type_element.ValueKind != JsonValueKind.Null )
				{
					type = type_element.GetString();
				}

				string? url = null;
				if( repository_object.TryGetProperty( "url", out JsonElement url_element ) && url_element.ValueKind != JsonValueKind.Null )
				{
					url = url_element.GetString();
				}

				return new NpmRepository( type, url );
			}

			// Any other shape (e.g. an array of repositories) is ignored.
			reader.Skip();
			return null;
		}

		/// <inheritdoc/>
		public override void Write( Utf8JsonWriter writer, NpmRepository? value, JsonSerializerOptions options )
		{
			if( value == null )
			{
				writer.WriteNullValue();
				return;
			}

			// Bare string when only a URL is present; full object when a type exists.
			if( string.IsNullOrEmpty( value.type ) )
			{
				if( value.url == null )
				{
					writer.WriteNullValue();
				}
				else
				{
					writer.WriteStringValue( value.url );
				}
			}
			else
			{
				writer.WriteStartObject();
				writer.WriteString( "type", value.type );
				writer.WriteString( "url", value.url );
				writer.WriteEndObject();
			}
		}
	}

	/// <summary>
	/// Normalizes the polymorphic "license" field to a plain string. npm has used two forms over time: a bare SPDX string ("MIT") and an object
	/// ( { "type": "MIT", "url": "..." } ). The object form is reduced to its type.
	/// </summary>
	public sealed class NpmLicenseConverter
		: JsonConverter<string?>
	{
		/// <inheritdoc/>
		public override string? Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
		{
			if( reader.TokenType == JsonTokenType.Null )
			{
				return null;
			}

			if( reader.TokenType == JsonTokenType.String )
			{
				return reader.GetString();
			}

			if( reader.TokenType == JsonTokenType.StartObject )
			{
				using JsonDocument license_document = JsonDocument.ParseValue( ref reader );
				JsonElement license_object = license_document.RootElement;
				if( license_object.TryGetProperty( "type", out JsonElement type_element ) && type_element.ValueKind != JsonValueKind.Null )
				{
					return type_element.GetString();
				}

				return null;
			}

			// Any other shape (e.g. a legacy array of license objects) is ignored.
			reader.Skip();
			return null;
		}

		/// <inheritdoc/>
		public override void Write( Utf8JsonWriter writer, string? value, JsonSerializerOptions options )
		{
			if( value == null )
			{
				writer.WriteNullValue();
			}
			else
			{
				writer.WriteStringValue( value );
			}
		}
	}

	/// <summary>
	/// Normalizes the polymorphic "author" field. npm accepts either a bare string (e.g. "Sindre Sorhus &lt;sindresorhus@gmail.com&gt;") or an object
	/// ( { "name": "...", "email": "...", "url": "..." } ). The object form is reduced to its "name".
	/// </summary>
	public sealed class NpmAuthorConverter
		: JsonConverter<string?>
	{
		/// <inheritdoc/>
		public override string? Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
		{
			if( reader.TokenType == JsonTokenType.Null )
			{
				return null;
			}

			if( reader.TokenType == JsonTokenType.String )
			{
				return reader.GetString();
			}

			if( reader.TokenType == JsonTokenType.StartObject )
			{
				using JsonDocument author_document = JsonDocument.ParseValue( ref reader );
				JsonElement author_object = author_document.RootElement;
				if( author_object.TryGetProperty( "name", out JsonElement name_element ) && name_element.ValueKind != JsonValueKind.Null )
				{
					return name_element.GetString();
				}

				return null;
			}

			// Any other shape is ignored.
			reader.Skip();
			return null;
		}

		/// <inheritdoc/>
		public override void Write( Utf8JsonWriter writer, string? value, JsonSerializerOptions options )
		{
			if( value == null )
			{
				writer.WriteNullValue();
			}
			else
			{
				writer.WriteStringValue( value );
			}
		}
	}

	/// <summary>
	/// The "repository" entry of an npm package can be either a bare string (e.g. "https://github.com/user/repo") or an object of the form
	/// { "type": "git", "url": "..." }. Both shapes are reduced to this type.
	/// </summary>
	public class NpmRepository( string? inType, string? inUrl )
	{
		public string? type { get; set; } = inType;
		public string? url { get; set; } = inUrl;
	}

	/// <summary>
	/// The registry "dist" object for a single published version. Only the tarball download URL is read here; the shasum/integrity/fileCount members are ignored.
	/// </summary>
	public class NpmDist
	{
		[JsonPropertyName( "tarball" )]
		public string? tarball { get; set; }
	}

	/// <summary>
	/// A single published version manifest from the registry "versions" map. Only the dependency maps and dist tarball URL are read; every other member
	/// is ignored.
	/// </summary>
	public class NpmPackageVersion
	{
		[JsonPropertyName( "dependencies" )]
		public Dictionary<string, string>? dependencies { get; set; }

		[JsonPropertyName( "devDependencies" )]
		public Dictionary<string, string>? devDependencies { get; set; }

		[JsonPropertyName( "dist" )]
		public NpmDist? dist { get; set; }
	}

	internal class NpmPackageRaw
	{
		// --- Fields deserialized from the registry metadata ---

		[JsonPropertyName( "name" )]
		public string name { get; set; } = "";

		[JsonPropertyName( "description" )]
		public string description { get; set; } = "";

		[JsonPropertyName( "license" )]
		[JsonConverter( typeof(NpmLicenseConverter) )]
		public string? license { get; set; }

		[JsonPropertyName( "author" )]
		[JsonConverter( typeof(NpmAuthorConverter) )]
		public string? author { get; set; }

		[JsonPropertyName( "homepage" )]
		public string? homepage { get; set; }

		[JsonPropertyName( "repository" )]
		[JsonConverter( typeof(NpmRepositoryConverter) )]
		public NpmRepository? repository { get; set; }

		[JsonPropertyName( "versions" )]
		public Dictionary<string, NpmPackageVersion>? versions { get; set; }

		[JsonPropertyName( "time" )]
		public Dictionary<string, string>? time { get; set; }
	}

	internal class NpmPackage
		: GenericPackage
	{
		/// <summary>
		/// The canonical name of this package type. Lower cased it is the command line verb, and upper cased it is the deps.dev system name, so neither is stored
		/// separately. It is also the display name and the ecosystem component of the .tps file name.
		/// </summary>
		internal const string PackageType = "npm";

		/// <summary>The ecosystem name used by OSV, from https://ossf.github.io/osv-schema/#defined-ecosystems. OSV rejects anything else with an HTTP 400.</summary>
		internal const string OSVEcosystem = "npm";

		/// <summary>The path segment used by security.snyk.io, which names several ecosystems differently to OSV.</summary>
		internal const string SnykEcosystem = "npm";

		private void AddLicense( LicenseInfo licenseInfo )
		{
			Licenses.Add( licenseInfo );
		}

		/// <summary>
		/// Delegates to the base class's license scraping and copyright cleanup; npm packages need no additional fallback data beyond what the registry provides.
		/// </summary>
		internal void CleanupData()
		{
			base.CleanupData( PackageType, OSVEcosystem, SnykEcosystem );
		}

		private void AddDependencies( Dictionary<string, string>? dependencies, EDependencyType dependencyType )
		{
			if( dependencies != null )
			{
				foreach( KeyValuePair<string, string> dependency in dependencies )
				{
					PackageDependency package_dependency = new PackageDependency()
					{
						Name = dependency.Key,
						Version = dependency.Value,
						DependencyType = dependencyType
					};

					AddSingleDependency( package_dependency );
				}
			}
		}

		private void AddAllDependencies( NpmPackageVersion? packageVersion )
		{
			if( packageVersion != null )
			{
				AddDependencies( packageVersion.dependencies, EDependencyType.Production );
				AddDependencies( packageVersion.devDependencies, EDependencyType.Development );
			}
		}

		/// <summary>
		/// Picks the version of a package to use from its published versions.
		/// </summary>
		/// <param name="packageName">The npm package name, used for logging.</param>
		/// <param name="versions">The published versions of the package.</param>
		/// <param name="requirements">Blank for the latest version, a published version number, or an npm version range such as "^4.17.0".</param>
		/// <returns>The version number to use, or null if no version could be found.</returns>
		private static string? SelectVersion( string packageName, List<string> versions, string requirements )
		{
			string trimmed_requirements = requirements.Trim();

			if( trimmed_requirements.Length == 0 )
			{
				// The registry lists its versions in publication order rather than version order, and a patch to an older release line can be published
				// after a newer one, so the highest version is found by comparing them rather than by taking the last entry.
				NpmVersion? latest_version = NpmVersionMatcher.SelectMatchingVersion( packageName, versions, string.Empty );
				if( latest_version != null )
				{
					return latest_version.ToString();
				}

				ConsoleLogger.Warning( $"No stable version found for the '{packageName}' npm package; falling back to most recently published" );
				string? published_version = versions.LastOrDefault();
				if( published_version == null )
				{
					ConsoleLogger.Error( $"No valid versions found." );
					return null;
				}

				return published_version;
			}

			// A published version number is used as is, which also allows a prerelease to be requested by name
			string? exact_version = versions.Find( x => x == trimmed_requirements );
			if( exact_version != null )
			{
				return exact_version;
			}

			NpmVersion? matching_version = NpmVersionMatcher.SelectMatchingVersion( packageName, versions, trimmed_requirements );
			return matching_version?.ToString();
		}

		/// <summary>
		/// Looks up an npm package's metadata from the registry: resolves the version to use from <paramref name="requirements"/>, and reads its author,
		/// license, homepage, repository, and tarball URL.
		/// </summary>
		/// <param name="packageName">The npm package name, e.g. "left-pad".</param>
		/// <param name="requirements">
		/// Blank to use the latest non-prerelease version; a specific version number such as "4.17.21" (a prerelease can be requested this way); or an npm
		/// version range such as "^4.17.0", "~1.2.3" or "&gt;=2 &lt;4", which selects the highest non-prerelease version that satisfies it.
		/// </param>
		/// <param name="isRootPackage">True if this is the package being checked, rather than one of its dependencies, so its dependencies are collected.</param>
		/// <returns>The populated package, or null if the package, a version satisfying the requirements, or that version's metadata could not be retrieved.</returns>
		internal static NpmPackage? GetNpmPackage( string packageName, string requirements, bool isRootPackage )
		{
			string get_request = $"https://registry.npmjs.org/{packageName}";
			string content = PackageCheck.Request( get_request ).Result;
			if( string.IsNullOrEmpty( content ) )
			{
				ConsoleLogger.Error( $"Failed to retrieve data about the '{packageName}' package from npmjs.org" );
				return null;
			}

			NpmPackageRaw? package;
			try
			{
				package = JsonSerializer.Deserialize<NpmPackageRaw>( content );
			}
			catch( JsonException json_exception )
			{
				ConsoleLogger.Error( $"Exception during deserialization of npm package '{packageName}' with exception '{json_exception.Message}'" );
				return null;
			}

			if( package == null || package.versions == null )
			{
				return null;
			}

			string? version = SelectVersion( packageName, package.versions.Keys.ToList(), requirements );
			if( version == null )
			{
				return null;
			}

			if( !package.versions.TryGetValue( version, out NpmPackageVersion? requested_version ) )
			{
				ConsoleLogger.Error( $"Version '{version}' not found for package '{packageName}'" );
				return null;
			}

			DateTime publish_date = DateTime.MinValue;
			if( package.time != null && package.time.TryGetValue( version, out string? version_timestamp ) )
			{
				DateTime.TryParse( version_timestamp, out publish_date );
			}

			NpmPackage npm_package = new NpmPackage
			{
				Name = package.name,
				Version = version,
				VersionCreatedAt = publish_date,
				Authors = package.author ?? string.Empty,
				Copyright = string.Empty,
				HomepageUri = package.homepage ?? string.Empty,
				SourceCodeUri = package.repository?.url ?? string.Empty,
				DownloadUri = requested_version.dist?.tarball ?? string.Empty,
				Description = PackageCheck.SanitizeString( package.description )
			};

			// Add license
			npm_package.AddLicense( new LicenseInfo( package.license ?? string.Empty ) );

			if( isRootPackage )
			{
				// Add dependencies
				npm_package.AddAllDependencies( requested_version );
			}

			return npm_package;
		}

		protected override void UpdateDependency( PackageDependency dependency )
		{
			NpmPackage? npm_package = GetNpmPackage( dependency.Name, dependency.Version, false );
			if( npm_package != null )
			{
				npm_package.CleanupDependencyData();
				npm_package.SetDependencyLicense( dependency );
			}
		}

		/// <summary>
		/// Looks up the given npm package, cleans up its data (licenses, copyright, fallback URIs), and prints its details to the console. Does nothing if the
		/// package or the requested version could not be found.
		/// </summary>
		/// <param name="packageName">The npm package name, e.g. "left-pad".</param>
		/// <param name="version">The specific version to check, an npm version range such as "^4.17.0", or "" to use the latest published version.</param>
		public static void CheckNpmPackage( string packageName, string version )
		{
			NpmPackage? npm_package = GetNpmPackage( packageName, version, true );
			if( npm_package != null )
			{
				npm_package.CleanupData();
				npm_package.PrintDetails( PackageType );
			}
		}
	}
}
