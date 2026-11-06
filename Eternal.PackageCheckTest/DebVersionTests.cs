// Copyright Eternal Developments LLC. All Rights Reserved.

using Eternal.PackageCheck;

namespace Eternal.PackageCheckTest
{
	[TestClass]
	public sealed class DebVersionTests
	{
		private static int Compare( string left, string right )
		{
			return Math.Sign( new DebVersion( left ).CompareTo( new DebVersion( right ) ) );
		}

		private static string FixSpdxCode( string licenseName )
		{
			List<LicenseInfo> licenses = new List<LicenseInfo> { new LicenseInfo( licenseName ) };
			LicenseChecker.FixSpdxCodes( licenses );
			return licenses[0].SpdxCode;
		}

		private static bool Satisfies( string version, string requirement )
		{
			DebRequirement deb_requirement = new DebRequirement( requirement );
			return deb_requirement.SatisfiedBy( new DebVersion( version ) );
		}

		// ---- DebVersion: parsing ----

		[TestMethod]
		[TestCategory( "DebVersion" )]
		public void DebVersionParsesAllParts()
		{
			DebVersion version = new DebVersion( "1:2.3-4+deb13u5" );

			Assert.AreEqual( 1, version.Epoch, "Epoch mismatch" );
			Assert.AreEqual( "2.3", version.UpstreamVersion, "Upstream version mismatch" );
			Assert.AreEqual( "4+deb13u5", version.DebianRevision, "Revision mismatch" );
			Assert.AreEqual( "1:2.3-4+deb13u5", version.ToString(), "ToString should return the original text" );
		}

		[TestMethod]
		[TestCategory( "DebVersion" )]
		public void DebVersionSplitsOnTheLastHyphen()
		{
			DebVersion version = new DebVersion( "1.2-beta-3" );

			Assert.AreEqual( 0, version.Epoch, "An omitted epoch defaults to zero" );
			Assert.AreEqual( "1.2-beta", version.UpstreamVersion, "The upstream version may contain a hyphen" );
			Assert.AreEqual( "3", version.DebianRevision, "The revision follows the last hyphen" );
		}

		[TestMethod]
		[TestCategory( "DebVersion" )]
		public void DebVersionNativePackageHasNoRevision()
		{
			DebVersion version = new DebVersion( "20250419" );

			Assert.AreEqual( "20250419", version.UpstreamVersion, "Upstream version mismatch" );
			Assert.AreEqual( string.Empty, version.DebianRevision, "A native package has no revision" );
		}

		[TestMethod]
		[TestCategory( "DebVersion" )]
		public void DebVersionRejectsInvalidText()
		{
			Assert.ThrowsExactly<FormatException>( () => new DebVersion( "" ), "An empty version is invalid" );
			Assert.ThrowsExactly<FormatException>( () => new DebVersion( "x:1.0" ), "A non-numeric epoch is invalid" );
			Assert.ThrowsExactly<FormatException>( () => new DebVersion( "1:-1" ), "A missing upstream version is invalid" );
		}

		// ---- DebVersion: ordering ----

		[TestMethod]
		[TestCategory( "DebVersion" )]
		public void DebVersionOrdersNumerically()
		{
			Assert.AreEqual( -1, Compare( "1.2", "1.10" ), "Digit runs compare numerically" );
			Assert.AreEqual( 0, Compare( "1.01", "1.1" ), "Leading zeros are ignored" );
			Assert.AreEqual( 1, Compare( "2.0-10", "2.0-9" ), "Revisions compare numerically too" );
		}

		[TestMethod]
		[TestCategory( "DebVersion" )]
		public void DebVersionEpochOutranksEverything()
		{
			Assert.AreEqual( 1, Compare( "1:0.1", "9.9" ), "Any epoch outranks no epoch" );
			Assert.AreEqual( 0, Compare( "0:1.0-1", "1.0-1" ), "An epoch of zero is the same as none" );
		}

		[TestMethod]
		[TestCategory( "DebVersion" )]
		public void DebVersionTildeSortsFirst()
		{
			Assert.AreEqual( -1, Compare( "8.23.0~rc3-1", "8.23.0-1" ), "A tilde sorts before the end of the string" );
			Assert.AreEqual( -1, Compare( "1.0~~", "1.0~" ), "Two tildes sort before one" );
			Assert.AreEqual( -1, Compare( "8.14.1-2~bpo12+1", "8.14.1-2" ), "A backport sorts before the version it was backported from" );
		}

		[TestMethod]
		[TestCategory( "DebVersion" )]
		public void DebVersionLettersSortBeforeOtherCharacters()
		{
			Assert.AreEqual( -1, Compare( "1.0a", "1.0+" ), "Letters sort before non-letters" );
			Assert.AreEqual( 1, Compare( "1.0a", "1.0" ), "A letter sorts after the end of the string" );
			Assert.AreEqual( 1, Compare( "8.14.1-2+deb13u5", "8.14.1-2" ), "A stable update sorts after the version it updates" );
			Assert.AreEqual( -1, Compare( "8.14.1-2+deb13u5", "8.14.1-2+deb13u10" ), "The update number compares numerically" );
		}

		// ---- DebRequirement ----

		[TestMethod]
		[TestCategory( "DebVersion" )]
		public void DebRequirementOperators()
		{
			Assert.IsTrue( Satisfies( "1.22.5", ">= 1.22.5" ), ">= includes the bound" );
			Assert.IsTrue( Satisfies( "1.2", "<= 1.2" ), "<= includes the bound" );
			Assert.IsFalse( Satisfies( "1.2", ">> 1.2" ), ">> excludes the bound" );
			Assert.IsFalse( Satisfies( "1.2", "<< 1.2" ), "<< excludes the bound" );
			Assert.IsTrue( Satisfies( "1.2", "= 1.2" ), "= matches exactly" );
			Assert.IsTrue( Satisfies( "1.2", "1.2" ), "No operator matches exactly" );
			Assert.IsFalse( Satisfies( "1.2-1", "1.2" ), "No operator does not ignore the revision" );
			Assert.IsTrue( Satisfies( "1.2", "> 1.2" ), "The deprecated > means >=" );
			Assert.IsTrue( Satisfies( "1.2", "< 1.2" ), "The deprecated < means <=" );
			Assert.IsTrue( Satisfies( "1.2", "" ), "No requirement matches everything" );
		}

		[TestMethod]
		[TestCategory( "DebVersion" )]
		public void DebVersionMatcherSelectsHighest()
		{
			List<string> versions = new List<string> { "8.14.1-2", "8.23.0~rc3-1", "8.14.1-2+deb13u5", "7.88.1-10" };

			Assert.AreEqual( "8.23.0~rc3-1", DebVersionMatcher.SelectMatchingVersion( "curl", versions, "" )?.ToString(), "The highest version should be selected" );
			Assert.AreEqual( "8.14.1-2+deb13u5", DebVersionMatcher.SelectMatchingVersion( "curl", versions, "<< 8.23.0~" )?.ToString(), "The highest version under the bound should be selected" );
			Assert.IsNull( DebVersionMatcher.SelectMatchingVersion( "curl", versions, ">= 9" ), "Nothing satisfies the requirement" );
		}

		// ---- DebPackage: licenses ----

		[TestMethod]
		[TestCategory( "DebVersion" )]
		public void DebLicenseNamesMapToSpdx()
		{
			Assert.AreEqual( "MIT", FixSpdxCode( "Expat" ), "Expat is MIT" );
			Assert.AreEqual( "BSD-3-Clause", FixSpdxCode( "BSD-3-clause" ), "Debian lower cases 'clause'" );
			Assert.AreEqual( "Apache-2.0", FixSpdxCode( "Apache-2" ), "Debian omits the minor version" );
			Assert.AreEqual( "GPL-2.0-only", FixSpdxCode( "GPL-2" ), "A bare GNU version is 'only'" );
			Assert.AreEqual( "GPL-2.0-or-later", FixSpdxCode( "GPL-2+" ), "A plus is 'or later'" );
			Assert.AreEqual( "LGPL-2.1-or-later", FixSpdxCode( "LGPL-2.1+" ), "A dotted GNU version is kept" );
			Assert.AreEqual( "GPL-3.0-only", FixSpdxCode( "GPL-3.0" ), "The deprecated SPDX code is updated too" );
			Assert.AreEqual( "GPL-3.0-or-later", FixSpdxCode( "GPL-3.0-or-later" ), "A current SPDX code is unchanged" );
			Assert.AreEqual( "public-domain", FixSpdxCode( "public-domain" ), "An unknown name is unchanged" );

			CollectionAssert.AreEqual( new List<string> { "GPL-2+", "Artistic", "BSD-3-clause" }, DebPackage.ParseLicenseExpression( "GPL-2+ or Artistic, and BSD-3-clause" ), "Commas and lower case operators should be handled" );
			CollectionAssert.AreEqual( new List<string> { "GPL-2+" }, DebPackage.ParseLicenseExpression( "GPL-2+ with OpenSSL exception" ), "An exception is not a license" );
		}

		[TestMethod]
		[TestCategory( "DebVersion" )]
		public void DebCopyrightUsesTheAllFilesParagraph()
		{
			string copyright =
				"Format: https://www.debian.org/doc/packaging-manuals/copyright-format/1.0/\n" +
				"Upstream-Name: example\n" +
				"\n" +
				"Files: *\n" +
				"Copyright: 2020 Jane Doe\n" +
				" 2021 John Doe\n" +
				"License: Expat or GPL-2+\n" +
				"\n" +
				"Files: tests/*\n" +
				"Copyright: 2022 Someone Else\n" +
				"License: BSD-2-clause\n" +
				"\n" +
				"License: Expat\n" +
				" Permission is hereby granted, free of charge,\n" +
				" .\n" +
				" to any person.\n";

			DebPackage package = new DebPackage();
			package.AddLicensesFromCopyright( copyright, "https://example.com/copyright" );

			Assert.HasCount( 2, package.Licenses, "Only the 'Files: *' licenses should be recorded" );
			Assert.AreEqual( "Expat", package.Licenses[0].SpdxCode, "The Debian short name is recorded until the data is cleaned up" );

			LicenseChecker.FixSpdxCodes( package.Licenses );
			Assert.AreEqual( "MIT", package.Licenses[0].SpdxCode, "Expat should map to MIT" );
			Assert.AreEqual( "GPL-2.0-or-later", package.Licenses[1].SpdxCode, "GPL-2+ should map to GPL-2.0-or-later" );
			Assert.AreEqual( "Permission is hereby granted, free of charge,\n\nto any person.", package.Licenses[0].LicenseContent, "The stand-alone paragraph should supply the text" );
			Assert.AreEqual( "https://example.com/copyright", package.Licenses[0].HtmlLicenseUri, "The copyright file should be the license URI" );
			Assert.AreEqual( "Copyright 2020 Jane Doe; 2021 John Doe", package.Copyright, "Copyright mismatch" );
		}

		// ---- CveChecker ----

		[TestMethod]
		[TestCategory( "DebVersion" )]
		public void DebAdvisoryListsItsCvesAsUpstream()
		{
			// A DSA covers several CVEs, and OSV lists them as upstream vulnerabilities rather than as aliases
			OsvVulnerability advisory = new OsvVulnerability
			{
				id = "DSA-5587-1",
				upstream = new List<string> { "CVE-2023-46218", "DEBIAN-CVE-2023-46218", "CVE-2023-46219" }
			};

			CollectionAssert.AreEqual( new List<string> { "CVE-2023-46218", "CVE-2023-46219" }, advisory.FindCveIds(), "Every upstream CVE should be found, and nothing else" );

			OsvVulnerability aliased = new OsvVulnerability
			{
				id = "GHSA-xxxx-xxxx-xxxx",
				aliases = new List<string> { "CVE-2021-12345" },
				upstream = new List<string> { "CVE-2021-12345" }
			};

			CollectionAssert.AreEqual( new List<string> { "CVE-2021-12345" }, aliased.FindCveIds(), "A CVE named twice should only be found once" );
		}

		// ---- PackageUrl ----

		[TestMethod]
		[TestCategory( "DebVersion" )]
		public void DebPurlDropsTheVendor()
		{
			SbomPackage? package = PackageUrl.GetPackage( "pkg:deb/debian/libcurl4t64@1%3A8.14.1-2?arch=amd64&distro=trixie", "libcurl4t64", null, "library" );

			Assert.IsNotNull( package, "The purl should be parsed" );
			Assert.AreEqual( DebPackage.PackageType, package.PackageType, "Package type mismatch" );
			Assert.AreEqual( "libcurl4t64", package.Name, "The vendor namespace is not part of the name" );
			Assert.AreEqual( "1:8.14.1-2", package.Version, "The epoch should be decoded" );
		}
	}
}
