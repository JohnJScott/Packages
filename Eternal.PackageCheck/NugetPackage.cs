// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Eternal.PackageCheck
{
	public class NuGetRepositoryInfo
	{
		// public string type = string.Empty;
		public string url = string.Empty;
		// public string branch = string.Empty;
		// public string commit = string.Empty;
	}

	/// <summary>
	/// NuGet's catalog "repository" field is either a bare empty string (packages that never set RepositoryUrl) or an object ( { "type", "url", "branch",
	/// "commit" } ) for packages that did. Both forms are reduced to this type.
	/// </summary>
	public sealed class NuGetRepositoryConverter
		: JsonConverter<NuGetRepositoryInfo>
	{
		/// <inheritdoc/>
		public override NuGetRepositoryInfo Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
		{
			if( reader.TokenType == JsonTokenType.Null || reader.TokenType == JsonTokenType.String )
			{
				// The empty-string case ("" when no repository metadata was set).
				return new NuGetRepositoryInfo();
			}

			if( reader.TokenType == JsonTokenType.StartObject )
			{
				JsonElement repository_element = JsonElement.ParseValue( ref reader );

				return new NuGetRepositoryInfo
				{
					// type = repository_element.TryGetProperty( "type", out JsonElement type_property ) ? type_property.GetString() ?? string.Empty : string.Empty,
					url = repository_element.TryGetProperty( "url", out JsonElement url_property ) ? url_property.GetString() ?? string.Empty : string.Empty,
					// branch = repository_element.TryGetProperty( "branch", out JsonElement branch_property ) ? branch_property.GetString() ?? string.Empty : string.Empty,
					// commit = repository_element.TryGetProperty( "commit", out JsonElement commit_property ) ? commit_property.GetString() ?? string.Empty : string.Empty
				};
			}

			reader.Skip();
			return new NuGetRepositoryInfo();
		}

		/// <inheritdoc/>
		public override void Write( Utf8JsonWriter writer, NuGetRepositoryInfo value, JsonSerializerOptions options )
		{
			writer.WriteStringValue( value.url );
		}
	}

	/// <summary>
	/// The response from https://api.nuget.org/v3-flatcontainer/{id}/index.json - every published version, oldest first. NOT filtered to stable releases:
	/// prerelease versions (e.g. "13.0.5-beta1") can appear after the latest stable one, so the last array entry is not necessarily the latest stable.
	/// </summary>
	public class NuGetFlatContainerVersions
	{
		public List<string> versions = new List<string>();
	}

	/// <summary>
	/// The response from
	/// https://api.nuget.org/v3/registration5-semver1/{id}/{version}.json - a thin permalink wrapper, not the package metadata itself. "catalogEntry"
	/// must be fetched separately to get the actual rich details.
	/// </summary>
	public class NuGetRegistrationLeaf
	{
		public string catalogEntry = string.Empty;
		public string packageContent = string.Empty;
		// public bool listed = false;
		// public DateTime published = DateTime.MinValue;
	}

	/// <summary>
	/// A single dependency within a dependency group - the package id required and the version range accepted. "range" is NuGet interval notation
	/// (e.g. "[8.0.1, )" for a minimum, "[2.0.1, 3.0.0)" for an exclusive upper bound, "[4.0.0, 4.0.0]" for an exact pin) rather than a bare version
	/// number, and is absent for a dependency that accepts any version.
	/// </summary>
	public class NuGetPackageDependency
	{
		public string id { get; set; } = string.Empty;
		public string range { get; set; } = string.Empty;
	}

	/// <summary>
	/// The dependencies required for one target framework. "targetFramework" is absent on the legacy "any framework" group, and "dependencies" is
	/// absent for a framework that is supported but needs nothing (most of Newtonsoft.Json's groups are of that form), so both default to empty.
	/// </summary>
	public class NuGetPackageDependencyGroup
	{
		public string targetFramework { get; set; } = string.Empty;
		public List<NuGetPackageDependency> dependencies { get; set; } = new List<NuGetPackageDependency>();
	}

	/// <summary>
	/// The actual rich package metadata, fetched from the URL named by NuGetRegistrationLeaf.catalogEntry - authors, license, project URL,
	/// dependencies, etc.
	/// </summary>
	public class NuGetCatalogEntry
	{
		public string id { get; set; }= string.Empty;
		public string version { get; set; } = string.Empty;
		public string authors { get; set; } = string.Empty;
		public string title { get; set; } = string.Empty;
		public string description { get; set; } = string.Empty;
		public string licenseExpression { get; set; } = string.Empty;
		public string licenseUrl { get; set; } = string.Empty;
		public string projectUrl { get; set; } = string.Empty;
		public string copyright { get; set; } = string.Empty;
		public bool listed { get; set; } = false;
		public DateTime published { get; set; } = DateTime.MinValue;
		public long packageSize { get; set; } = 0;
		[JsonPropertyName( "repository" )]
		[JsonConverter( typeof( NuGetRepositoryConverter ) )]
		public NuGetRepositoryInfo repository { get; set; } = new NuGetRepositoryInfo();
		public List<NuGetPackageDependencyGroup> dependencyGroups { get; set; } = new List<NuGetPackageDependencyGroup>();
	}

	internal class NuGetPackage
		: GenericPackage
	{
		private void AddLicense( LicenseInfo licenseInfo )
		{
			Licenses.Add( licenseInfo );
		}

		/// <summary>
		/// Merges the catalog's per-target-framework dependency groups into a single flat list of dependencies.
		/// </summary>
		/// <param name="dependencyGroups">The "dependencyGroups" array from the catalog entry; empty for a package that declares no dependencies at all.</param>
		private void AddDependencies( List<NuGetPackageDependencyGroup> dependencyGroups )
		{
			foreach( NuGetPackageDependencyGroup dependency_group in dependencyGroups )
			{
				if( !Enum.TryParse( dependency_group.targetFramework.Replace( ".", "" ), out EDependencyType dependency_type ) )
				{
					dependency_type = EDependencyType.Unknown;
				}

				foreach( NuGetPackageDependency dependency in dependency_group.dependencies )
				{
					if( string.IsNullOrEmpty( dependency.id ) )
					{
						continue;
					}

					PackageDependency package_dependency = new PackageDependency()
					{
						Name = dependency.id,
						Version = dependency.range,
						DependencyType = dependency_type
					};

					AddSingleDependency( package_dependency );
				}
			}
		}

		/// <summary>
		/// Delegates to the base class's license scraping and copyright cleanup; NuGet packages need no additional fallback data beyond what the catalog provides.
		/// </summary>
		internal void CleanupData()
		{
			base.CleanupData( "NuGet" );
		}

		/// <summary>
		/// Looks up a NuGet package's metadata from nuget.org: finds the requested version (or the latest stable one if <paramref name="version"/> is empty, skipping
		/// prerelease versions), then follows the registration leaf's catalog entry link to read its authors, license, project URL, repository URL, and download URL.
		/// </summary>
		/// <param name="packageName">The NuGet package id, e.g. "Newtonsoft.Json".</param>
		/// <param name="version">The specific version to fetch, or "" to use the latest stable version.</param>
		/// <returns>The populated package, or null if the package, version, or catalog entry could not be retrieved.</returns>
		internal static NuGetPackage? GetNuGetPackage( string packageName, string version, bool isRootPackage )
		{
			// NuGet's v3 API requires the package id lowercased in every URL segment.
			string versions_request = $"https://api.nuget.org/v3-flatcontainer/{packageName.ToLowerInvariant()}/index.json";
			string versions_content = PackageCheck.Request( versions_request ).Result;
			if( string.IsNullOrEmpty( versions_content ) )
			{
				ConsoleLogger.Error( $"Failed to retrieve data about the '{packageName}' package from NuGet.org" );
				return null;
			}

			NuGetFlatContainerVersions? versions = JsonHelper.ReadJson<NuGetFlatContainerVersions>( versions_content );
			if( versions == null || versions.versions.Count == 0 )
			{
				ConsoleLogger.Error( $"No published versions found for the '{packageName}' NuGet package" );
				return null;
			}

			string? requested_version;
			if( string.IsNullOrEmpty( version ) )
			{
				// The flat container list is not filtered to stable releases, and prerelease versions can be published after the latest stable one -
				// skip anything containing '-' (the semver prerelease marker) rather than trusting the last array entry.
				requested_version = versions.versions.LastOrDefault( v => !v.Contains( '-' ) );
				if( requested_version == null )
				{
					ConsoleLogger.Warning( $"No stable version found for the '{packageName}' NuGet package; falling back to latest prerelease" );
					requested_version = versions.versions[^1];
				}
			}
			else
			{
				requested_version = versions.versions.Find( v => ( v == version ) );
				if( requested_version == null )
				{
					ConsoleLogger.Error( $"Version '{version}' not found for the '{packageName}' NuGet package." );
					return null;
				}
			}

			string leaf_request = $"https://api.nuget.org/v3/registration5-semver1/{packageName.ToLowerInvariant()}/{requested_version.ToLowerInvariant()}.json";
			string leaf_content = PackageCheck.Request( leaf_request ).Result;
			if( string.IsNullOrEmpty( leaf_content ) )
			{
				return null;
			}

			NuGetRegistrationLeaf? leaf = JsonHelper.ReadJson<NuGetRegistrationLeaf>( leaf_content );
			if( leaf == null )
			{
				return null;
			}

			// The leaf is only a permalink wrapper - the actual metadata (license,
			// authors, dependencies, etc.) lives at the URL it names.
			string catalog_content = PackageCheck.Request( leaf.catalogEntry ).Result;
			if( string.IsNullOrEmpty( catalog_content ) )
			{
				return null;
			}

			NuGetCatalogEntry? catalog_entry;
			try
			{
				catalog_entry = JsonSerializer.Deserialize<NuGetCatalogEntry>( catalog_content );
			}
			catch( JsonException json_exception )
			{
				ConsoleLogger.Error( $"Exception during deserialization of NuGet catalog for '{packageName}' with exception '{json_exception.Message}'" );
				return null;
			}

			if( catalog_entry == null )
			{
				return null;
			}

			string description = catalog_entry.description;
			if( !string.IsNullOrEmpty( catalog_entry.title ) )
			{
				description = catalog_entry.title + " - " + description;
			}

			NuGetPackage nuget_package = new NuGetPackage
			{
				Name = catalog_entry.id,
				Version = catalog_entry.version,
				VersionCreatedAt = catalog_entry.published,
				Authors = catalog_entry.authors,
				Copyright = catalog_entry.copyright,
				HomepageUri = catalog_entry.projectUrl,
				SourceCodeUri = catalog_entry.repository.url,
				DownloadUri = leaf.packageContent,
				Description = PackageCheck.SanitizeString( description )
			};

			nuget_package.AddLicense( new LicenseInfo( catalog_entry.licenseExpression ) );

			if( isRootPackage )
			{
				nuget_package.AddDependencies( catalog_entry.dependencyGroups );
			}

			return nuget_package;
		}

		protected override void UpdateDependency( PackageDependency dependency )
		{
			NuGetPackage? nuget_package = GetNuGetPackage( dependency.Name, "", false );
			if( nuget_package == null )
			{
				return;
			}

			nuget_package.CleanupDependencyData( "NuGet" );

			base.UpdateDependency( dependency );
		}

		/// <summary>
		/// Looks up the given NuGet package, cleans up its data (licenses, copyright, fallback URIs), and prints its details to the console. Does nothing if the
		/// package or the requested version could not be found.
		/// </summary>
		/// <param name="packageName">The NuGet package id, e.g. "Newtonsoft.Json".</param>
		/// <param name="version">The specific version to check, or "" to use the latest stable version.</param>
		public static void CheckNuGetPackage( string packageName, string version )
		{
			NuGetPackage? nuget_package = GetNuGetPackage( packageName, version, true );
			if( nuget_package == null )
			{
				return;
			}

			nuget_package.CleanupData();

			nuget_package.PrintDetails( "NuGet" );
		}
	}
}
