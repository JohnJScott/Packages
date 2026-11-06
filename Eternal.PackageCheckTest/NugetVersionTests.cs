// Copyright Eternal Developments LLC. All Rights Reserved.

using Eternal.PackageCheck;

namespace Eternal.PackageCheckTest
{
	[TestClass]
	public sealed class NugetVersionTests
	{
		private static int Compare( string left, string right )
		{
			return Math.Sign( new NuGetVersion( left ).CompareTo( new NuGetVersion( right ) ) );
		}

		private static bool Satisfies( string version, string range )
		{
			NuGetRequirement requirement = new NuGetRequirement( range );
			return requirement.SatisfiedBy( new NuGetVersion( version ) );
		}

		private static List<string> MakeVersions( params string[] numbers )
		{
			return numbers.ToList();
		}

		// ---- NuGetVersion: parsing ----

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void NuGetVersionParsesDottedParts()
		{
			NuGetVersion version = new NuGetVersion( "13.0.4" );

			CollectionAssert.AreEqual( new int[] { 13, 0, 4 }, version.Parts, "Parts mismatch" );
			Assert.AreEqual( "13.0.4", version.ToString(), "ToString should return the original text" );
			Assert.IsFalse( version.IsPreRelease, "13.0.4 is not a prerelease" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void NuGetVersionParsesFourNumericParts()
		{
			NuGetVersion version = new NuGetVersion( "3.1.3.42154" );

			CollectionAssert.AreEqual( new int[] { 3, 1, 3, 42154 }, version.Parts, "NuGet allows a fourth numeric part" );
			Assert.AreEqual( "3.1.3.42154", version.ToString(), "ToString should return the original text" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void NuGetVersionSeparatesThePrereleaseLabel()
		{
			NuGetVersion version = new NuGetVersion( "8.0.0-preview.2" );

			CollectionAssert.AreEqual( new int[] { 8, 0, 0 }, version.Parts, "The label after '-' is not part of the number" );
			Assert.IsTrue( version.IsPreRelease, "8.0.0-preview.2 is a prerelease" );
			Assert.AreEqual( "8.0.0-preview.2", version.ToString(), "ToString should keep the label" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void NuGetVersionIgnoresBuildMetadata()
		{
			NuGetVersion version = new NuGetVersion( "1.0.0+build.5114f85" );

			CollectionAssert.AreEqual( new int[] { 1, 0, 0 }, version.Parts, "Build metadata is not part of the number" );
			Assert.IsFalse( version.IsPreRelease, "Build metadata does not make a version a prerelease" );
			Assert.AreEqual( "1.0.0+build.5114f85", version.ToString(), "ToString should keep the metadata" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void NuGetVersionTreatsEverythingAfterTheFirstHyphenAsOneLabel()
		{
			// "1.0.0-rc-1" is the single label "rc-1", not "rc" followed by "1", so it is ranked as one string against the "rc" of "rc.1"
			Assert.AreEqual( 0, Compare( "1.0.0-rc-1", "1.0.0-rc-1" ), "The same version compares equal to itself" );
			Assert.AreEqual( 1, Compare( "1.0.0-rc-1", "1.0.0-rc.1" ), "'rc-1' outranks the 'rc' it shares a prefix with" );
			Assert.AreEqual( -1, Compare( "1.0.0-rc-1", "1.0.0-rc1" ), "'rc-1' ranks below 'rc1' because '-' precedes '1' in ASCII" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		[DataRow( "abc" )]
		[DataRow( "" )]
		[DataRow( "1.0.x" )]
		[DataRow( "not-a-version" )]
		[DataRow( "v1.0.0" )]
		public void NuGetVersionRejectsNonNumericSegments( string version )
		{
			Assert.ThrowsExactly<FormatException>( () => new NuGetVersion( version ), $"'{version}' should not parse" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void NuGetVersionTryParseReportsFailureInsteadOfThrowing()
		{
			Assert.IsFalse( NuGetVersion.TryParse( "not-a-version", out NuGetVersion? rejected ), "'not-a-version' should not parse" );
			Assert.IsNull( rejected, "A rejected version should come back null" );

			Assert.IsTrue( NuGetVersion.TryParse( "1.2.3.4", out NuGetVersion? accepted ), "'1.2.3.4' should parse" );
			Assert.IsNotNull( accepted, "An accepted version should not be null" );
			Assert.AreEqual( "1.2.3.4", accepted.ToString(), "The parsed version" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void NuGetVersionPartAtPadsMissingPartsWithZero()
		{
			NuGetVersion version = new NuGetVersion( "1.2" );

			Assert.AreEqual( 1, version.PartAt( 0 ), "Part 0" );
			Assert.AreEqual( 2, version.PartAt( 1 ), "Part 1" );
			Assert.AreEqual( 0, version.PartAt( 2 ), "Part 2 is past the end and should be 0" );
			Assert.AreEqual( 0, version.PartAt( 10 ), "Part 10 is past the end and should be 0" );
		}

		// ---- NuGetVersion: ordering ----

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		[DataRow( "1.0.0", "1.0.0", 0 )]
		[DataRow( "1.0.1", "1.0.0", 1 )]
		[DataRow( "0.9", "1.0", -1 )]
		[DataRow( "2.0", "1.99.99", 1 )]
		// Parts compare numerically, not as text
		[DataRow( "1.10", "1.9", 1 )]
		[DataRow( "10.0", "9.99", 1 )]
		[DataRow( "13.0.10", "13.0.9", 1 )]
		// Missing trailing parts are zero, so these are the same version
		[DataRow( "1.0", "1.0.0", 0 )]
		[DataRow( "1", "1.0.0.0", 0 )]
		[DataRow( "13.0.4", "13.0.4.0", 0 )]
		// ...but a non-zero fourth part makes the longer version higher
		[DataRow( "1.0", "1.0.1", -1 )]
		[DataRow( "1.0.0.1", "1.0", 1 )]
		[DataRow( "3.1.3", "3.1.3.42154", -1 )]
		// Build metadata takes no part in ordering
		[DataRow( "1.0.0+build1", "1.0.0+build2", 0 )]
		[DataRow( "1.0.0+build1", "1.0.0", 0 )]
		public void NuGetVersionCompareTo( string left, string right, int expected )
		{
			Assert.AreEqual( expected, Compare( left, right ), $"'{left}' compared with '{right}'" );
			Assert.AreEqual( -expected, Compare( right, left ), $"'{right}' compared with '{left}' (should be the mirror image)" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void NuGetVersionCompareToNullIsGreater()
		{
			Assert.AreEqual( 1, new NuGetVersion( "1.0" ).CompareTo( null ), "Any version is greater than null" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void NuGetVersionMaxPicksNumericallyHighest()
		{
			List<NuGetVersion> versions = new List<NuGetVersion>
			{
				new NuGetVersion( "1.9" ),
				new NuGetVersion( "1.10" ),
				new NuGetVersion( "1.2" ),
				new NuGetVersion( "1.0.0" ),
				new NuGetVersion( "1.0" )
			};

			Assert.AreEqual( "1.10", versions.Max()!.ToString(), "1.10 is higher than 1.9" );
		}

		// ---- NuGetVersion: SemVer 2.0 prerelease ordering ----

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		// A release always outranks a prerelease of the same number
		[DataRow( "1.0.0-alpha", "1.0.0", -1 )]
		[DataRow( "13.0.4-beta1", "13.0.4", -1 )]
		// ...but the numeric parts are compared first
		[DataRow( "1.0.1-alpha", "1.0.0", 1 )]
		[DataRow( "9.0.2-beta2", "9.0.1", 1 )]
		// Labels compare alphabetically, without regard to case
		[DataRow( "1.0.0-alpha", "1.0.0-beta", -1 )]
		[DataRow( "1.0.0-beta", "1.0.0-rc", -1 )]
		[DataRow( "1.0.0-RC1", "1.0.0-rc1", 0 )]
		[DataRow( "1.0.0-Beta", "1.0.0-beta", 0 )]
		// A shorter run of labels ranks below a longer run that shares its prefix
		[DataRow( "1.0.0-alpha", "1.0.0-alpha.1", -1 )]
		[DataRow( "1.0.0-rc", "1.0.0-rc.0", -1 )]
		// Numeric labels compare numerically, not as text
		[DataRow( "1.0.0-rc.2", "1.0.0-rc.10", -1 )]
		[DataRow( "1.0.0-alpha.9", "1.0.0-alpha.10", -1 )]
		// A numeric label always ranks below an alphanumeric one
		[DataRow( "1.0.0-1", "1.0.0-alpha", -1 )]
		[DataRow( "1.0.0-alpha.1", "1.0.0-alpha.beta", -1 )]
		// Both prereleases and equal
		[DataRow( "1.0.0-preview.2", "1.0.0-preview.2", 0 )]
		public void NuGetVersionPrereleaseOrdering( string left, string right, int expected )
		{
			Assert.AreEqual( expected, Compare( left, right ), $"'{left}' compared with '{right}'" );
			Assert.AreEqual( -expected, Compare( right, left ), $"'{right}' compared with '{left}' (should be the mirror image)" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void NuGetVersionPrereleaseOrderingFollowsTheSemVerExample()
		{
			// The ordering given in the SemVer 2.0 specification
			string[] ascending = ["1.0.0-alpha", "1.0.0-alpha.1", "1.0.0-alpha.beta", "1.0.0-beta", "1.0.0-beta.2", "1.0.0-beta.11", "1.0.0-rc.1", "1.0.0"];

			for( int i = 1; i < ascending.Length; i++ )
			{
				Assert.AreEqual( -1, Compare( ascending[i - 1], ascending[i] ), $"'{ascending[i - 1]}' should rank below '{ascending[i]}'" );
			}
		}

		// ---- NuGetRequirement: interval notation ----

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		// A bare version is a MINIMUM in NuGet, unlike RubyGems where it is an exact match
		[DataRow( "1.0.0", "1.0.0", true )]
		[DataRow( "1.0.0", "1.5.0", true )]
		[DataRow( "1.0.0", "99.0.0", true )]
		[DataRow( "1.0.0", "0.9.0", false )]
		// "[1.0.0]" is the only form that pins a single version
		[DataRow( "[1.0.0]", "1.0.0", true )]
		[DataRow( "[1.0.0]", "1.0.1", false )]
		[DataRow( "[1.0.0]", "0.9.9", false )]
		[DataRow( "[1.0]", "1.0.0", true )]
		// Minimum, inclusive and exclusive
		[DataRow( "[1.0.0, )", "1.0.0", true )]
		[DataRow( "[1.0.0, )", "0.9.9", false )]
		[DataRow( "(1.0.0, )", "1.0.0", false )]
		[DataRow( "(1.0.0, )", "1.0.1", true )]
		// Maximum, inclusive and exclusive
		[DataRow( "(, 1.0.0]", "1.0.0", true )]
		[DataRow( "(, 1.0.0]", "1.0.1", false )]
		[DataRow( "(, 1.0.0)", "1.0.0", false )]
		[DataRow( "(, 1.0.0)", "0.9.9", true )]
		// Both bounds
		[DataRow( "[1.0.0, 2.0.0)", "1.0.0", true )]
		[DataRow( "[1.0.0, 2.0.0)", "1.9.9", true )]
		[DataRow( "[1.0.0, 2.0.0)", "2.0.0", false )]
		[DataRow( "[1.0.0, 2.0.0)", "0.9.9", false )]
		[DataRow( "[1.0.0, 2.0.0]", "2.0.0", true )]
		[DataRow( "(1.0.0, 2.0.0)", "1.0.0", false )]
		[DataRow( "(1.0.0, 2.0.0)", "2.0.0", false )]
		[DataRow( "(1.0.0, 2.0.0)", "1.5.0", true )]
		// Unbounded forms accept anything
		[DataRow( "(, )", "0.0.1", true )]
		[DataRow( "(, )", "99.0.0", true )]
		[DataRow( "", "0.0.1", true )]
		[DataRow( "", "99.0.0", true )]
		public void NuGetRequirementIntervals( string range, string version, bool expected )
		{
			Assert.AreEqual( expected, Satisfies( version, range ), $"'{version}' against '{range}'" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		[DataRow( "[1.0.0,2.0.0)", "1.5.0", true )]
		[DataRow( "[ 1.0.0 , 2.0.0 )", "1.5.0", true )]
		[DataRow( "  [1.0.0, 2.0.0)  ", "1.5.0", true )]
		[DataRow( "  [1.0.0, 2.0.0)  ", "2.0.0", false )]
		[DataRow( "[1.0.0,)", "1.5.0", true )]
		[DataRow( "   ", "1.5.0", true )]
		public void NuGetRequirementToleratesWhitespace( string range, string version, bool expected )
		{
			Assert.AreEqual( expected, Satisfies( version, range ), $"'{version}' against '{range}'" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void NuGetRequirementComparesPrereleasesAgainstTheBounds()
		{
			Assert.IsFalse( Satisfies( "1.0.0-rc1", "[1.0.0, )" ), "A prerelease ranks below the release it precedes" );
			Assert.IsTrue( Satisfies( "1.0.0-rc1", "[1.0.0-beta, )" ), "1.0.0-rc1 is above the 1.0.0-beta lower bound" );
			Assert.IsTrue( Satisfies( "2.0.0-alpha", "[1.0.0, 3.0.0)" ), "A prerelease inside the numeric range still satisfies it" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void NuGetRequirementContradictoryBoundsMatchNothing()
		{
			Assert.IsFalse( Satisfies( "0.5", "[2.0.0, 1.0.0]" ), "0.5" );
			Assert.IsFalse( Satisfies( "1.5", "[2.0.0, 1.0.0]" ), "1.5" );
			Assert.IsFalse( Satisfies( "3.0", "[2.0.0, 1.0.0]" ), "3.0" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		[DataRow( "[garbage" )]
		[DataRow( "[1.0.0" )]
		[DataRow( "(2.0.0)" )]
		[DataRow( "[]" )]
		[DataRow( "[1.0.0, 2.0.0, 3.0.0)" )]
		public void NuGetRequirementMalformedRangeAcceptsAnyVersion( string range )
		{
			// A range that cannot be understood is logged and dropped rather than silently excluding every version
			Assert.IsTrue( Satisfies( "0.0.1", range ), $"'0.0.1' against '{range}'" );
			Assert.IsTrue( Satisfies( "99.0.0", range ), $"'99.0.0' against '{range}'" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		[DataRow( "[not-a-version]" )]
		[DataRow( "[1.0.0, abc)" )]
		[DataRow( "[abc, 2.0.0)" )]
		[DataRow( "abc" )]
		public void NuGetRequirementRejectsUnparsableBounds( string range )
		{
			Assert.ThrowsExactly<FormatException>( () => new NuGetRequirement( range ), $"'{range}' should not parse" );
		}

		// ---- NuGetVersionMatcher: selection from a version list ----

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void SelectMatchingVersionComparesNumericallyAndSkipsPrereleases()
		{
			// 9.0.2-beta2 is higher than 9.0.1 but must not be chosen, and 9.0.10 is lexically lower than 9.0.9
			List<string> versions = MakeVersions( "9.0.1-beta1", "9.0.1", "9.0.9", "9.0.10", "9.0.11-beta1", "10.0.1" );

			NuGetVersion? match = NuGetVersionMatcher.SelectMatchingVersion( "Newtonsoft.Json", versions, "[9.0.0, 10.0.0)" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "9.0.10", match.ToString(), "Highest stable version in range" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void SelectMatchingVersionIgnoresOrderOfTheList()
		{
			List<string> versions = MakeVersions( "1.9", "1.10", "1.2" );

			NuGetVersion? forward = NuGetVersionMatcher.SelectMatchingVersion( "Package", versions, "" );
			versions.Reverse();
			NuGetVersion? backward = NuGetVersionMatcher.SelectMatchingVersion( "Package", versions, "" );

			Assert.IsNotNull( forward, "No version found for the original order" );
			Assert.IsNotNull( backward, "No version found for the reversed order" );
			Assert.AreEqual( "1.10", forward.ToString(), "Original order" );
			Assert.AreEqual( "1.10", backward.ToString(), "Reversed order" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void SelectMatchingVersionWithNoRangeReturnsHighestStable()
		{
			List<string> versions = MakeVersions( "9.9", "10.0", "11.0.0-beta1" );

			NuGetVersion? match = NuGetVersionMatcher.SelectMatchingVersion( "Package", versions, "" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "10.0", match.ToString(), "The prerelease must not be chosen even though it is higher" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void SelectMatchingVersionHonoursAnExclusiveUpperBound()
		{
			// The case that matters for dependencies: the latest version is outside the range the package asked for
			List<string> versions = MakeVersions( "1.0.0", "2.0.0", "2.2.0", "3.0.0", "3.1.0", "3.2.0" );

			NuGetVersion? match = NuGetVersionMatcher.SelectMatchingVersion( "SixLabors.Fonts", versions, "[1.0.0, 3.0.0)" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "2.2.0", match.ToString(), "The highest version below 3.0.0, not the latest published version" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void SelectMatchingVersionExactPinReturnsThatVersion()
		{
			List<string> versions = MakeVersions( "1.8.20", "1.8.25", "1.9.0" );

			NuGetVersion? match = NuGetVersionMatcher.SelectMatchingVersion( "Hangfire.Core", versions, "[1.8.25, 1.8.25]" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "1.8.25", match.ToString(), "A pinned range selects exactly that version" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void SelectMatchingVersionToleratesDuplicateVersionNumbers()
		{
			List<string> versions = MakeVersions( "3.1.21", "3.1.21", "3.1.20", "3.1.22" );

			NuGetVersion? match = NuGetVersionMatcher.SelectMatchingVersion( "Package", versions, "(, 3.1.21]" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "3.1.21", match.ToString(), "Highest version at or below 3.1.21" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void SelectMatchingVersionSkipsUnparsableVersionsRatherThanFailing()
		{
			// One bad entry must not hide every good one
			List<string> versions = MakeVersions( "1.0.0", "not-a-version", "2.0.0" );

			NuGetVersion? match = NuGetVersionMatcher.SelectMatchingVersion( "Package", versions, "[1.0.0, )" );

			Assert.IsNotNull( match, "The unparsable entry should be skipped, not abandon the selection" );
			Assert.AreEqual( "2.0.0", match.ToString(), "Highest parsable version" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void SelectMatchingVersionReturnsNullWhenNothingQualifies()
		{
			Assert.IsNull( NuGetVersionMatcher.SelectMatchingVersion( "Package", MakeVersions( "1.0", "2.0" ), "[3.0, )" ), "No version satisfies the range" );
			Assert.IsNull( NuGetVersionMatcher.SelectMatchingVersion( "Package", MakeVersions( "1.0.0-rc1", "1.0.0-beta2" ), "" ), "Only prereleases exist" );
			Assert.IsNull( NuGetVersionMatcher.SelectMatchingVersion( "Package", MakeVersions(), "" ), "There are no versions at all" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void SelectMatchingVersionInvalidRangeReturnsNull()
		{
			NuGetVersion? match = NuGetVersionMatcher.SelectMatchingVersion( "Package", MakeVersions( "1.0", "2.0" ), "[not-a-version, )" );

			Assert.IsNull( match, "An unparsable range should log and return null rather than throw" );
		}

		// The cases below read live data from NuGet.org. The expected versions are all in ranges that have been closed for years, so they are stable.

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void FindMatchingVersionPicksHighestInRange()
		{
			NuGetVersion? match = NuGetVersionMatcher.FindMatchingVersion( "Newtonsoft.Json", "[12.0.0, 13.0.0)" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "12.0.3", match.ToString(), "Highest Newtonsoft.Json 12.x release" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void FindMatchingVersionSkipsPrereleasesThatOutrankTheHighestRelease()
		{
			// 9.0.2-beta1 and 9.0.2-beta2 are both higher than 9.0.1, and neither may be chosen
			NuGetVersion? match = NuGetVersionMatcher.FindMatchingVersion( "Newtonsoft.Json", "[9.0.0, 10.0.0)" );

			Assert.IsNotNull( match, "No version found; prereleases were probably not filtered out" );
			Assert.AreEqual( "9.0.1", match.ToString(), "Highest Newtonsoft.Json 9.x release" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void FindMatchingVersionHonoursAnExclusiveUpperBound()
		{
			NuGetVersion? match = NuGetVersionMatcher.FindMatchingVersion( "Newtonsoft.Json", "(, 4.0.0)" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "3.5.8", match.ToString(), "The only Newtonsoft.Json release below 4.0.0" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void FindMatchingVersionExactPin()
		{
			NuGetVersion? match = NuGetVersionMatcher.FindMatchingVersion( "Newtonsoft.Json", "[12.0.1]" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "12.0.1", match.ToString(), "A pinned range selects exactly that version" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void FindMatchingVersionToleratesFourPartVersions()
		{
			// Antlr publishes 3.1.3.42154, 3.4.1.9004-pre, 3.4.1.9004 and 3.5.0.2
			NuGetVersion? match = NuGetVersionMatcher.FindMatchingVersion( "Antlr", "[3.1.3, 3.5.0)" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "3.4.1.9004", match.ToString(), "Highest Antlr release below 3.5.0" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void FindMatchingVersionNoRangeReturnsLatestStable()
		{
			NuGetVersion? match = NuGetVersionMatcher.FindMatchingVersion( "Newtonsoft.Json", "" );

			Assert.IsNotNull( match, "No version found" );
			Assert.IsGreaterThanOrEqualTo( 0, match.CompareTo( new NuGetVersion( "13.0.4" ) ), $"'{match}' should be at least 13.0.4" );
			Assert.IsFalse( match.IsPreRelease, $"'{match}' should not be a prerelease" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void FindMatchingVersionNoSatisfyingVersionReturnsNull()
		{
			NuGetVersion? match = NuGetVersionMatcher.FindMatchingVersion( "Newtonsoft.Json", "[99.0.0, )" );

			Assert.IsNull( match, "No Newtonsoft.Json version is >= 99.0.0" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void FindMatchingVersionUnknownPackageReturnsNull()
		{
			NuGetVersion? match = NuGetVersionMatcher.FindMatchingVersion( "Eternal.NoSuchPackage.9f3a7c", "[0.0.0, )" );

			Assert.IsNull( match, "A package that does not exist has no versions" );
		}

		// ---- NuGetPackage.GetNuGetPackage: blank, a specific version, or a range (live, as above) ----

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void GetNuGetPackageBlankRequirementsUseTheLatestVersion()
		{
			NuGetPackage? latest_package = NuGetPackage.GetNuGetPackage( "Newtonsoft.Json", "", false );
			NuGetPackage? whitespace_package = NuGetPackage.GetNuGetPackage( "Newtonsoft.Json", "   ", false );

			Assert.IsNotNull( latest_package, "No package found for blank requirements" );
			Assert.IsNotNull( whitespace_package, "No package found for whitespace requirements" );
			Assert.IsGreaterThanOrEqualTo( 0, new NuGetVersion( latest_package.Version ).CompareTo( new NuGetVersion( "13.0.4" ) ), $"'{latest_package.Version}' should be at least 13.0.4" );
			Assert.AreEqual( latest_package.Version, whitespace_package.Version, "Whitespace should be the same as blank" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		[DataRow( "Newtonsoft.Json", "12.0.3", "12.0.3" )]
		// A prerelease can be requested by its exact number, even though it can never satisfy a range
		[DataRow( "Newtonsoft.Json", "13.0.1-beta1", "13.0.1-beta1" )]
		[DataRow( "Antlr", "3.1.3.42154", "3.1.3.42154" )]
		public void GetNuGetPackageSpecificVersion( string packageName, string requirements, string expectedVersion )
		{
			NuGetPackage? nuget_package = NuGetPackage.GetNuGetPackage( packageName, requirements, false );

			Assert.IsNotNull( nuget_package, $"No package found for '{packageName}' '{requirements}'" );
			Assert.AreEqual( expectedVersion, nuget_package.Version, $"Version for '{packageName}' '{requirements}'" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		[DataRow( "Newtonsoft.Json", "[12.0.0, 13.0.0)", "12.0.3" )]
		// 9.0.2-beta1 and 9.0.2-beta2 both outrank 9.0.1 and must be skipped
		[DataRow( "Newtonsoft.Json", "[9.0.0, 10.0.0)", "9.0.1" )]
		[DataRow( "Newtonsoft.Json", "(, 4.0.0)", "3.5.8" )]
		[DataRow( "Newtonsoft.Json", "[12.0.1]", "12.0.1" )]
		[DataRow( "Newtonsoft.Json", "[6.0.0, 6.1.0)", "6.0.8" )]
		[DataRow( "Antlr", "[3.1.3, 3.5.0)", "3.4.1.9004" )]
		public void GetNuGetPackageRangeSelectsTheHighestSatisfyingVersion( string packageName, string requirements, string expectedVersion )
		{
			NuGetPackage? nuget_package = NuGetPackage.GetNuGetPackage( packageName, requirements, false );

			Assert.IsNotNull( nuget_package, $"No package found for '{packageName}' '{requirements}'" );
			Assert.AreEqual( expectedVersion, nuget_package.Version, $"Version for '{packageName}' '{requirements}'" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		public void GetNuGetPackageBareVersionIsAMinimumWhenItWasNeverPublished()
		{
			// "12.0.3" is a published number so it is taken as it is, but "12.0.3.0" is not, so it falls through to the range
			// parser, where a bare version means ">= 12.0.3.0" rather than an exact match
			NuGetPackage? published_package = NuGetPackage.GetNuGetPackage( "Newtonsoft.Json", "12.0.3", false );
			NuGetPackage? unpublished_package = NuGetPackage.GetNuGetPackage( "Newtonsoft.Json", "12.0.3.0", false );

			Assert.IsNotNull( published_package, "No package found for '12.0.3'" );
			Assert.IsNotNull( unpublished_package, "No package found for '12.0.3.0'" );
			Assert.AreEqual( "12.0.3", published_package.Version, "A published number is used as it is" );
			Assert.IsGreaterThanOrEqualTo( 0, new NuGetVersion( unpublished_package.Version ).CompareTo( new NuGetVersion( "13.0.4" ) ), $"'{unpublished_package.Version}' should be at least 13.0.4" );
		}

		[TestMethod]
		[TestCategory( "NuGetVersion" )]
		[DataRow( "Newtonsoft.Json", "[99.0.0, )" )]
		[DataRow( "Newtonsoft.Json", "[99.0.0]" )]
		[DataRow( "Newtonsoft.Json", "[not-a-version, )" )]
		[DataRow( "Eternal.NoSuchPackage.9f3a7c", "" )]
		public void GetNuGetPackageWithNoSatisfyingVersionReturnsNull( string packageName, string requirements )
		{
			NuGetPackage? nuget_package = NuGetPackage.GetNuGetPackage( packageName, requirements, false );

			Assert.IsNull( nuget_package, $"No package should be found for '{packageName}' '{requirements}'" );
		}
	}
}
