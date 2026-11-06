// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;

namespace Eternal.PackageCheck
{
	/// <summary>
	/// One entry of a CycloneDX BOM's "components" array. Only the fields needed to identify the package are declared; everything else a BOM carries - hashes,
	/// licenses, external references, and so on - is ignored, as this tool derives that from the registries instead.
	/// </summary>
	internal class CycloneDXComponent
	{
		public string? type = string.Empty;
		public string name = string.Empty;
		public string? version = string.Empty;
		public string? purl = string.Empty;
	}

	/// <summary>
	/// The subset of a CycloneDX BOM document that identifies its components. Matches the 1.4, 1.5, and 1.6 JSON schemas, which did not change the shape of the
	/// fields read here.
	/// </summary>
	internal class CycloneDXBom
	{
		public string? bomFormat = string.Empty;
		public string? specVersion = string.Empty;
		public List<CycloneDXComponent>? components = new List<CycloneDXComponent>();
	}

	internal static class CycloneDX
	{
		/// <summary>
		/// Extracts the package type, name, and version of every component of a CycloneDX BOM held in memory.
		/// </summary>
		/// <param name="content">The BOM's JSON.</param>
		/// <returns>The packages found, which is empty if the BOM could not be read or lists no component this tool handles.</returns>
		internal static List<SbomPackage> ReadBom( string content )
		{
			List<SbomPackage> packages = new List<SbomPackage>();

			CycloneDXBom? bom = JsonHelper.ReadJson<CycloneDXBom>( content );
			if( bom == null )
			{
				ConsoleLogger.Error( "Failed to deserialize the CycloneDX BOM" );
				return packages;
			}

			if( !string.IsNullOrEmpty( bom.bomFormat ) && bom.bomFormat != "CycloneDX" )
			{
				ConsoleLogger.Warning( $"'{bom.bomFormat}' is not a CycloneDX BOM." );
				return packages;
			}

			if( bom.components == null )
			{
				ConsoleLogger.Warning( "The CycloneDX BOM lists no components." );
				return packages;
			}

			foreach( CycloneDXComponent component in bom.components )
			{
				SbomPackage? package = PackageUrl.GetPackage( component.purl, component.name, component.version, component.type );
				if( package != null )
				{
					packages.Add( package );
				}
			}

			return packages;
		}

		/// <summary>
		/// Extracts the package type, name, and version of every component of a CycloneDX BOM file, such as the .sbom.json produced by building with
		/// &lt;GenerateSbom&gt;.
		/// </summary>
		/// <param name="bomPath">The path of the BOM to read.</param>
		/// <returns>The packages found, which is empty if the file is missing, unreadable, or lists no component this tool handles.</returns>
		internal static List<SbomPackage> ReadBomFile( string bomPath )
		{
			if( !File.Exists( bomPath ) )
			{
				ConsoleLogger.Error( $"CycloneDX BOM '{bomPath}' does not exist" );
				return [];
			}

			return ReadBom( File.ReadAllText( bomPath ) );
		}
	}
}
