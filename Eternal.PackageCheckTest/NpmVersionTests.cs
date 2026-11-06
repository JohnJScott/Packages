// Copyright Eternal Developments LLC. All Rights Reserved.

using Eternal.PackageCheck;

namespace Eternal.PackageCheckTest
{
	[TestClass]
	public sealed class NpmVersionTests
	{
		private static int Compare( string left, string right )
		{
			return Math.Sign( new NpmVersion( left ).CompareTo( new NpmVersion( right ) ) );
		}

		private static bool Satisfies( string version, string range )
		{
			NpmRequirement requirement = new NpmRequirement( range );
			return requirement.SatisfiedBy( new NpmVersion( version ) );
		}

		private static List<string> MakeVersions( params string[] numbers )
		{
			return numbers.ToList();
		}

		// ---- NpmVersion: parsing ----

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void NpmVersionParsesDottedParts()
		{
			NpmVersion version = new NpmVersion( "4.17.21" );

			CollectionAssert.AreEqual( new int[] { 4, 17, 21 }, version.Parts, "Parts mismatch" );
			Assert.AreEqual( "4.17.21", version.ToString(), "ToString should return the original text" );
			Assert.IsFalse( version.IsPreRelease, "4.17.21 is not a prerelease" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void NpmVersionSeparatesThePrereleaseLabel()
		{
			NpmVersion version = new NpmVersion( "7.0.0-beta.3" );

			CollectionAssert.AreEqual( new int[] { 7, 0, 0 }, version.Parts, "The label after '-' is not part of the number" );
			Assert.IsTrue( version.IsPreRelease, "7.0.0-beta.3 is a prerelease" );
			Assert.AreEqual( "7.0.0-beta.3", version.ToString(), "ToString should keep the label" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void NpmVersionIgnoresBuildMetadata()
		{
			NpmVersion version = new NpmVersion( "1.0.0+20130313144700" );

			CollectionAssert.AreEqual( new int[] { 1, 0, 0 }, version.Parts, "Build metadata is not part of the number" );
			Assert.IsFalse( version.IsPreRelease, "Build metadata does not make a version a prerelease" );
			Assert.AreEqual( "1.0.0+20130313144700", version.ToString(), "ToString should keep the metadata" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void NpmVersionAcceptsALeadingV()
		{
			NpmVersion version = new NpmVersion( "v1.2.3" );

			CollectionAssert.AreEqual( new int[] { 1, 2, 3 }, version.Parts, "A leading 'v' is not part of the number" );
			Assert.AreEqual( 0, Compare( "v1.2.3", "1.2.3" ), "'v1.2.3' and '1.2.3' are the same version" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		[DataRow( "abc" )]
		[DataRow( "" )]
		[DataRow( "1.0.x" )]
		[DataRow( "not-a-version" )]
		[DataRow( "^1.0.0" )]
		[DataRow( "latest" )]
		public void NpmVersionRejectsNonNumericSegments( string version )
		{
			Assert.ThrowsExactly<FormatException>( () => new NpmVersion( version ), $"'{version}' should not parse" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void NpmVersionTryParseReportsFailureInsteadOfThrowing()
		{
			Assert.IsFalse( NpmVersion.TryParse( "not-a-version", out NpmVersion? rejected ), "'not-a-version' should not parse" );
			Assert.IsNull( rejected, "A rejected version should come back null" );

			Assert.IsTrue( NpmVersion.TryParse( "1.2.3", out NpmVersion? accepted ), "'1.2.3' should parse" );
			Assert.IsNotNull( accepted, "An accepted version should not be null" );
			Assert.AreEqual( "1.2.3", accepted.ToString(), "The parsed version" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void NpmVersionPartAtPadsMissingPartsWithZero()
		{
			NpmVersion version = new NpmVersion( "1.2" );

			Assert.AreEqual( 1, version.PartAt( 0 ), "Part 0" );
			Assert.AreEqual( 2, version.PartAt( 1 ), "Part 1" );
			Assert.AreEqual( 0, version.PartAt( 2 ), "Part 2 is past the end and should be 0" );
			Assert.AreEqual( 0, version.PartAt( 10 ), "Part 10 is past the end and should be 0" );
		}

		// ---- NpmVersion: ordering ----

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		[DataRow( "1.0.0", "1.0.0", 0 )]
		[DataRow( "1.0.1", "1.0.0", 1 )]
		[DataRow( "0.9.0", "1.0.0", -1 )]
		[DataRow( "2.0.0", "1.99.99", 1 )]
		// Parts compare numerically, not as text
		[DataRow( "1.10.0", "1.9.0", 1 )]
		[DataRow( "10.0.0", "9.99.0", 1 )]
		[DataRow( "4.17.21", "4.17.9", 1 )]
		[DataRow( "3.10.1", "3.9.3", 1 )]
		// Missing trailing parts are zero
		[DataRow( "1.0", "1.0.0", 0 )]
		[DataRow( "1", "1.0.0", 0 )]
		// Build metadata takes no part in ordering
		[DataRow( "1.0.0+build1", "1.0.0+build2", 0 )]
		[DataRow( "1.0.0+build1", "1.0.0", 0 )]
		public void NpmVersionCompareTo( string left, string right, int expected )
		{
			Assert.AreEqual( expected, Compare( left, right ), $"'{left}' compared with '{right}'" );
			Assert.AreEqual( -expected, Compare( right, left ), $"'{right}' compared with '{left}' (should be the mirror image)" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void NpmVersionCompareToNullIsGreater()
		{
			Assert.AreEqual( 1, new NpmVersion( "1.0.0" ).CompareTo( null ), "Any version is greater than null" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void NpmVersionMaxPicksNumericallyHighest()
		{
			List<NpmVersion> versions = new List<NpmVersion>
			{
				new NpmVersion( "3.9.3" ),
				new NpmVersion( "3.10.1" ),
				new NpmVersion( "3.1.0" ),
				new NpmVersion( "3.0.0" )
			};

			Assert.AreEqual( "3.10.1", versions.Max()!.ToString(), "3.10.1 is higher than 3.9.3" );
		}

		// ---- NpmVersion: SemVer 2.0 prerelease ordering ----

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		// A release always outranks a prerelease of the same number
		[DataRow( "1.0.0-alpha", "1.0.0", -1 )]
		[DataRow( "7.0.0-beta.3", "7.0.0", -1 )]
		// ...but the numeric parts are compared first
		[DataRow( "1.0.1-alpha", "1.0.0", 1 )]
		// Labels compare alphabetically
		[DataRow( "1.0.0-alpha", "1.0.0-beta", -1 )]
		[DataRow( "1.0.0-beta", "1.0.0-rc", -1 )]
		// A shorter run of labels ranks below a longer run that shares its prefix
		[DataRow( "1.0.0-alpha", "1.0.0-alpha.1", -1 )]
		// Numeric labels compare numerically, not as text
		[DataRow( "1.0.0-rc.2", "1.0.0-rc.10", -1 )]
		[DataRow( "1.0.0-beta.2", "1.0.0-beta.11", -1 )]
		// A numeric label always ranks below an alphanumeric one
		[DataRow( "1.0.0-1", "1.0.0-alpha", -1 )]
		[DataRow( "1.0.0-alpha.1", "1.0.0-alpha.beta", -1 )]
		// Unlike NuGet, npm compares the labels case sensitively, and upper case sorts first
		[DataRow( "1.0.0-RC1", "1.0.0-rc1", -1 )]
		[DataRow( "1.0.0-Beta", "1.0.0-beta", -1 )]
		// Both prereleases and equal
		[DataRow( "1.0.0-beta.3", "1.0.0-beta.3", 0 )]
		public void NpmVersionPrereleaseOrdering( string left, string right, int expected )
		{
			Assert.AreEqual( expected, Compare( left, right ), $"'{left}' compared with '{right}'" );
			Assert.AreEqual( -expected, Compare( right, left ), $"'{right}' compared with '{left}' (should be the mirror image)" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void NpmVersionPrereleaseOrderingFollowsTheSemVerExample()
		{
			// The ordering given in the SemVer 2.0 specification
			string[] ascending = ["1.0.0-alpha", "1.0.0-alpha.1", "1.0.0-alpha.beta", "1.0.0-beta", "1.0.0-beta.2", "1.0.0-beta.11", "1.0.0-rc.1", "1.0.0"];

			for( int i = 1; i < ascending.Length; i++ )
			{
				Assert.AreEqual( -1, Compare( ascending[i - 1], ascending[i] ), $"'{ascending[i - 1]}' should rank below '{ascending[i]}'" );
			}
		}

		// ---- NpmRequirement: caret ranges ----

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		// "^1.2.3" allows anything up to but not including 2.0.0
		[DataRow( "^1.2.3", "1.2.3", true )]
		[DataRow( "^1.2.3", "1.2.4", true )]
		[DataRow( "^1.2.3", "1.9.9", true )]
		[DataRow( "^1.2.3", "2.0.0", false )]
		[DataRow( "^1.2.3", "1.2.2", false )]
		// A zero major locks the minor: "^0.2.3" stops below 0.3.0
		[DataRow( "^0.2.3", "0.2.3", true )]
		[DataRow( "^0.2.9", "0.2.9", true )]
		[DataRow( "^0.2.3", "0.2.99", true )]
		[DataRow( "^0.2.3", "0.3.0", false )]
		[DataRow( "^0.2.3", "0.2.2", false )]
		// A zero major and minor lock the patch: "^0.0.3" stops below 0.0.4
		[DataRow( "^0.0.3", "0.0.3", true )]
		[DataRow( "^0.0.3", "0.0.4", false )]
		[DataRow( "^0.0.3", "0.1.0", false )]
		// Partial and wildcard forms
		[DataRow( "^1.2", "1.9.9", true )]
		[DataRow( "^1.2", "2.0.0", false )]
		[DataRow( "^1.2", "1.1.9", false )]
		[DataRow( "^1.x", "1.9.9", true )]
		[DataRow( "^1.x", "2.0.0", false )]
		[DataRow( "^0.0", "0.0.9", true )]
		[DataRow( "^0.0", "0.1.0", false )]
		[DataRow( "^0.0.x", "0.0.9", true )]
		[DataRow( "^0.0.x", "0.1.0", false )]
		[DataRow( "^0.x", "0.9.9", true )]
		[DataRow( "^0.x", "1.0.0", false )]
		[DataRow( "^0", "0.9.9", true )]
		[DataRow( "^0", "1.0.0", false )]
		public void NpmRequirementCaretRanges( string range, string version, bool expected )
		{
			Assert.AreEqual( expected, Satisfies( version, range ), $"'{version}' against '{range}'" );
		}

		// ---- NpmRequirement: tilde ranges ----

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		// "~1.2.3" allows patch level changes
		[DataRow( "~1.2.3", "1.2.3", true )]
		[DataRow( "~1.2.3", "1.2.99", true )]
		[DataRow( "~1.2.3", "1.3.0", false )]
		[DataRow( "~1.2.3", "1.2.2", false )]
		// "~1.2" is the same as "~1.2.0"
		[DataRow( "~1.2", "1.2.0", true )]
		[DataRow( "~1.2", "1.2.99", true )]
		[DataRow( "~1.2", "1.3.0", false )]
		// "~1" allows minor level changes
		[DataRow( "~1", "1.0.0", true )]
		[DataRow( "~1", "1.99.99", true )]
		[DataRow( "~1", "2.0.0", false )]
		// A zero major behaves no differently for tilde
		[DataRow( "~0.2.3", "0.2.99", true )]
		[DataRow( "~0.2.3", "0.3.0", false )]
		[DataRow( "~0.2", "0.2.99", true )]
		[DataRow( "~0.2", "0.3.0", false )]
		[DataRow( "~0", "0.99.99", true )]
		[DataRow( "~0", "1.0.0", false )]
		// npm accepts "~>" as a synonym for "~"
		[DataRow( "~>1.2.3", "1.2.99", true )]
		[DataRow( "~>1.2.3", "1.3.0", false )]
		public void NpmRequirementTildeRanges( string range, string version, bool expected )
		{
			Assert.AreEqual( expected, Satisfies( version, range ), $"'{version}' against '{range}'" );
		}

		// ---- NpmRequirement: wildcards and partial versions ----

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		// A bare wildcard accepts anything
		[DataRow( "*", "0.0.1", true )]
		[DataRow( "*", "99.0.0", true )]
		[DataRow( "x", "1.2.3", true )]
		[DataRow( "", "1.2.3", true )]
		// "1.x" stands for the whole of the 1 release line
		[DataRow( "1.x", "1.0.0", true )]
		[DataRow( "1.x", "1.99.99", true )]
		[DataRow( "1.x", "2.0.0", false )]
		[DataRow( "1.x", "0.9.9", false )]
		[DataRow( "1", "1.5.0", true )]
		[DataRow( "1", "2.0.0", false )]
		// "1.2.x" stands for the whole of the 1.2 line
		[DataRow( "1.2.x", "1.2.0", true )]
		[DataRow( "1.2.x", "1.2.99", true )]
		[DataRow( "1.2.x", "1.3.0", false )]
		[DataRow( "1.2", "1.2.5", true )]
		[DataRow( "1.2", "1.3.0", false )]
		[DataRow( "1.2.*", "1.2.5", true )]
		// A complete version is an exact match
		[DataRow( "1.2.3", "1.2.3", true )]
		[DataRow( "1.2.3", "1.2.4", false )]
		[DataRow( "=1.2.3", "1.2.3", true )]
		[DataRow( "=1.2.3", "1.2.4", false )]
		[DataRow( "v1.2.3", "1.2.3", true )]
		public void NpmRequirementWildcards( string range, string version, bool expected )
		{
			Assert.AreEqual( expected, Satisfies( version, range ), $"'{version}' against '{range}'" );
		}

		// ---- NpmRequirement: comparators ----

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		[DataRow( ">1.2.3", "1.2.4", true )]
		[DataRow( ">1.2.3", "1.2.3", false )]
		[DataRow( ">=1.2.3", "1.2.3", true )]
		[DataRow( ">=1.2.3", "1.2.2", false )]
		[DataRow( "<1.2.3", "1.2.2", true )]
		[DataRow( "<1.2.3", "1.2.3", false )]
		[DataRow( "<=1.2.3", "1.2.3", true )]
		[DataRow( "<=1.2.3", "1.2.4", false )]
		// A partial version after a comparator stands for its whole release line, which shifts where the bound falls
		[DataRow( ">1.x", "2.0.0", true )]
		[DataRow( ">1.x", "1.99.99", false )]
		[DataRow( ">=1.x", "1.0.0", true )]
		[DataRow( ">=1.x", "0.9.9", false )]
		[DataRow( "<1.2.x", "1.1.9", true )]
		[DataRow( "<1.2.x", "1.2.0", false )]
		[DataRow( "<=1.2.x", "1.2.99", true )]
		[DataRow( "<=1.2.x", "1.3.0", false )]
		// A wildcard after a comparator still accepts anything
		[DataRow( ">=*", "0.0.1", true )]
		public void NpmRequirementComparators( string range, string version, bool expected )
		{
			Assert.AreEqual( expected, Satisfies( version, range ), $"'{version}' against '{range}'" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		[DataRow( ">= 1.2.3", "1.2.3", true )]
		[DataRow( ">=  1.2.3", "1.2.2", false )]
		[DataRow( "^ 1.2.3", "1.9.9", true )]
		[DataRow( "  ^1.2.3  ", "1.9.9", true )]
		[DataRow( ">=1.2.7   <1.3.0", "1.2.8", true )]
		public void NpmRequirementToleratesWhitespace( string range, string version, bool expected )
		{
			Assert.AreEqual( expected, Satisfies( version, range ), $"'{version}' against '{range}'" );
		}

		// ---- NpmRequirement: hyphen ranges ----

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		// "1.2.3 - 2.3.4" takes in both ends
		[DataRow( "1.2.3 - 2.3.4", "1.2.3", true )]
		[DataRow( "1.2.3 - 2.3.4", "2.3.4", true )]
		[DataRow( "1.2.3 - 2.3.4", "2.0.0", true )]
		[DataRow( "1.2.3 - 2.3.4", "1.2.2", false )]
		[DataRow( "1.2.3 - 2.3.4", "2.3.5", false )]
		// A partial upper bound takes in the whole of that line, so "- 2.3" stops below 2.4.0
		[DataRow( "1.2.3 - 2.3", "2.3.99", true )]
		[DataRow( "1.2.3 - 2.3", "2.4.0", false )]
		[DataRow( "1.2.3 - 2", "2.99.99", true )]
		[DataRow( "1.2.3 - 2", "3.0.0", false )]
		// A partial lower bound starts at the bottom of its line
		[DataRow( "1.2 - 2.3.4", "1.2.0", true )]
		[DataRow( "1.2 - 2.3.4", "1.1.9", false )]
		public void NpmRequirementHyphenRanges( string range, string version, bool expected )
		{
			Assert.AreEqual( expected, Satisfies( version, range ), $"'{version}' against '{range}'" );
		}

		// ---- NpmRequirement: comparator sets and alternatives ----

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void NpmRequirementSpaceSeparatedComparatorsAreAllRequired()
		{
			// The example from npm's own documentation for ">=1.2.7 <1.3.0"
			Assert.IsTrue( Satisfies( "1.2.7", ">=1.2.7 <1.3.0" ), "1.2.7 is the inclusive lower bound" );
			Assert.IsTrue( Satisfies( "1.2.8", ">=1.2.7 <1.3.0" ), "1.2.8 is inside the range" );
			Assert.IsTrue( Satisfies( "1.2.99", ">=1.2.7 <1.3.0" ), "1.2.99 is inside the range" );
			Assert.IsFalse( Satisfies( "1.2.6", ">=1.2.7 <1.3.0" ), "1.2.6 is below the range" );
			Assert.IsFalse( Satisfies( "1.3.0", ">=1.2.7 <1.3.0" ), "1.3.0 is the exclusive upper bound" );
			Assert.IsFalse( Satisfies( "1.1.0", ">=1.2.7 <1.3.0" ), "1.1.0 is below the range" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void NpmRequirementAlternativesNeedOnlyOneToHold()
		{
			// The example from npm's own documentation for "1.2.7 || >=1.2.9 <2.0.0"
			Assert.IsTrue( Satisfies( "1.2.7", "1.2.7 || >=1.2.9 <2.0.0" ), "1.2.7 satisfies the first alternative" );
			Assert.IsTrue( Satisfies( "1.2.9", "1.2.7 || >=1.2.9 <2.0.0" ), "1.2.9 satisfies the second alternative" );
			Assert.IsTrue( Satisfies( "1.4.6", "1.2.7 || >=1.2.9 <2.0.0" ), "1.4.6 satisfies the second alternative" );
			Assert.IsFalse( Satisfies( "1.2.8", "1.2.7 || >=1.2.9 <2.0.0" ), "1.2.8 falls between the alternatives" );
			Assert.IsFalse( Satisfies( "2.0.0", "1.2.7 || >=1.2.9 <2.0.0" ), "2.0.0 is above the second alternative" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void NpmRequirementAlternativesOfCaretRanges()
		{
			Assert.IsTrue( Satisfies( "1.9.9", "^1.0.0 || ^3.0.0" ), "1.9.9 satisfies the first alternative" );
			Assert.IsTrue( Satisfies( "3.9.9", "^1.0.0 || ^3.0.0" ), "3.9.9 satisfies the second alternative" );
			Assert.IsFalse( Satisfies( "2.0.0", "^1.0.0 || ^3.0.0" ), "2.0.0 falls between the alternatives" );
			Assert.IsFalse( Satisfies( "4.0.0", "^1.0.0 || ^3.0.0" ), "4.0.0 is above both alternatives" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void NpmRequirementContradictoryComparatorsMatchNothing()
		{
			Assert.IsFalse( Satisfies( "0.5.0", ">=2.0.0 <1.0.0" ), "0.5.0" );
			Assert.IsFalse( Satisfies( "1.5.0", ">=2.0.0 <1.0.0" ), "1.5.0" );
			Assert.IsFalse( Satisfies( "3.0.0", ">=2.0.0 <1.0.0" ), "3.0.0" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void NpmRequirementComparesPrereleasesAgainstTheBounds()
		{
			// npm only lets a prerelease satisfy a range when a comparator names the same major.minor.patch, which this reduction does not reproduce.
			// It does not matter in practice because prereleases are filtered out before a range is ever applied.
			Assert.IsFalse( Satisfies( "1.0.0-rc1", ">=1.0.0" ), "A prerelease ranks below the release it precedes" );
			Assert.IsTrue( Satisfies( "1.2.3-beta.2", ">=1.2.3-beta.1 <2.0.0" ), "1.2.3-beta.2 is above the 1.2.3-beta.1 lower bound" );
			Assert.IsTrue( Satisfies( "1.5.0-alpha", "^1.0.0" ), "A prerelease inside the numeric range satisfies this reduction" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		[DataRow( "not-a-range" )]
		[DataRow( "^not-a-version" )]
		[DataRow( ">=abc" )]
		[DataRow( "git+https://github.com/user/repo.git" )]
		[DataRow( "npm:other-package@^1.0.0" )]
		public void NpmRequirementRejectsUnparsableRanges( string range )
		{
			Assert.ThrowsExactly<FormatException>( () => new NpmRequirement( range ), $"'{range}' should not parse" );
		}

		// ---- NpmVersionMatcher: selection from a version list ----

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void SelectMatchingVersionComparesNumericallyAndSkipsPrereleases()
		{
			// 3.10.1 is lexically lower than 3.9.3, and 4.0.0-rc1 must not be chosen even though it is the highest
			List<string> versions = MakeVersions( "3.0.0", "3.9.3", "3.10.1", "4.0.0-rc1" );

			NpmVersion? match = NpmVersionMatcher.SelectMatchingVersion( "lodash", versions, "^3.0.0" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "3.10.1", match.ToString(), "Highest stable version in range" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void SelectMatchingVersionIgnoresOrderOfTheList()
		{
			List<string> versions = MakeVersions( "1.9.0", "1.10.0", "1.2.0" );

			NpmVersion? forward = NpmVersionMatcher.SelectMatchingVersion( "package", versions, "" );
			versions.Reverse();
			NpmVersion? backward = NpmVersionMatcher.SelectMatchingVersion( "package", versions, "" );

			Assert.IsNotNull( forward, "No version found for the original order" );
			Assert.IsNotNull( backward, "No version found for the reversed order" );
			Assert.AreEqual( "1.10.0", forward.ToString(), "Original order" );
			Assert.AreEqual( "1.10.0", backward.ToString(), "Reversed order" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void SelectMatchingVersionWithNoRangeReturnsHighestStable()
		{
			List<string> versions = MakeVersions( "9.9.0", "10.0.0", "11.0.0-beta1" );

			NpmVersion? match = NpmVersionMatcher.SelectMatchingVersion( "package", versions, "" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "10.0.0", match.ToString(), "The prerelease must not be chosen even though it is higher" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void SelectMatchingVersionHonoursACaretUpperBound()
		{
			// The case that matters for dependencies: the latest version is outside the range the package asked for
			List<string> versions = MakeVersions( "4.17.0", "4.17.21", "5.0.0", "5.1.0" );

			NpmVersion? match = NpmVersionMatcher.SelectMatchingVersion( "package", versions, "^4.17.0" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "4.17.21", match.ToString(), "The highest 4.x version, not the latest published version" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void SelectMatchingVersionExactRangeReturnsThatVersion()
		{
			List<string> versions = MakeVersions( "1.2.2", "1.2.3", "1.2.4" );

			NpmVersion? match = NpmVersionMatcher.SelectMatchingVersion( "package", versions, "1.2.3" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "1.2.3", match.ToString(), "An exact range selects exactly that version" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void SelectMatchingVersionToleratesDuplicateVersionNumbers()
		{
			List<string> versions = MakeVersions( "3.1.21", "3.1.21", "3.1.20", "3.1.22" );

			NpmVersion? match = NpmVersionMatcher.SelectMatchingVersion( "package", versions, "<=3.1.21" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "3.1.21", match.ToString(), "Highest version at or below 3.1.21" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void SelectMatchingVersionSkipsUnparsableVersionsRatherThanFailing()
		{
			// One bad entry must not hide every good one
			List<string> versions = MakeVersions( "1.0.0", "not-a-version", "2.0.0" );

			NpmVersion? match = NpmVersionMatcher.SelectMatchingVersion( "package", versions, ">=1.0.0" );

			Assert.IsNotNull( match, "The unparsable entry should be skipped, not abandon the selection" );
			Assert.AreEqual( "2.0.0", match.ToString(), "Highest parsable version" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void SelectMatchingVersionReturnsNullWhenNothingQualifies()
		{
			Assert.IsNull( NpmVersionMatcher.SelectMatchingVersion( "package", MakeVersions( "1.0.0", "2.0.0" ), ">=3.0.0" ), "No version satisfies the range" );
			Assert.IsNull( NpmVersionMatcher.SelectMatchingVersion( "package", MakeVersions( "1.0.0-rc1", "1.0.0-beta2" ), "" ), "Only prereleases exist" );
			Assert.IsNull( NpmVersionMatcher.SelectMatchingVersion( "package", MakeVersions(), "" ), "There are no versions at all" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		[DataRow( "^not-a-version" )]
		[DataRow( "git+https://github.com/user/repo.git" )]
		[DataRow( "npm:other-package@^1.0.0" )]
		public void SelectMatchingVersionInvalidRangeReturnsNull( string range )
		{
			// npm allows a dependency to name a git URL or an alias rather than a version range; neither can be resolved here
			NpmVersion? match = NpmVersionMatcher.SelectMatchingVersion( "package", MakeVersions( "1.0.0", "2.0.0" ), range );

			Assert.IsNull( match, $"'{range}' should log and return null rather than throw" );
		}

		// The cases below read live data from npmjs.org. left-pad has not been published since 2018, and the lodash and express release lines used here
		// were closed years ago, so the expected versions are stable.

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void FindMatchingVersionCaretRange()
		{
			NpmVersion? match = NpmVersionMatcher.FindMatchingVersion( "lodash", "^3.0.0" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "3.10.1", match.ToString(), "Highest lodash 3.x release" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void FindMatchingVersionComparesNumericallyRatherThanAsText()
		{
			// 3.10.1 sorts before 3.9.3 as text, so a string comparison would pick the wrong one
			NpmVersion? match = NpmVersionMatcher.FindMatchingVersion( "lodash", ">=3.0.0 <4.0.0" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "3.10.1", match.ToString(), "3.10.1 is higher than 3.9.3" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void FindMatchingVersionTildeRange()
		{
			NpmVersion? match = NpmVersionMatcher.FindMatchingVersion( "lodash", "~3.9.0" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "3.9.3", match.ToString(), "Highest lodash 3.9.x release" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void FindMatchingVersionWildcardRange()
		{
			NpmVersion? match = NpmVersionMatcher.FindMatchingVersion( "lodash", "1.x" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "1.3.1", match.ToString(), "Highest lodash 1.x release" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void FindMatchingVersionHyphenRange()
		{
			NpmVersion? match = NpmVersionMatcher.FindMatchingVersion( "lodash", "2.4.1 - 3.9.0" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "3.9.0", match.ToString(), "The inclusive upper bound of the hyphen range" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void FindMatchingVersionAlternatives()
		{
			NpmVersion? match = NpmVersionMatcher.FindMatchingVersion( "lodash", "^1.0.0 || ^3.0.0" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "3.10.1", match.ToString(), "The highest version satisfying either alternative" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void FindMatchingVersionZeroMajorCaretLocksTheMinor()
		{
			NpmVersion? match = NpmVersionMatcher.FindMatchingVersion( "lodash", "^0.9.0" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "0.9.2", match.ToString(), "Highest lodash 0.9.x release, as ^0.9.0 stops below 0.10.0" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void FindMatchingVersionNoRangeReturnsLatestStable()
		{
			NpmVersion? match = NpmVersionMatcher.FindMatchingVersion( "left-pad", "" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "1.3.0", match.ToString(), "left-pad has not been published since 2018" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void FindMatchingVersionNoSatisfyingVersionReturnsNull()
		{
			NpmVersion? match = NpmVersionMatcher.FindMatchingVersion( "left-pad", ">=99.0.0" );

			Assert.IsNull( match, "No left-pad version is >= 99.0.0" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void FindMatchingVersionUnknownPackageReturnsNull()
		{
			NpmVersion? match = NpmVersionMatcher.FindMatchingVersion( "eternal-no-such-package-9f3a7c", "*" );

			Assert.IsNull( match, "A package that does not exist has no versions" );
		}

		// ---- NpmPackage.GetNpmPackage: blank, a specific version, or a range (live, as above) ----

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		public void GetNpmPackageBlankRequirementsUseTheLatestVersion()
		{
			NpmPackage? latest_package = NpmPackage.GetNpmPackage( "left-pad", "", false );
			NpmPackage? whitespace_package = NpmPackage.GetNpmPackage( "left-pad", "   ", false );

			Assert.IsNotNull( latest_package, "No package found for blank requirements" );
			Assert.IsNotNull( whitespace_package, "No package found for whitespace requirements" );
			Assert.AreEqual( "1.3.0", latest_package.Version, "left-pad has not been published since 2018" );
			Assert.AreEqual( latest_package.Version, whitespace_package.Version, "Whitespace should be the same as blank" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		[DataRow( "left-pad", "1.1.3", "1.1.3" )]
		[DataRow( "lodash", "3.9.3", "3.9.3" )]
		// A prerelease can be requested by its exact number, even though it can never satisfy a range
		[DataRow( "express", "5.0.0-beta.1", "5.0.0-beta.1" )]
		public void GetNpmPackageSpecificVersion( string packageName, string requirements, string expectedVersion )
		{
			NpmPackage? npm_package = NpmPackage.GetNpmPackage( packageName, requirements, false );

			Assert.IsNotNull( npm_package, $"No package found for '{packageName}' '{requirements}'" );
			Assert.AreEqual( expectedVersion, npm_package.Version, $"Version for '{packageName}' '{requirements}'" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		[DataRow( "left-pad", "^1.0.0", "1.3.0" )]
		[DataRow( "left-pad", "~1.1.0", "1.1.3" )]
		[DataRow( "left-pad", "0.0.x", "0.0.9" )]
		[DataRow( "left-pad", "^0.0.3", "0.0.3" )]
		[DataRow( "left-pad", ">=1.0.0 <1.2.0", "1.1.3" )]
		[DataRow( "left-pad", "1.0.0 - 1.1.2", "1.1.2" )]
		[DataRow( "left-pad", "1.0.1 || ^1.2.0", "1.3.0" )]
		[DataRow( "lodash", "^3.0.0", "3.10.1" )]
		[DataRow( "lodash", "~3.9.0", "3.9.3" )]
		[DataRow( "lodash", "^2.0.0", "2.4.2" )]
		[DataRow( "lodash", "1.x", "1.3.1" )]
		[DataRow( "express", "^3.0.0", "3.21.2" )]
		[DataRow( "express", "^2.0.0", "2.5.11" )]
		public void GetNpmPackageRangeSelectsTheHighestSatisfyingVersion( string packageName, string requirements, string expectedVersion )
		{
			NpmPackage? npm_package = NpmPackage.GetNpmPackage( packageName, requirements, false );

			Assert.IsNotNull( npm_package, $"No package found for '{packageName}' '{requirements}'" );
			Assert.AreEqual( expectedVersion, npm_package.Version, $"Version for '{packageName}' '{requirements}'" );
		}

		[TestMethod]
		[TestCategory( "NpmVersion" )]
		[DataRow( "left-pad", ">=99.0.0" )]
		[DataRow( "left-pad", "99.0.0" )]
		[DataRow( "left-pad", "^not-a-version" )]
		[DataRow( "left-pad", "git+https://github.com/stevemao/left-pad.git" )]
		[DataRow( "eternal-no-such-package-9f3a7c", "" )]
		public void GetNpmPackageWithNoSatisfyingVersionReturnsNull( string packageName, string requirements )
		{
			NpmPackage? npm_package = NpmPackage.GetNpmPackage( packageName, requirements, false );

			Assert.IsNull( npm_package, $"No package should be found for '{packageName}' '{requirements}'" );
		}
	}
}
