// Copyright Eternal Developments LLC. All Rights Reserved.

using Eternal.PackageCheck;

[assembly: Parallelize( Scope = ExecutionScope.MethodLevel )]

namespace Eternal.PackageCheckTest
{
	[TestClass]
	public sealed class PackageCheckTests
	{
		[TestMethod]
		public void TestGoPackage()
		{
			string package_name = "golang.org/x/sys";
			GoPackage? raw_go_package = GoPackage.GetGoPackage( package_name, "", true );
			Assert.IsNotNull( raw_go_package, $"Failed to get Go package '{package_name}'" );
			Assert.AreEqual( "v0.48.0", raw_go_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_go_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "BSD-3-Clause", raw_go_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_go_package.CleanupData();
			
			Assert.AreEqual( "https://pkg.go.dev/golang.org/x/sys@v0.48.0", raw_go_package.HomepageUri, "Homepage URL mismatch" );
			Assert.AreEqual( "https://proxy.golang.org/golang.org/x/sys/@v/v0.48.0.zip", raw_go_package.DownloadUri, "Download URL mismatch" );
			Assert.AreEqual( "https://go.googlesource.com/sys/+/refs/tags/v0.48.0/LICENSE", raw_go_package.Licenses[0].HtmlLicenseUri, "HTML license URL mismatch" );

			raw_go_package.WriteTpsFile( "Go" );
		}

		[TestMethod]
		public void TestGoPackageMIT0()
		{
			string package_name = "github.com/segmentio/asm";
			GoPackage? raw_go_package = GoPackage.GetGoPackage( package_name, "", true );
			Assert.IsNotNull( raw_go_package, $"Failed to get Go package '{package_name}'" );
			Assert.AreEqual( "v1.2.1", raw_go_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_go_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "MIT-0", raw_go_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_go_package.CleanupData();

			Assert.AreEqual( "https://pkg.go.dev/github.com/segmentio/asm@v1.2.1", raw_go_package.HomepageUri, "Homepage URL mismatch" );
			Assert.AreEqual( "https://proxy.golang.org/github.com/segmentio/asm/@v/v1.2.1.zip", raw_go_package.DownloadUri, "Download URL mismatch" );
			Assert.AreEqual( "https://github.com/segmentio/asm/blob/main/LICENSE", raw_go_package.Licenses[0].HtmlLicenseUri, "HTML license URL mismatch" );

			raw_go_package.WriteTpsFile( "Go" );
		}

		[TestMethod]
		public void TestGoPackageWithDependencies()
		{
			string package_name = "modernc.org/sqlite";
			GoPackage? raw_go_package = GoPackage.GetGoPackage( package_name, "", true );
			Assert.IsNotNull( raw_go_package, $"Failed to get Go package '{package_name}'" );
			Assert.AreEqual( "v1.58.0", raw_go_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_go_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "BSD-3-Clause", raw_go_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_go_package.CleanupData();

			Assert.AreEqual( "https://pkg.go.dev/modernc.org/sqlite@v1.58.0", raw_go_package.HomepageUri, "Homepage URL mismatch" );
			Assert.AreEqual( "https://proxy.golang.org/modernc.org/sqlite/@v/v1.58.0.zip", raw_go_package.DownloadUri, "Download URL mismatch" );
			Assert.AreEqual( "https://gitlab.com/cznic/sqlite/-/blob/master/LICENSE", raw_go_package.Licenses[0].HtmlLicenseUri, "HTML license URL mismatch" );

			Assert.HasCount( 2, raw_go_package.Dependencies, "Incorrect number of dependency types" );
			Assert.HasCount( 5, raw_go_package.Dependencies[EDependencyType.Direct], "Incorrect number of direct dependencies" );

			raw_go_package.WriteTpsFile( "Go" );
		}

		[TestMethod]
		public void TestNuGetPackage()
		{
			string package_name = "EtErNaL.ConsoleUTILITIES";
			NuGetPackage? raw_nuget_package = NuGetPackage.GetNuGetPackage( package_name, "", true );
			Assert.IsNotNull( raw_nuget_package, $"Failed to get NuGet package '{package_name}'" );
			Assert.AreEqual( "1.0.14", raw_nuget_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_nuget_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "MIT", raw_nuget_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			Assert.AreEqual( "https://eternaldevelopments.com/docs", raw_nuget_package.HomepageUri, "Incorrect homepage uri" );
			Assert.AreEqual( "https://github.com/JohnJScott/Eternal", raw_nuget_package.SourceCodeUri, "Incorrect source code uri" );

			raw_nuget_package.CleanupData();

			Assert.AreEqual( "Copyright Eternal Developments, LLC. All Rights Reserved.", raw_nuget_package.Copyright, "Incorrect copyright" );

			raw_nuget_package.WriteTpsFile( "NuGet" );
		}

		[TestMethod]
		public void TestNuGetPackageWithDependencies()
		{
			string package_name = "StackExchange.Redis";
			NuGetPackage? raw_nuget_package = NuGetPackage.GetNuGetPackage( package_name, "", true );
			Assert.IsNotNull( raw_nuget_package, $"Failed to get NuGet package '{package_name}'" );
			Assert.AreEqual( "3.2.0", raw_nuget_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_nuget_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "MIT", raw_nuget_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			Assert.AreEqual( "https://seredis.dev/", raw_nuget_package.HomepageUri, "Incorrect homepage uri" );

			raw_nuget_package.CleanupData();

			Assert.AreEqual( "2014 - 2026 Stack Exchange, Inc.", raw_nuget_package.Copyright, "Incorrect copyright" );

			Assert.HasCount( 5, raw_nuget_package.Dependencies, "Incorrect number of dependency types" );
			Assert.HasCount( 7, raw_nuget_package.Dependencies[EDependencyType.NETFramework472], "Incorrect number of direct dependencies" );

			raw_nuget_package.WriteTpsFile( "NuGet" );
		}

		[TestMethod]
		public void TestnpmPackage()
		{
			string package_name = "is-obj";
			NpmPackage? raw_npm_package = NpmPackage.GetNpmPackage( package_name, "", true );
			Assert.IsNotNull( raw_npm_package, $"Failed to get npm package '{package_name}'" );
			Assert.AreEqual( "3.0.0", raw_npm_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_npm_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "MIT", raw_npm_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_npm_package.CleanupData();

			raw_npm_package.WriteTpsFile( "npm" );
		}

		[TestMethod]
		public void TestnpmPackageBlueOak()
		{
			string package_name = "minipass";
			NpmPackage? raw_npm_package = NpmPackage.GetNpmPackage( package_name, "", true );
			Assert.IsNotNull( raw_npm_package, $"Failed to get npm package '{package_name}'" );
			Assert.AreEqual( "7.1.3", raw_npm_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_npm_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "BlueOak-1.0.0", raw_npm_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_npm_package.CleanupData();

			raw_npm_package.WriteTpsFile( "npm" );
		}

		[TestMethod]
		public void TestnpmPackageWithDependencies()
		{
			string package_name = "yargs";
			NpmPackage? raw_npm_package = NpmPackage.GetNpmPackage( package_name, "", true );
			Assert.IsNotNull( raw_npm_package, $"Failed to get npm package '{package_name}'" );
			Assert.AreEqual( "18.1.0", raw_npm_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_npm_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "MIT", raw_npm_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_npm_package.CleanupData();

			Assert.HasCount( 2, raw_npm_package.Dependencies, "Incorrect number of dependency types" );
			Assert.HasCount( 22, raw_npm_package.Dependencies[EDependencyType.Development], "Incorrect number of development dependencies" );

			raw_npm_package.WriteTpsFile( "npm" );
		}

		[TestMethod]
		public void TestRubyGemsPackage()
		{
			string gem_name = "bcrypt";
			RubyGem? raw_rubygems_gem = RubyGem.GetRubyGem( gem_name, "", true );
			Assert.IsNotNull( raw_rubygems_gem, $"Failed to get Ruby Gem '{gem_name}'" );
			Assert.AreEqual( "3.1.22", raw_rubygems_gem.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_rubygems_gem.Licenses, "There should only be a single license" );
			Assert.AreEqual( "MIT", raw_rubygems_gem.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_rubygems_gem.CleanupData();

			raw_rubygems_gem.WriteTpsFile( "RubyGems" );
		}

		[TestMethod]
		public void TestRubyGemsPackageCC0()
		{
			string gem_name = "codeinventory-github";
			RubyGem? raw_rubygems_gem = RubyGem.GetRubyGem( gem_name, "", true );
			Assert.IsNotNull( raw_rubygems_gem, $"Failed to get Ruby Gem '{gem_name}'" );
			Assert.AreEqual( "0.4.2", raw_rubygems_gem.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_rubygems_gem.Licenses, "There should only be a single license" );
			Assert.AreEqual( "CC0-1.0", raw_rubygems_gem.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_rubygems_gem.CleanupData();

			raw_rubygems_gem.WriteTpsFile( "RubyGems" );
		}

		[TestMethod]
		public void TestRubyGemsPackageWithDependencies()
		{
			string gem_name = "sinatra";
			RubyGem? raw_rubygems_gem = RubyGem.GetRubyGem( gem_name, "", true );
			Assert.IsNotNull( raw_rubygems_gem, $"Failed to get Ruby Gem '{gem_name}'" );
			Assert.AreEqual( "4.2.1", raw_rubygems_gem.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_rubygems_gem.Licenses, "There should only be a single license" );
			Assert.AreEqual( "MIT", raw_rubygems_gem.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_rubygems_gem.CleanupData();

			Assert.HasCount( 1, raw_rubygems_gem.Dependencies, "Incorrect number of dependency types" );
			Assert.HasCount( 6, raw_rubygems_gem.Dependencies[EDependencyType.Production], "Incorrect number of development dependencies" );

			raw_rubygems_gem.WriteTpsFile( "RubyGems" );
		}

		[TestMethod]
		public void TestPyPIPackage()
		{
			string python_package = "chardet";
			PythonPackage? raw_python_package = PythonPackage.GetPythonPackage( python_package, "", true );
			Assert.IsNotNull( raw_python_package, $"Failed to get Ruby Gem '{python_package}'" );
			Assert.AreEqual( "7.6.0", raw_python_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_python_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "0BSD", raw_python_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_python_package.CleanupData();

			raw_python_package.WriteTpsFile( "PyPI" );
		}

		[TestMethod]
		public void TestPyPIPackagePSF()
		{
			string python_package = "distlib";
			PythonPackage? raw_python_package = PythonPackage.GetPythonPackage( python_package, "", true );
			Assert.IsNotNull( raw_python_package, $"Failed to get Ruby Gem '{python_package}'" );
			Assert.AreEqual( "0.4.3", raw_python_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_python_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "PSF-2.0", raw_python_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_python_package.CleanupData();

			raw_python_package.WriteTpsFile( "PyPI" );
		}

		[TestMethod]
		public void TestPyPIPackageMPL()
		{
			string python_package = "certifi";
			PythonPackage? raw_python_package = PythonPackage.GetPythonPackage( python_package, "", true );
			Assert.IsNotNull( raw_python_package, $"Failed to get Ruby Gem '{python_package}'" );
			Assert.AreEqual( "2026.7.22", raw_python_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_python_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "MPL-2.0", raw_python_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_python_package.CleanupData();

			raw_python_package.WriteTpsFile( "PyPI" );
		}
	}
}
