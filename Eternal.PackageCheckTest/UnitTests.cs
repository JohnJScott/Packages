// Copyright Eternal Developments LLC. All Rights Reserved.

using Eternal.PackageCheck;

[assembly: Parallelize( Scope = ExecutionScope.MethodLevel )]

namespace Eternal.PackageCheckTest
{
	[TestClass]
	public sealed class PackageCheckTests
	{
		[TestMethod]
		[TestCategory( "GoPackage" )]
		public void TestGoPackageXSys()
		{
			string package_name = "golang.org/x/sys";
			GoPackage? raw_go_package = GoPackage.GetGoPackage( package_name, "", true );
			Assert.IsNotNull( raw_go_package, $"Failed to get Go package '{package_name}'" );
			Assert.AreEqual( "v0.49.0", raw_go_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_go_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "BSD-3-Clause", raw_go_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_go_package.CleanupData();
			
			Assert.AreEqual( "https://pkg.go.dev/golang.org/x/sys@v0.49.0", raw_go_package.HomepageUri, "Homepage URL mismatch" );
			Assert.AreEqual( "https://proxy.golang.org/golang.org/x/sys/@v/v0.49.0.zip", raw_go_package.DownloadUri, "Download URL mismatch" );
			Assert.AreEqual( "https://go.googlesource.com/sys/+/refs/tags/v0.49.0/LICENSE", raw_go_package.Licenses[0].HtmlLicenseUri, "HTML license URL mismatch" );

			// deps.dev treats the package name as a single path segment, so the slashes in a Go module path have to be escaped or the page reports "INVALID URL"
			Assert.AreEqual( "https://deps.dev/go/golang.org%2Fx%2Fsys/v0.49.0/requirements", raw_go_package.DependencyUri, "Dependency URL mismatch" );

			raw_go_package.WriteTpsFile( GoPackage.PackageType );
		}

		[TestMethod]
		[TestCategory( "GoPackage" )]
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

			raw_go_package.WriteTpsFile( GoPackage.PackageType );
		}

		[TestMethod]
		[TestCategory( "GoPackage" )]
		public void TestGoPackageSQLite()
		{
			string package_name = "modernc.org/sqlite";
			GoPackage? raw_go_package = GoPackage.GetGoPackage( package_name, "", true );
			Assert.IsNotNull( raw_go_package, $"Failed to get Go package '{package_name}'" );
			Assert.AreEqual( "v1.61.1", raw_go_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_go_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "BSD-3-Clause", raw_go_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_go_package.CleanupData();

			Assert.AreEqual( "https://pkg.go.dev/modernc.org/sqlite@v1.61.1", raw_go_package.HomepageUri, "Homepage URL mismatch" );
			Assert.AreEqual( "https://proxy.golang.org/modernc.org/sqlite/@v/v1.61.1.zip", raw_go_package.DownloadUri, "Download URL mismatch" );
			Assert.AreEqual( "https://gitlab.com/cznic/sqlite/-/blob/master/LICENSE", raw_go_package.Licenses[0].HtmlLicenseUri, "HTML license URL mismatch" );

			Assert.HasCount( 2, raw_go_package.Dependencies, "Incorrect number of dependency types" );
			Assert.HasCount( 5, raw_go_package.Dependencies[EDependencyType.Direct], "Incorrect number of direct dependencies" );
			Assert.AreEqual( "BSD-3-Clause", raw_go_package.Dependencies[EDependencyType.Direct][1].SpdxCode, "Incorrect license in dependency" );

			raw_go_package.WriteTpsFile( GoPackage.PackageType );
		}

		[TestMethod]
		[TestCategory( "NuGetPackage" )]
		public void TestNuGetPackageConsoleUtilities()
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

			raw_nuget_package.WriteTpsFile( NuGetPackage.PackageType );
		}

		[TestMethod]
		[TestCategory( "NuGetPackage" )]
		public void TestNuGetPackageAutomapper()
		{
			string package_name = "Automapper";
			NuGetPackage? raw_nuget_package = NuGetPackage.GetNuGetPackage( package_name, "", true );
			Assert.IsNotNull( raw_nuget_package, $"Failed to get NuGet package '{package_name}'" );
			Assert.AreEqual( "16.2.0", raw_nuget_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_nuget_package.Licenses, "There should only be a single license" );

			raw_nuget_package.CleanupData();

			Assert.AreEqual( "Copyright Jimmy Bogard", raw_nuget_package.Copyright, "Incorrect copyright" );

			raw_nuget_package.WriteTpsFile( NuGetPackage.PackageType );
		}

		[TestMethod]
		[TestCategory( "NuGetPackage" )]
		public void TestNuGetPackageDotNetOpenAuthCoreMSPL()
		{
			string package_name = "DotNetOpenAuth.Core";
			NuGetPackage? raw_nuget_package = NuGetPackage.GetNuGetPackage( package_name, "", true );
			Assert.IsNotNull( raw_nuget_package, $"Failed to get NuGet package '{package_name}'" );
			Assert.AreEqual( "4.3.4.13329", raw_nuget_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_nuget_package.Licenses, "There should only be a single license" );
			//Assert.AreEqual( "RPL-1.5", raw_nuget_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			//Assert.AreEqual( "https://eternaldevelopments.com/docs", raw_nuget_package.HomepageUri, "Incorrect homepage uri" );
			//Assert.AreEqual( "https://github.com/JohnJScott/Eternal", raw_nuget_package.SourceCodeUri, "Incorrect source code uri" );

			raw_nuget_package.CleanupData();

			raw_nuget_package.WriteTpsFile( NuGetPackage.PackageType );
		}

		[TestMethod]
		[TestCategory( "NuGetPackage" )]
		public void TestNuGetPackageRedis()
		{
			string package_name = "StackExchange.Redis";
			NuGetPackage? raw_nuget_package = NuGetPackage.GetNuGetPackage( package_name, "", true );
			Assert.IsNotNull( raw_nuget_package, $"Failed to get NuGet package '{package_name}'" );
			Assert.AreEqual( "3.4.31", raw_nuget_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_nuget_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "MIT", raw_nuget_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			Assert.AreEqual( "https://seredis.dev/", raw_nuget_package.HomepageUri, "Incorrect homepage uri" );

			raw_nuget_package.CleanupData();

			Assert.AreEqual( "2014 - 2026 Stack Exchange, Inc.", raw_nuget_package.Copyright, "Incorrect copyright" );

			Assert.HasCount( 5, raw_nuget_package.Dependencies, "Incorrect number of dependency types" );
			Assert.HasCount( 8, raw_nuget_package.Dependencies[EDependencyType.NETFramework472], "Incorrect number of direct dependencies" );

			raw_nuget_package.WriteTpsFile( NuGetPackage.PackageType );
		}

		[TestMethod]
		[TestCategory( "NpmPackage" )]
		public void TestnpmPackageIsObj()
		{
			string package_name = "is-obj";
			NpmPackage? raw_npm_package = NpmPackage.GetNpmPackage( package_name, "", true );
			Assert.IsNotNull( raw_npm_package, $"Failed to get npm package '{package_name}'" );
			Assert.AreEqual( "3.0.0", raw_npm_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_npm_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "MIT", raw_npm_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_npm_package.CleanupData();

			// An unscoped name needs no escaping, so this is the simple case the Go and scoped npm tests are contrasted against
			Assert.AreEqual( "https://deps.dev/npm/is-obj/3.0.0/dependencies", raw_npm_package.DependencyUri, "Dependency URL mismatch" );

			raw_npm_package.WriteTpsFile( NpmPackage.PackageType );
		}

		[TestMethod]
		[TestCategory( "NpmPackage" )]
		public void TestnpmPackageScoped()
		{
			// A scoped package name carries both an '@' and a '/', which have to be escaped for the deps.dev link to resolve
			string package_name = "@babel/core";
			NpmPackage? raw_npm_package = NpmPackage.GetNpmPackage( package_name, "7.28.0", true );
			Assert.IsNotNull( raw_npm_package, $"Failed to get npm package '{package_name}'" );
			Assert.AreEqual( "7.28.0", raw_npm_package.Version, "Requested version mismatch" );
			Assert.AreEqual( "@babel/core", raw_npm_package.Name, "The scope should be kept as part of the name" );

			raw_npm_package.CleanupData();

			Assert.AreEqual( "https://deps.dev/npm/%40babel%2Fcore/7.28.0/dependencies", raw_npm_package.DependencyUri, "Dependency URL mismatch" );

			raw_npm_package.WriteTpsFile( NpmPackage.PackageType );
		}

		[TestMethod]
		[TestCategory( "NpmPackage" )]
		public void TestnpmPackageBlueOak()
		{
			string package_name = "minipass";
			NpmPackage? raw_npm_package = NpmPackage.GetNpmPackage( package_name, "", true );
			Assert.IsNotNull( raw_npm_package, $"Failed to get npm package '{package_name}'" );
			Assert.AreEqual( "7.1.3", raw_npm_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_npm_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "BlueOak-1.0.0", raw_npm_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_npm_package.CleanupData();

			raw_npm_package.WriteTpsFile( NpmPackage.PackageType );
		}

		[TestMethod]
		[TestCategory( "NpmPackage" )]
		public void TestnpmPackageYargs()
		{
			string package_name = "yargs";
			NpmPackage? raw_npm_package = NpmPackage.GetNpmPackage( package_name, "", true );
			Assert.IsNotNull( raw_npm_package, $"Failed to get npm package '{package_name}'" );
			Assert.AreEqual( "18.2.0", raw_npm_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_npm_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "MIT", raw_npm_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_npm_package.CleanupData();

			Assert.HasCount( 2, raw_npm_package.Dependencies, "Incorrect number of dependency types" );
			Assert.HasCount( 22, raw_npm_package.Dependencies[EDependencyType.Development], "Incorrect number of development dependencies" );
			Assert.AreEqual( "MIT", raw_npm_package.Dependencies[EDependencyType.Production][3].SpdxCode, "License mismatch in dependency" );
			Assert.AreEqual( "string-width", raw_npm_package.Dependencies[EDependencyType.Production][3].Name, "Name mismatch in dependency" );

			raw_npm_package.WriteTpsFile( NpmPackage.PackageType );
		}

		[TestMethod]
		[TestCategory( "RubyPackage" )]
		public void TestRubyGemsPackageBcrypt()
		{
			string gem_name = "bcrypt";
			RubyGem? raw_rubygems_gem = RubyGem.GetRubyGem( gem_name, "", true );
			Assert.IsNotNull( raw_rubygems_gem, $"Failed to get Ruby Gem '{gem_name}'" );
			Assert.AreEqual( "3.1.22", raw_rubygems_gem.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_rubygems_gem.Licenses, "There should only be a single license" );
			Assert.AreEqual( "MIT", raw_rubygems_gem.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_rubygems_gem.CleanupData();

			raw_rubygems_gem.WriteTpsFile( RubyGem.PackageType );
		}

		[TestMethod]
		[TestCategory( "RubyPackage" )]
		public void TestRubyGemsPackageCC0()
		{
			string gem_name = "codeinventory-github";
			RubyGem? raw_rubygems_gem = RubyGem.GetRubyGem( gem_name, "", true );
			Assert.IsNotNull( raw_rubygems_gem, $"Failed to get Ruby Gem '{gem_name}'" );
			Assert.AreEqual( "0.4.2", raw_rubygems_gem.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_rubygems_gem.Licenses, "There should only be a single license" );
			Assert.AreEqual( "CC0-1.0", raw_rubygems_gem.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_rubygems_gem.CleanupData();

			raw_rubygems_gem.WriteTpsFile( RubyGem.PackageType );
		}

		[TestMethod]
		[TestCategory( "RubyPackage" )]
		public void TestRubyGemsPackageElasticSearchRails()
		{
			string gem_name = "elasticsearch-rails";
			RubyGem? raw_rubygems_gem = RubyGem.GetRubyGem( gem_name, "", true );
			Assert.IsNotNull( raw_rubygems_gem, $"Failed to get Ruby Gem '{gem_name}'" );
			Assert.AreEqual( "8.0.1", raw_rubygems_gem.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_rubygems_gem.Licenses, "There should only be a single license" );
			Assert.AreEqual( "Apache 2", raw_rubygems_gem.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_rubygems_gem.CleanupData();

			Assert.AreEqual( "Apache-2.0", raw_rubygems_gem.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_rubygems_gem.WriteTpsFile( RubyGem.PackageType );
		}

		[TestMethod]
		[TestCategory( "RubyPackage" )]
		public void TestRubyGemsPackageSinatra()
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

			// Every ecosystem other than Cargo still links to Snyk
			Assert.StartsWith( "https://security.snyk.io/package/rubygems/sinatra/", raw_rubygems_gem.VulnerabilityUri, "Non-Cargo packages should still link to Snyk" );

			raw_rubygems_gem.WriteTpsFile( RubyGem.PackageType );
		}

		[TestMethod]
		[TestCategory( "PythonPackage" )]
		public void TestPyPIPackageChardet()
		{
			string python_package = "chardet";
			PythonPackage? raw_python_package = PythonPackage.GetPythonPackage( python_package, "", true );
			Assert.IsNotNull( raw_python_package, $"Failed to get Ruby Gem '{python_package}'" );
			Assert.AreEqual( "7.7.0", raw_python_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_python_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "0BSD", raw_python_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_python_package.CleanupData();

			raw_python_package.WriteTpsFile( PythonPackage.PackageType );
		}

		[TestMethod]
		[TestCategory( "PythonPackage" )]
		public void TestPyPIPackagePSF()
		{
			string python_package = "distlib";
			PythonPackage? raw_python_package = PythonPackage.GetPythonPackage( python_package, "", true );
			Assert.IsNotNull( raw_python_package, $"Failed to get Ruby Gem '{python_package}'" );
			Assert.AreEqual( "0.4.4", raw_python_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_python_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "PSF-2.0", raw_python_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_python_package.CleanupData();

			raw_python_package.WriteTpsFile( PythonPackage.PackageType );
		}

		[TestMethod]
		[TestCategory( "PythonPackage" )]
		public void TestPyPIPackageMPL()
		{
			string python_package = "certifi";
			PythonPackage? raw_python_package = PythonPackage.GetPythonPackage( python_package, "", true );
			Assert.IsNotNull( raw_python_package, $"Failed to get Ruby Gem '{python_package}'" );
			Assert.AreEqual( "2026.10.23", raw_python_package.Version, "Latest version mismatch" );
			Assert.HasCount( 1, raw_python_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "MPL-2.0", raw_python_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			raw_python_package.CleanupData();

			raw_python_package.WriteTpsFile( PythonPackage.PackageType );
		}

		[TestMethod]
		[TestCategory( "CargoPackage" )]
		public void TestCargoPackageSerde()
		{
			// Pinned to a published version so that the assertions do not drift as the crate is updated
			string crate_name = "serde";
			CargoPackage? raw_cargo_package = CargoPackage.GetCargoPackage( crate_name, "1.0.228", true );
			Assert.IsNotNull( raw_cargo_package, $"Failed to get crate '{crate_name}'" );
			Assert.AreEqual( "1.0.228", raw_cargo_package.Version, "Requested version mismatch" );

			// crates.io publishes a single SPDX expression, so "MIT OR Apache-2.0" has to be split into one entry per license
			Assert.HasCount( 2, raw_cargo_package.Licenses, "A dual licensed crate should produce two licenses" );
			Assert.AreEqual( "MIT", raw_cargo_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );
			Assert.AreEqual( "Apache-2.0", raw_cargo_package.Licenses[1].SpdxCode, "Mismatch in SPDX code" );

			Assert.AreEqual( "https://serde.rs", raw_cargo_package.HomepageUri, "Homepage URL mismatch" );
			Assert.AreEqual( "https://github.com/serde-rs/serde", raw_cargo_package.SourceCodeUri, "Source code URL mismatch" );
			Assert.AreEqual( "https://crates.io/api/v1/crates/serde/1.0.228/download", raw_cargo_package.DownloadUri, "Download URL mismatch" );

			raw_cargo_package.CleanupData();

			// MIT outranks Apache-2.0 in LicensePriorities.json, so it is the one reported for the package
			Assert.IsTrue( raw_cargo_package.Licenses[0].IsPreferred, "MIT should be the preferred license" );

			// Make sure the licence contents are set for both licenses
			Assert.IsTrue( raw_cargo_package.Licenses[0].LicenseContent.Length > 0, "License content should be set" );
			Assert.IsTrue( raw_cargo_package.Licenses[1].LicenseContent.Length > 0, "License content should be set" );

			// Snyk has no page for a crate without an advisory, so Cargo packages link to deps.dev, which carries every published crate
			Assert.AreEqual( "https://deps.dev/cargo/serde/1.0.228", raw_cargo_package.VulnerabilityUri, "Cargo packages should link to deps.dev" );

			raw_cargo_package.WriteTpsFile( CargoPackage.PackageType );
		}

		[TestMethod]
		[TestCategory( "CargoPackage" )]
		public void TestCargoPackageRandDependencies()
		{
			string crate_name = "rand";
			CargoPackage? raw_cargo_package = CargoPackage.GetCargoPackage( crate_name, "0.8.5", true );
			Assert.IsNotNull( raw_cargo_package, $"Failed to get crate '{crate_name}'" );
			Assert.AreEqual( "0.8.5", raw_cargo_package.Version, "Requested version mismatch" );
			Assert.HasCount( 2, raw_cargo_package.Licenses, "A dual licensed crate should produce two licenses" );

			raw_cargo_package.CleanupData();

			// 0.8.5 declares six "normal" and two "dev" dependencies; dev and build kinds both land under Development
			Assert.HasCount( 2, raw_cargo_package.Dependencies, "Incorrect number of dependency types" );
			Assert.HasCount( 6, raw_cargo_package.Dependencies[EDependencyType.Production], "Incorrect number of production dependencies" );
			Assert.HasCount( 2, raw_cargo_package.Dependencies[EDependencyType.Development], "Incorrect number of development dependencies" );

			raw_cargo_package.WriteTpsFile( CargoPackage.PackageType );
		}

		[TestMethod]
		[TestCategory( "CargoPackage" )]
		public void TestCargoPackageSingleLicense()
		{
			string crate_name = "openssl";
			CargoPackage? raw_cargo_package = CargoPackage.GetCargoPackage( crate_name, "0.10.81", true );
			Assert.IsNotNull( raw_cargo_package, $"Failed to get crate '{crate_name}'" );
			Assert.AreEqual( "0.10.81", raw_cargo_package.Version, "Requested version mismatch" );
			Assert.HasCount( 1, raw_cargo_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "Apache-2.0", raw_cargo_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );

			// The crate publishes no homepage, so the crates.io page stands in for one
			Assert.AreEqual( "https://crates.io/crates/openssl", raw_cargo_package.HomepageUri, "Homepage URL should fall back to crates.io" );

			raw_cargo_package.CleanupData();

			raw_cargo_package.WriteTpsFile( CargoPackage.PackageType );
		}

		[TestMethod]
		[TestCategory( "CargoPackage" )]
		public void TestCargoPackageCaretRequirement()
		{
			// A caret requirement should resolve within the 0.8 line rather than jumping to the latest release
			string crate_name = "rand";
			CargoPackage? raw_cargo_package = CargoPackage.GetCargoPackage( crate_name, "^0.8", true );
			Assert.IsNotNull( raw_cargo_package, $"Failed to get crate '{crate_name}'" );
			Assert.StartsWith( "0.8.", raw_cargo_package.Version, "A '^0.8' requirement must stay inside the 0.8 line" );
		}

		[TestMethod]
		[TestCategory( "CargoPackage" )]
		public void TestCargoPackageLatestVersion()
		{
			string crate_name = "bitflags";
			CargoPackage? raw_cargo_package = CargoPackage.GetCargoPackage( crate_name, "", true );
			Assert.IsNotNull( raw_cargo_package, $"Failed to get crate '{crate_name}'" );
			Assert.IsFalse( new CargoVersion( raw_cargo_package.Version ).IsPreRelease, "A prerelease should never be selected as the latest version" );
			Assert.IsGreaterThanOrEqualTo( 1, raw_cargo_package.Licenses.Count, "The crate should report at least one license" );

			raw_cargo_package.CleanupData();

			raw_cargo_package.WriteTpsFile( CargoPackage.PackageType );
		}

		[TestMethod]
		[TestCategory( "DebPackage" )]
		public void TestDebPackageLibCurl()
		{
			// Pinned to a published version so that the assertions do not drift as the package is updated. libcurl4t64 is a binary package built from curl.
			string package_name = "libcurl4t64";
			DebPackage? raw_deb_package = DebPackage.GetDebPackage( package_name, "8.14.1-2", true );
			Assert.IsNotNull( raw_deb_package, $"Failed to get Debian package '{package_name}'" );
			Assert.AreEqual( "8.14.1-2", raw_deb_package.Version, "Requested version mismatch" );
			Assert.AreEqual( "curl", raw_deb_package.SourceName, "The source package should be resolved from the binary package" );
			Assert.AreEqual( "8.14.1-2", raw_deb_package.SourceVersion, "Source version mismatch" );
			Assert.AreEqual( "13", raw_deb_package.DebianRelease, "The original release should be placed by the stable update built on top of it" );

			Assert.HasCount( 1, raw_deb_package.Licenses, "Only the 'Files: *' paragraph's license should be reported" );
			Assert.AreEqual( "curl", raw_deb_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );
			Assert.IsTrue( raw_deb_package.Licenses[0].LicenseContent.Length > 0, "The stand-alone license paragraph should supply the license text" );
			Assert.StartsWith( "Copyright 1996-2025, Daniel Stenberg", raw_deb_package.Copyright, "Copyright mismatch" );

			Assert.AreEqual( "https://curl.se/", raw_deb_package.HomepageUri, "Homepage URL mismatch" );
			Assert.AreEqual( "https://salsa.debian.org/debian/curl", raw_deb_package.SourceCodeUri, "Source code URL mismatch" );
			Assert.AreEqual( "https://snapshot.debian.org/package/curl/8.14.1-2/", raw_deb_package.DownloadUri, "Download URL mismatch" );

			Assert.HasCount( 1, raw_deb_package.Dependencies, "Only build dependencies are published" );
			Assert.IsTrue( raw_deb_package.Dependencies[EDependencyType.Build].Exists( x => x.Name == "libssl-dev" ), "libssl-dev should be a build dependency" );

			raw_deb_package.CleanupData();

			Assert.AreEqual( "https://security-tracker.debian.org/tracker/source-package/curl", raw_deb_package.VulnerabilityUri, "Debian packages should link to the Debian security tracker" );

			raw_deb_package.WriteTpsFile( DebPackage.PackageType );
		}

		[TestMethod]
		[TestCategory( "DebPackage" )]
		public void TestDebPackageEpoch()
		{
			// The epoch is part of the version, but not of any file name in the archive
			string package_name = "openssh-client";
			DebPackage? raw_deb_package = DebPackage.GetDebPackage( package_name, "1:9.2p1-2+deb12u3", false );
			Assert.IsNotNull( raw_deb_package, $"Failed to get Debian package '{package_name}'" );
			Assert.AreEqual( "1:9.2p1-2+deb12u3", raw_deb_package.Version, "Requested version mismatch" );
			Assert.AreEqual( "openssh", raw_deb_package.SourceName, "The source package should be resolved from the binary package" );
			Assert.AreEqual( "12", raw_deb_package.DebianRelease, "A superseded stable update should be placed by its '+deb12' suffix" );

			string tps_name = GenericPackage.GetTpsName( DebPackage.PackageType, raw_deb_package.Name, raw_deb_package.Version );
			Assert.AreEqual( "openssh-client.1_9.2p1-2+deb12u3.Deb.tps", tps_name, "A colon is not valid in a Windows file name" );
		}

		[TestMethod]
		[TestCategory( "DebPackage" )]
		public void TestDebPackageLatestVersion()
		{
			string package_name = "zlib1g";
			DebPackage? raw_deb_package = DebPackage.GetDebPackage( package_name, "", true );
			Assert.IsNotNull( raw_deb_package, $"Failed to get Debian package '{package_name}'" );
			Assert.AreEqual( "zlib", raw_deb_package.SourceName, "The source package should be resolved from the binary package" );
			Assert.AreEqual( "13", raw_deb_package.DebianRelease, "The version in stable belongs to the stable release" );
			Assert.HasCount( 1, raw_deb_package.Licenses, "There should only be a single license" );
			Assert.AreEqual( "Zlib", raw_deb_package.Licenses[0].SpdxCode, "Mismatch in SPDX code" );
		}
	}
}
