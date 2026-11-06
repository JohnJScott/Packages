// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;

namespace Eternal.PackageCheck
{
	/// <summary>
	/// One entry of an SPDX package's "externalRefs" array. The Package URL lives in here rather than in a field of its own, so this is the only way an SPDX
	/// document states which ecosystem a package came from.
	/// </summary>
	internal class SpdxExternalRef
	{
		public string? referenceCategory = string.Empty;
		public string? referenceType = string.Empty;
		public string? referenceLocator = string.Empty;
	}

	/// <summary>
	/// One entry of an SPDX document's "packages" array. Only the fields needed to identify the package are declared; the licensing and checksum fields an SPDX
	/// document carries are ignored, as this tool derives that from the registries instead.
	/// </summary>
	internal class SpdxPackageRaw
	{
		public string? SPDXID = string.Empty;
		public string name = string.Empty;
		public string? versionInfo = string.Empty;

		/// <summary>SPDX's equivalent of the CycloneDX component type. Added in 2.3 and optional, so plenty of real documents omit it.</summary>
		public string? primaryPackagePurpose = string.Empty;

		public List<SpdxExternalRef>? externalRefs = new List<SpdxExternalRef>();
	}

	/// <summary>
	/// The subset of an SPDX document that identifies its packages. Matches the 2.2 and 2.3 JSON schemas, which did not change the shape of the fields read here.
	/// SPDX 3.0 replaced the document with a JSON-LD graph and is not handled.
	/// </summary>
	internal class SpdxDocument
	{
		public string? spdxVersion = string.Empty;
		public string? SPDXID = string.Empty;
		public string? name = string.Empty;
		public List<SpdxPackageRaw>? packages = new List<SpdxPackageRaw>();
	}

	internal static class SPDX
	{
		/// <summary>The reference type that marks an external reference as carrying a Package URL.</summary>
		private const string PurlReferenceType = "purl";

		/// <summary>
		/// The category an SPDX document gives a package manager reference. The 2.2 schema permits both spellings and real documents use either, so both are
		/// accepted; 2.3 settled on the hyphenated form.
		/// </summary>
		private static readonly string[] PackageManagerCategories = [ "PACKAGE-MANAGER", "PACKAGE_MANAGER" ];

		/// <summary>
		/// Finds the Package URL an SPDX package declares among its external references.
		/// </summary>
		/// <param name="package">The SPDX package to search.</param>
		/// <returns>The purl, or null if the package declares none.</returns>
		private static string? GetPurl( SpdxPackageRaw package )
		{
			if( package.externalRefs == null )
			{
				return null;
			}

			foreach( SpdxExternalRef external_ref in package.externalRefs )
			{
				if( !string.Equals( external_ref.referenceType, PurlReferenceType, StringComparison.OrdinalIgnoreCase ) )
				{
					continue;
				}

				// The category is checked only when it is present, as it adds nothing once the reference type says purl and some generators omit it
				if( !string.IsNullOrEmpty( external_ref.referenceCategory )
				    && !PackageManagerCategories.Contains( external_ref.referenceCategory, StringComparer.OrdinalIgnoreCase ) )
				{
					continue;
				}

				if( !string.IsNullOrEmpty( external_ref.referenceLocator ) )
				{
					return external_ref.referenceLocator;
				}
			}

			return null;
		}

		/// <summary>
		/// Extracts the package type, name, and version of every package in an SPDX document held in memory.
		/// </summary>
		/// <param name="content">The document's JSON.</param>
		/// <returns>The packages found, which is empty if the document could not be read or lists no package this tool handles.</returns>
		internal static List<SbomPackage> ReadBom( string content )
		{
			List<SbomPackage> packages = new List<SbomPackage>();

			SpdxDocument? document = JsonHelper.ReadJson<SpdxDocument>( content );
			if( document == null )
			{
				ConsoleLogger.Error( "Failed to deserialize the SPDX document" );
				return packages;
			}

			if( string.IsNullOrEmpty( document.spdxVersion ) )
			{
				ConsoleLogger.Warning( "The document declares no spdxVersion; it may not be an SPDX document." );
			}
			else if( !document.spdxVersion.StartsWith( "SPDX-2.", StringComparison.OrdinalIgnoreCase ) )
			{
				// SPDX 3.0 is a JSON-LD graph with an "@graph" of elements rather than a "packages" array, so none of the fields read here exist in it
				ConsoleLogger.Error( $"'{document.spdxVersion}' is not supported; only SPDX 2.x documents can be read." );
				return packages;
			}

			if( document.packages == null )
			{
				ConsoleLogger.Warning( "The SPDX document lists no packages." );
				return packages;
			}

			foreach( SpdxPackageRaw spdx_package in document.packages )
			{
				SbomPackage? package = PackageUrl.GetPackage( GetPurl( spdx_package ), spdx_package.name, spdx_package.versionInfo, spdx_package.primaryPackagePurpose );
				if( package != null )
				{
					packages.Add( package );
				}
			}

			return packages;
		}

		/// <summary>
		/// Extracts the package type, name, and version of every package in an SPDX document file, such as the manifest.spdx.json produced by the Microsoft SBOM
		/// tool or exported from a GitHub repository's dependency graph.
		/// </summary>
		/// <param name="documentPath">The path of the SPDX document to read.</param>
		/// <returns>The packages found, which is empty if the file is missing, unreadable, or lists no package this tool handles.</returns>
		internal static List<SbomPackage> ReadBomFile( string documentPath )
		{
			if( !File.Exists( documentPath ) )
			{
				ConsoleLogger.Error( $"SPDX document '{documentPath}' does not exist" );
				return [];
			}

			return ReadBom( File.ReadAllText( documentPath ) );
		}
	}
}
