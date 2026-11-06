// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Eternal.PackageCheck
{
	/// <summary>
	/// What an SBOM says a component is. This is the union of the CycloneDX "component.type" and SPDX "primaryPackagePurpose" vocabularies, which overlap heavily
	/// but not completely: CycloneDX contributes Platform, DeviceDriver, MachineLearningModel, Data, and CryptographicAsset, while SPDX contributes Source,
	/// Archive, Install, and Other. The value is self-declared by whatever produced the SBOM and is optional in SPDX, so treat it as advisory.
	/// </summary>
	[JsonConverter( typeof( StringEnumConverter ) )]
	internal enum EComponentType
	{
		/// <summary>The SBOM declared no type, or one neither specification defines.</summary>
		Unknown = -1,
		Application,
		Framework,
		Library,
		Container,
		Platform,
		OperatingSystem,
		Device,
		DeviceDriver,
		Firmware,
		File,
		MachineLearningModel,
		Data,
		CryptographicAsset,
		Source,
		Archive,
		Install,
		Other
	}

	/// <summary>
	/// A single package identified from an SBOM - what is needed to look it up in its registry, plus what the SBOM claims it is. Shared by every SBOM format, as
	/// they all ultimately identify a package by its Package URL.
	/// </summary>
	internal class SbomPackage
	{
		/// <summary>The canonical package type, matching the PackageType constant of the class that handles it, e.g. "NuGet" or "RubyGems".</summary>
		public string PackageType = string.Empty;

		/// <summary>What the SBOM declares the component to be, e.g. a library or the root application. Unknown when the SBOM declared nothing usable.</summary>
		public EComponentType ComponentType = EComponentType.Unknown;

		/// <summary>The package name as its registry knows it, including any namespace, e.g. "@babel/core" or "github.com/gin-gonic/gin".</summary>
		public string Name = string.Empty;

		/// <summary>The exact version, e.g. "13.0.4".</summary>
		public string Version = string.Empty;

		public override string ToString()
		{
			return $"{PackageType} {Name} {Version} ({ComponentType})";
		}
	}

	/// <summary>
	/// Reads the component type an SBOM declares for a package.
	/// </summary>
	internal static class ComponentType
	{
		/// <summary>
		/// Both specifications spell their vocabularies the same way apart from case - CycloneDX uses "operating-system" where SPDX uses "OPERATING-SYSTEM" - so
		/// one case insensitive table covers both. No string means different things in the two specifications, so there is nothing to disambiguate.
		/// </summary>
		private static readonly Dictionary<string, EComponentType> DeclaredTypes = new Dictionary<string, EComponentType>( StringComparer.OrdinalIgnoreCase )
		{
			{ "application", EComponentType.Application },
			{ "framework", EComponentType.Framework },
			{ "library", EComponentType.Library },
			{ "container", EComponentType.Container },
			{ "platform", EComponentType.Platform },
			{ "operating-system", EComponentType.OperatingSystem },
			{ "device", EComponentType.Device },
			{ "device-driver", EComponentType.DeviceDriver },
			{ "firmware", EComponentType.Firmware },
			{ "file", EComponentType.File },
			{ "machine-learning-model", EComponentType.MachineLearningModel },
			{ "data", EComponentType.Data },
			{ "cryptographic-asset", EComponentType.CryptographicAsset },
			{ "source", EComponentType.Source },
			{ "archive", EComponentType.Archive },
			{ "install", EComponentType.Install },
			{ "other", EComponentType.Other }
		};

		/// <summary>
		/// Converts the type an SBOM declares into its enumerated form.
		/// </summary>
		/// <param name="declaredType">The raw CycloneDX "type" or SPDX "primaryPackagePurpose" value, which may be null or empty.</param>
		/// <returns>The component type, or Unknown if nothing usable was declared.</returns>
		internal static EComponentType Parse( string? declaredType )
		{
			if( string.IsNullOrEmpty( declaredType ) )
			{
				return EComponentType.Unknown;
			}

			if( !DeclaredTypes.TryGetValue( declaredType.Trim(), out EComponentType component_type ) )
			{
				// Worth saying out loud, as it means the SBOM was written against a newer specification than this understands
				ConsoleLogger.Warning( $"Unrecognized component type '{declaredType}'." );
				return EComponentType.Unknown;
			}

			return component_type;
		}
	}

	/// <summary>
	/// Reads Package URLs, the identifier every SBOM format uses to name a package and the ecosystem it came from.
	/// </summary>
	internal static class PackageUrl
	{
		private static readonly string SupportedVendor = "debian";

		/// <summary>
		/// Maps a Package URL type onto this tool's canonical package type. purl is yet another naming of the same ecosystems - it calls RubyGems "gem" and Go
		/// "golang" - so the mapping is explicit rather than derived. See https://github.com/package-url/purl-spec for the registered types.
		/// </summary>
		private static readonly Dictionary<string, string> PurlTypeToPackageType = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase )
		{
			{ "nuget", NuGetPackage.PackageType },
			{ "npm", NpmPackage.PackageType },
			{ "gem", RubyGem.PackageType },
			{ "golang", GoPackage.PackageType },
			{ "pypi", PythonPackage.PackageType },
			{ "cargo", CargoPackage.PackageType },
			{ "deb", DebPackage.PackageType }
		};

		/// <summary>
		/// Splits a Package URL into the parts this tool needs. The grammar is "pkg:type/namespace/name@version?qualifiers#subpath", where the namespace is optional
		/// and may itself contain slashes, and every segment may be percent encoded.
		/// </summary>
		/// <param name="purl">The purl to parse, e.g. "pkg:golang/github.com%2Fgin-gonic/gin@v1.9.1".</param>
		/// <param name="purlType">Receives the raw purl type, e.g. "golang".</param>
		/// <param name="name">Receives the decoded namespace and name joined by a slash, e.g. "github.com/gin-gonic/gin".</param>
		/// <param name="version">Receives the decoded version, or "" when the purl carries none.</param>
		/// <returns>true if the purl could be parsed.</returns>
		internal static bool TryParse( string purl, out string purlType, out string name, out string version )
		{
			purlType = string.Empty;
			name = string.Empty;
			version = string.Empty;

			if( !purl.StartsWith( "pkg:", StringComparison.OrdinalIgnoreCase ) )
			{
				return false;
			}

			string remainder = purl[4..];

			// Qualifiers and the subpath are not needed, and the subpath always follows the qualifiers
			int subpath_index = remainder.IndexOf( '#' );
			if( subpath_index >= 0 )
			{
				remainder = remainder[..subpath_index];
			}

			int qualifiers_index = remainder.IndexOf( '?' );
			if( qualifiers_index >= 0 )
			{
				remainder = remainder[..qualifiers_index];
			}

			// The version is whatever follows the last '@', which cannot appear unencoded anywhere earlier
			int version_index = remainder.LastIndexOf( '@' );
			if( version_index >= 0 )
			{
				version = Uri.UnescapeDataString( remainder[( version_index + 1 )..] );
				remainder = remainder[..version_index];
			}

			int type_index = remainder.IndexOf( '/' );
			if( type_index <= 0 )
			{
				return false;
			}

			purlType = remainder[..type_index];

			// Everything after the type is the namespace and name. Decoding each segment separately keeps an encoded slash inside a segment from being confused
			// with a separator, which matters for Go module paths and scoped npm packages.
			string[] segments = remainder[( type_index + 1 )..].Split( '/', StringSplitOptions.RemoveEmptyEntries );
			if( segments.Length == 0 )
			{
				return false;
			}

			name = string.Join( '/', segments.Select( Uri.UnescapeDataString ) );
			return name.Length > 0;
		}

		/// <summary>
		/// Reduces a Package URL, plus whatever the SBOM entry states separately, to a package type, name, and version.
		/// </summary>
		/// <param name="purl">The purl from the SBOM entry, which may be null or empty if it declared none.</param>
		/// <param name="fallbackName">The name the SBOM entry states in its own field, used when the purl omits it.</param>
		/// <param name="fallbackVersion">The version the SBOM entry states in its own field, used when the purl omits it.</param>
		/// <param name="declaredType">The CycloneDX "type" or SPDX "primaryPackagePurpose" the SBOM entry declares, if any.</param>
		/// <returns>The package, or null if there is no usable purl or its type is not one this tool handles.</returns>
		internal static SbomPackage? GetPackage( string? purl, string fallbackName, string? fallbackVersion, string? declaredType )
		{
			if( string.IsNullOrEmpty( purl ) )
			{
				ConsoleLogger.Warning( $"Package '{fallbackName}' has no purl; skipping it." );
				return null;
			}

			if( !TryParse( purl, out string purl_type, out string purl_name, out string purl_version ) )
			{
				ConsoleLogger.Warning( $"Could not parse the purl '{purl}'; skipping it." );
				return null;
			}

			if( !PurlTypeToPackageType.TryGetValue( purl_type, out string? package_type ) )
			{
				ConsoleLogger.Warning( $"Unsupported purl type '{purl_type}' in '{purl}'; skipping it." );
				return null;
			}

			if( purl_type == "deb" )
			{
				int vendor_index = purl_name.LastIndexOf( '/' );
				if( vendor_index >= 0 )
				{
					string vendor = purl_name[..vendor_index];
					if( !string.Equals( vendor, SupportedVendor, StringComparison.OrdinalIgnoreCase ) )
					{
						ConsoleLogger.Warning( $"'{purl}' is from '{vendor}', but only the '{SupportedVendor}' archive is consulted." );
					}

					purl_name = purl_name[( vendor_index + 1 )..];
				}
			}

			// The purl is authoritative for the name because it spells out the namespace, which the SBOM entry's own name field often omits. Those fields are only
			// used when the purl leaves them out.
			if( purl_name.Length == 0 )
			{
				purl_name = fallbackName;
			}

			if( purl_version.Length == 0 )
			{
				purl_version = fallbackVersion ?? string.Empty;
			}

			if( purl_version.Length == 0 )
			{
				ConsoleLogger.Warning( $"Package '{purl_name}' has no version; skipping it." );
				return null;
			}

			return new SbomPackage
			{
				PackageType = package_type,
				ComponentType = ComponentType.Parse( declaredType ),
				Name = purl_name,
				Version = purl_version
			};
		}

		/// <summary>
		/// Writes a .tps file for every package read from an SBOM, into a sub-directory named after its component type. A package whose .tps already exists is
		/// skipped, so an interrupted run can be resumed, and the whole loop stops as soon as a host starts rate limiting rather than writing partial data.
		/// </summary>
		/// <param name="packages">The packages read from the SBOM.</param>
		internal static void GenerateTpsFiles( List<SbomPackage> packages )
		{
			if( packages.Count == 0 )
			{
				ConsoleLogger.Warning( "No packages to process" );
				return;
			}

			ConsoleLogger.Log( $"Generating TPS files for {packages.Count} packages" );

			foreach( SbomPackage package in packages )
			{
				if( !PackageCheck.RateLimitExceeded )
				{
					string tps_name = GenericPackage.GetTpsName( package.PackageType, package.Name, package.Version );
					if( !File.Exists( tps_name ) )
					{
						PackageCheck.ProcessPackage( package.PackageType.ToLower(), package.Name, package.Version );
					}
				}
				else
				{
					ConsoleLogger.Error( "Rate limit exceeded; aborting to avoid writing partial TPS files" );
					break;
				}
			}
		}
	}
}
