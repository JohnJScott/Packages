// Copyright Eternal Developments LLC. All Rights Reserved.

using Eternal.PackageCheck;

namespace Eternal.PackageCheckTest
{
	[TestClass]
	public sealed class PythonVersionTests
	{
		private static int Compare( string left, string right )
		{
			return Math.Sign( new PythonVersion( left ).CompareTo( new PythonVersion( right ) ) );
		}

		private static bool Satisfies( string version, string specifier )
		{
			PythonRequirement requirement = new PythonRequirement( specifier );
			return requirement.SatisfiedBy( new PythonVersion( version ) );
		}

		private static List<string> MakeVersions( params string[] numbers )
		{
			return numbers.ToList();
		}

		// ---- PythonVersion: parsing ----

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonVersionParsesDottedParts()
		{
			PythonVersion version = new PythonVersion( "2.32.3" );

			CollectionAssert.AreEqual( new int[] { 2, 32, 3 }, version.Parts, "Parts mismatch" );
			Assert.AreEqual( 0, version.Epoch, "No epoch was written, so it is 0" );
			Assert.AreEqual( "2.32.3", version.ToString(), "ToString should return the original text" );
			Assert.IsFalse( version.IsPreRelease, "2.32.3 is a final release" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonVersionAllowsAnyNumberOfReleaseParts()
		{
			CollectionAssert.AreEqual( new int[] { 1 }, new PythonVersion( "1" ).Parts, "A single part is a valid release segment" );
			CollectionAssert.AreEqual( new int[] { 2020, 6, 20 }, new PythonVersion( "2020.6.20" ).Parts, "A date based version" );
			CollectionAssert.AreEqual( new int[] { 1, 2, 3, 4, 5 }, new PythonVersion( "1.2.3.4.5" ).Parts, "PEP 440 puts no limit on the release segment" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonVersionParsesAnEpoch()
		{
			PythonVersion version = new PythonVersion( "1!2.0" );

			Assert.AreEqual( 1, version.Epoch, "The epoch written before the '!'" );
			CollectionAssert.AreEqual( new int[] { 2, 0 }, version.Parts, "The release segment follows the epoch" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		[DataRow( "1.0a1" )]
		[DataRow( "1.0alpha1" )]
		[DataRow( "1.0b1" )]
		[DataRow( "1.0beta1" )]
		[DataRow( "1.0rc1" )]
		[DataRow( "1.0c1" )]
		[DataRow( "1.0pre1" )]
		[DataRow( "1.0preview1" )]
		[DataRow( "1.0.dev1" )]
		[DataRow( "1.0dev1" )]
		[DataRow( "1.0-dev1" )]
		[DataRow( "1.0a1.dev1" )]
		public void PythonVersionRecognisesAPreRelease( string version )
		{
			Assert.IsTrue( new PythonVersion( version ).IsPreRelease, $"'{version}' is a pre-release or development release" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		[DataRow( "1.0" )]
		[DataRow( "1.0.post1" )]
		[DataRow( "1.0-1" )]
		[DataRow( "1.0.rev1" )]
		[DataRow( "1.0r1" )]
		[DataRow( "1!1.0" )]
		public void PythonVersionRecognisesAFinalRelease( string version )
		{
			// A post-release comes after the release it follows, so it is not a pre-release
			Assert.IsFalse( new PythonVersion( version ).IsPreRelease, $"'{version}' is a final release" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonVersionAcceptsTheSeveralSpellingsOfEachSegment()
		{
			// PEP 440 normalizes "alpha" to "a", "beta" to "b", and "c", "pre" and "preview" to "rc"
			Assert.AreEqual( 0, Compare( "1.0alpha1", "1.0a1" ), "'alpha' is 'a'" );
			Assert.AreEqual( 0, Compare( "1.0beta1", "1.0b1" ), "'beta' is 'b'" );
			Assert.AreEqual( 0, Compare( "1.0c1", "1.0rc1" ), "'c' is 'rc'" );
			Assert.AreEqual( 0, Compare( "1.0pre1", "1.0rc1" ), "'pre' is 'rc'" );
			Assert.AreEqual( 0, Compare( "1.0preview1", "1.0rc1" ), "'preview' is 'rc'" );

			// "post", "rev", "r" and a bare "-N" all mean a post-release
			Assert.AreEqual( 0, Compare( "1.0.rev1", "1.0.post1" ), "'rev' is 'post'" );
			Assert.AreEqual( 0, Compare( "1.0r1", "1.0.post1" ), "'r' is 'post'" );
			Assert.AreEqual( 0, Compare( "1.0-1", "1.0.post1" ), "A bare '-1' is 'post1'" );

			// The separator before a segment may be '.', '-', '_' or nothing at all
			Assert.AreEqual( 0, Compare( "1.0.a1", "1.0a1" ), "A '.' separator" );
			Assert.AreEqual( 0, Compare( "1.0-a1", "1.0a1" ), "A '-' separator" );
			Assert.AreEqual( 0, Compare( "1.0_a1", "1.0a1" ), "A '_' separator" );

			// A missing number means the zeroth of its kind
			Assert.AreEqual( 0, Compare( "1.0a", "1.0a0" ), "'a' is 'a0'" );
			Assert.AreEqual( 0, Compare( "1.0.post", "1.0.post0" ), "'post' is 'post0'" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonVersionIsCaseInsensitiveAndAcceptsALeadingV()
		{
			Assert.AreEqual( 0, Compare( "1.0RC1", "1.0rc1" ), "The segment labels are not case sensitive" );
			Assert.AreEqual( 0, Compare( "1.0.POST1", "1.0.post1" ), "The segment labels are not case sensitive" );
			Assert.AreEqual( 0, Compare( "v1.0", "1.0" ), "A leading 'v' is not part of the number" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		[DataRow( "abc" )]
		[DataRow( "" )]
		[DataRow( "1.0.x" )]
		[DataRow( "not-a-version" )]
		[DataRow( ">=1.0" )]
		public void PythonVersionRejectsVersionsOutsidePep440( string version )
		{
			Assert.ThrowsExactly<FormatException>( () => new PythonVersion( version ), $"'{version}' should not parse" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonVersionAcceptsSemVerLookingPreReleases()
		{
			// PEP 440 allows a '-' or '.' on either side of the label, so a version that looks like SemVer is read as the PEP 440 version it normalizes to
			Assert.AreEqual( 0, Compare( "1.0.0-beta.2", "1.0.0b2" ), "'1.0.0-beta.2' normalizes to '1.0.0b2'" );
			Assert.AreEqual( 0, Compare( "1.0.0-alpha.1", "1.0.0a1" ), "'1.0.0-alpha.1' normalizes to '1.0.0a1'" );
			Assert.AreEqual( 0, Compare( "1.0.0-rc.1", "1.0.0rc1" ), "'1.0.0-rc.1' normalizes to '1.0.0rc1'" );
			Assert.IsTrue( new PythonVersion( "1.0.0-beta.2" ).IsPreRelease, "It is still a pre-release" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonVersionTryParseReportsFailureInsteadOfThrowing()
		{
			Assert.IsFalse( PythonVersion.TryParse( "not-a-version", out PythonVersion? rejected ), "'not-a-version' should not parse" );
			Assert.IsNull( rejected, "A rejected version should come back null" );

			Assert.IsTrue( PythonVersion.TryParse( "1.2.3", out PythonVersion? accepted ), "'1.2.3' should parse" );
			Assert.IsNotNull( accepted, "An accepted version should not be null" );
			Assert.AreEqual( "1.2.3", accepted.ToString(), "The parsed version" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonVersionPartAtPadsMissingPartsWithZero()
		{
			PythonVersion version = new PythonVersion( "1.2" );

			Assert.AreEqual( 1, version.PartAt( 0 ), "Part 0" );
			Assert.AreEqual( 2, version.PartAt( 1 ), "Part 1" );
			Assert.AreEqual( 0, version.PartAt( 2 ), "Part 2 is past the end and should be 0" );
			Assert.AreEqual( 0, version.PartAt( 10 ), "Part 10 is past the end and should be 0" );
		}

		// ---- PythonVersion: ordering ----

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		[DataRow( "1.0", "1.0", 0 )]
		[DataRow( "1.0.1", "1.0", 1 )]
		[DataRow( "0.9", "1.0", -1 )]
		// Parts compare numerically, not as text
		[DataRow( "1.10", "1.9", 1 )]
		[DataRow( "1.10.0", "1.9.0", 1 )]
		[DataRow( "2.25.1", "2.9.2", 1 )]
		// Missing trailing parts are zero
		[DataRow( "1.0", "1.0.0", 0 )]
		[DataRow( "1", "1.0.0.0", 0 )]
		[DataRow( "1.0", "1.0.1", -1 )]
		// An epoch outranks the release segment entirely
		[DataRow( "1!1.0", "2.0", 1 )]
		[DataRow( "1!1.0", "999.0", 1 )]
		[DataRow( "1!1.0", "2!1.0", -1 )]
		[DataRow( "0!1.0", "1.0", 0 )]
		public void PythonVersionCompareTo( string left, string right, int expected )
		{
			Assert.AreEqual( expected, Compare( left, right ), $"'{left}' compared with '{right}'" );
			Assert.AreEqual( -expected, Compare( right, left ), $"'{right}' compared with '{left}' (should be the mirror image)" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonVersionCompareToNullIsGreater()
		{
			Assert.AreEqual( 1, new PythonVersion( "1.0" ).CompareTo( null ), "Any version is greater than null" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonVersionMaxPicksNumericallyHighest()
		{
			List<PythonVersion> versions = new List<PythonVersion>
			{
				new PythonVersion( "1.9.0" ),
				new PythonVersion( "1.10.0" ),
				new PythonVersion( "1.2.0" ),
				new PythonVersion( "1.0" )
			};

			Assert.AreEqual( "1.10.0", versions.Max()!.ToString(), "1.10.0 is higher than 1.9.0" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		[DataRow( "1.0.dev1", "1.0a1", -1 )]
		[DataRow( "1.0a1", "1.0b1", -1 )]
		[DataRow( "1.0b1", "1.0rc1", -1 )]
		[DataRow( "1.0rc1", "1.0", -1 )]
		[DataRow( "1.0", "1.0.post1", -1 )]
		[DataRow( "1.0a1.dev1", "1.0a1", -1 )]
		[DataRow( "1.0.post1.dev1", "1.0.post1", -1 )]
		[DataRow( "1.0a1", "1.0a2", -1 )]
		[DataRow( "1.0a2", "1.0a12", -1 )]
		[DataRow( "1.0.post1", "1.0.post2", -1 )]
		[DataRow( "1.0.dev1", "1.0.dev2", -1 )]
		[DataRow( "1.0.dev1", "1.0", -1 )]
		public void PythonVersionSegmentOrdering( string left, string right, int expected )
		{
			Assert.AreEqual( expected, Compare( left, right ), $"'{left}' compared with '{right}'" );
			Assert.AreEqual( -expected, Compare( right, left ), $"'{right}' compared with '{left}' (should be the mirror image)" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonVersionOrderingFollowsThePep440Example()
		{
			// The ordering given in the PEP 440 specification
			string[] ascending =
			[
				"1.dev0",
				"1.0.dev456",
				"1.0a1",
				"1.0a2.dev456",
				"1.0a12.dev456",
				"1.0a12",
				"1.0b1.dev456",
				"1.0b2",
				"1.0b2.post345.dev456",
				"1.0b2.post345",
				"1.0rc1.dev456",
				"1.0rc1",
				"1.0",
				"1.0.post456.dev34",
				"1.0.post456",
				"1.1.dev1"
			];

			for( int i = 1; i < ascending.Length; i++ )
			{
				Assert.AreEqual( -1, Compare( ascending[i - 1], ascending[i] ), $"'{ascending[i - 1]}' should rank below '{ascending[i]}'" );
			}
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonVersionIgnoresTheLocalVersionWhenOrdering()
		{
			// PEP 440 sorts a local version above the same version without one, but PyPI refuses to publish a local version, so it never arises here
			Assert.AreEqual( 0, Compare( "1.0+abc.5", "1.0" ), "The local version takes no part in ordering" );
			Assert.AreEqual( 0, Compare( "1.0+abc.5", "1.0+def.6" ), "Two local versions of the same release rank equally" );
			Assert.AreEqual( "1.0+abc.5", new PythonVersion( "1.0+abc.5" ).ToString(), "ToString should keep the local version" );
		}

		// ---- PythonRequirement: operators ----

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		[DataRow( "==1.2.3", "1.2.3", true )]
		[DataRow( "==1.2.3", "1.2.4", false )]
		[DataRow( "==1.0", "1.0.0", true )]
		[DataRow( "1.2.3", "1.2.3", true )]
		[DataRow( "1.2.3", "1.2.4", false )]
		[DataRow( "!=1.2.3", "1.2.3", false )]
		[DataRow( "!=1.2.3", "1.2.4", true )]
		[DataRow( ">=1.2.3", "1.2.3", true )]
		[DataRow( ">=1.2.3", "1.2.2", false )]
		[DataRow( ">=1.2.3", "1.10.0", true )]
		[DataRow( ">1.2.3", "1.2.3", false )]
		[DataRow( ">1.2.3", "1.2.4", true )]
		[DataRow( "<=1.2.3", "1.2.3", true )]
		[DataRow( "<=1.2.3", "1.2.4", false )]
		[DataRow( "<1.2.3", "1.2.2", true )]
		[DataRow( "<1.2.3", "1.2.3", false )]
		[DataRow( "<2.0", "1.9.9", true )]
		[DataRow( "<2.0", "2.0", false )]
		// Whitespace around the operator and between clauses is allowed
		[DataRow( ">= 1.2.3", "1.2.3", true )]
		[DataRow( "  >=1.2.3  ", "1.2.2", false )]
		public void PythonRequirementOperators( string specifier, string version, bool expected )
		{
			Assert.AreEqual( expected, Satisfies( version, specifier ), $"'{version}' against '{specifier}'" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		[DataRow( "==1.2.*", "1.2.0", true )]
		[DataRow( "==1.2.*", "1.2.99", true )]
		[DataRow( "==1.2.*", "1.3.0", false )]
		[DataRow( "==1.2.*", "1.1.9", false )]
		[DataRow( "==1.*", "1.9.9", true )]
		[DataRow( "==1.*", "2.0", false )]
		[DataRow( "==2.*", "2.25.1", true )]
		[DataRow( "!=1.2.*", "1.2.5", false )]
		[DataRow( "!=1.2.*", "1.3.0", true )]
		[DataRow( "!=1.*", "2.0", true )]
		// A post-release is still inside the release line it follows
		[DataRow( "==1.2.*", "1.2.post1", true )]
		public void PythonRequirementPrefixMatching( string specifier, string version, bool expected )
		{
			Assert.AreEqual( expected, Satisfies( version, specifier ), $"'{version}' against '{specifier}'" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		// "~= 1.2.3" is ">= 1.2.3" together with "== 1.2.*"
		[DataRow( "~=1.2.3", "1.2.3", true )]
		[DataRow( "~=1.2.3", "1.2.99", true )]
		[DataRow( "~=1.2.3", "1.3.0", false )]
		[DataRow( "~=1.2.3", "1.2.2", false )]
		// "~= 1.2" is ">= 1.2" together with "== 1.*"
		[DataRow( "~=1.2", "1.2", true )]
		[DataRow( "~=1.2", "1.99", true )]
		[DataRow( "~=1.2", "2.0", false )]
		[DataRow( "~=1.2", "1.1", false )]
		// A longer release segment moves which part may grow
		[DataRow( "~=1.2.3.4", "1.2.3.99", true )]
		[DataRow( "~=1.2.3.4", "1.2.4.0", false )]
		[DataRow( "~=2.25.0", "2.25.1", true )]
		[DataRow( "~=2.25.0", "2.26.0", false )]
		public void PythonRequirementCompatibleRelease( string specifier, string version, bool expected )
		{
			Assert.AreEqual( expected, Satisfies( version, specifier ), $"'{version}' against '{specifier}'" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonRequirementCompatibleReleaseNeedsTwoParts()
		{
			Assert.ThrowsExactly<FormatException>( () => new PythonRequirement( "~=1" ), "'~= 1' names no part that may grow" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonRequirementArbitraryEqualityComparesText()
		{
			Assert.IsTrue( Satisfies( "1.0", "===1.0" ), "The text matches" );
			Assert.IsFalse( Satisfies( "1.0.0", "===1.0" ), "'1.0.0' is the same version but not the same text" );
			Assert.IsFalse( Satisfies( "1.0", "===1.0.0" ), "'1.0' is the same version but not the same text" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonRequirementArbitraryEqualityAcceptsTextOutsidePep440()
		{
			// "===" is the one operator whose right hand side need not be a valid version
			PythonRequirement requirement = new PythonRequirement( "===not-a-version" );

			Assert.IsFalse( requirement.SatisfiedBy( new PythonVersion( "1.0" ) ), "No published version has that text" );
		}

		// ---- PythonRequirement: combinations ----

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonRequirementMultipleClausesAreAllRequired()
		{
			Assert.IsTrue( Satisfies( "1.25.0", ">=1.21.1,<1.27" ), "1.25.0 is inside the range" );
			Assert.IsTrue( Satisfies( "1.21.1", ">=1.21.1,<1.27" ), "1.21.1 is the inclusive lower bound" );
			Assert.IsFalse( Satisfies( "1.27", ">=1.21.1,<1.27" ), "1.27 is the exclusive upper bound" );
			Assert.IsFalse( Satisfies( "1.21.0", ">=1.21.1,<1.27" ), "1.21.0 is below the range" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonRequirementExclusionWithinARange()
		{
			Assert.IsFalse( Satisfies( "1.5.7", ">=1.5.6,!=1.5.7" ), "1.5.7 is excluded" );
			Assert.IsTrue( Satisfies( "1.5.8", ">=1.5.6,!=1.5.7" ), "1.5.8 is allowed" );
			Assert.IsTrue( Satisfies( "1.5.6", ">=1.5.6,!=1.5.7" ), "1.5.6 is the lower bound" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonRequirementContradictoryClausesMatchNothing()
		{
			Assert.IsFalse( Satisfies( "0.5", ">=2,<1" ), "0.5" );
			Assert.IsFalse( Satisfies( "1.5", ">=2,<1" ), "1.5" );
			Assert.IsFalse( Satisfies( "3.0", ">=2,<1" ), "3.0" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonRequirementWithNoClausesMatchesEverything()
		{
			Assert.IsTrue( Satisfies( "0.1", "" ), "0.1" );
			Assert.IsTrue( Satisfies( "99.99", "" ), "99.99" );
			Assert.IsTrue( Satisfies( "1.0", "   " ), "Whitespace is the same as blank" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void PythonRequirementComparesPreReleasesAgainstTheBounds()
		{
			// PEP 440 excludes pre-releases from an ordered comparison unless the specifier names one, which this reduction does not reproduce.
			// It does not matter in practice because pre-releases are filtered out before a specifier is ever applied.
			Assert.IsFalse( Satisfies( "2.0a1", ">=2.0" ), "A pre-release ranks below the release it precedes" );
			Assert.IsTrue( Satisfies( "2.0a1", "<2.0" ), "2.0a1 is numerically below 2.0" );
			Assert.IsTrue( Satisfies( "1.21.0rc1", ">=1.0,<2.0a0" ), "The upper bound names a pre-release, as a bound meant to exclude 2.x does" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		[DataRow( ">=not-a-version" )]
		[DataRow( "~=abc" )]
		[DataRow( ">=" )]
		[DataRow( ">=1.0,<not-a-version" )]
		[DataRow( "@ https://example.com/package.whl" )]
		public void PythonRequirementRejectsUnparsableClauses( string specifier )
		{
			Assert.ThrowsExactly<FormatException>( () => new PythonRequirement( specifier ), $"'{specifier}' should not parse" );
		}

		// ---- PythonVersionMatcher: selection from a version list ----

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void SelectMatchingVersionComparesNumericallyAndSkipsPreReleases()
		{
			// 1.10.0 is lexically lower than 1.9.0, and 2.0rc1 must not be chosen even though it is the highest
			List<string> versions = MakeVersions( "1.0", "1.9.0", "1.10.0", "2.0rc1" );

			PythonVersion? match = PythonVersionMatcher.SelectMatchingVersion( "six", versions, "<2.0" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "1.10.0", match.ToString(), "Highest final version in range" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void SelectMatchingVersionIgnoresOrderOfTheList()
		{
			List<string> versions = MakeVersions( "1.9.0", "1.10.0", "1.2.0" );

			PythonVersion? forward = PythonVersionMatcher.SelectMatchingVersion( "package", versions, "" );
			versions.Reverse();
			PythonVersion? backward = PythonVersionMatcher.SelectMatchingVersion( "package", versions, "" );

			Assert.IsNotNull( forward, "No version found for the original order" );
			Assert.IsNotNull( backward, "No version found for the reversed order" );
			Assert.AreEqual( "1.10.0", forward.ToString(), "Original order" );
			Assert.AreEqual( "1.10.0", backward.ToString(), "Reversed order" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void SelectMatchingVersionWithNoSpecifierReturnsHighestFinal()
		{
			List<string> versions = MakeVersions( "9.9", "10.0", "11.0b1", "11.0.dev1" );

			PythonVersion? match = PythonVersionMatcher.SelectMatchingVersion( "package", versions, "" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "10.0", match.ToString(), "Neither the beta nor the development release may be chosen" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void SelectMatchingVersionKeepsPostReleases()
		{
			// A post-release is a final release, so it is a candidate and outranks the release it follows
			List<string> versions = MakeVersions( "1.0", "1.0.post1", "1.0rc2" );

			PythonVersion? match = PythonVersionMatcher.SelectMatchingVersion( "package", versions, "==1.0.*" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "1.0.post1", match.ToString(), "The post-release is the highest final version" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void SelectMatchingVersionHonoursAnUpperBound()
		{
			// The case that matters for dependencies: the latest version is outside the range the package asked for
			List<string> versions = MakeVersions( "1.21.1", "1.26.20", "2.0.0", "2.8.0" );

			PythonVersion? match = PythonVersionMatcher.SelectMatchingVersion( "urllib3", versions, ">=1.21.1,<1.27" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "1.26.20", match.ToString(), "The highest 1.26.x version, not the latest published version" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void SelectMatchingVersionCompatibleRelease()
		{
			List<string> versions = MakeVersions( "2.24.0", "2.25.0", "2.25.1", "2.26.0" );

			PythonVersion? match = PythonVersionMatcher.SelectMatchingVersion( "requests", versions, "~=2.25.0" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "2.25.1", match.ToString(), "The highest 2.25.x version" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void SelectMatchingVersionToleratesDuplicateVersionNumbers()
		{
			List<string> versions = MakeVersions( "3.1.21", "3.1.21", "3.1.20", "3.1.22" );

			PythonVersion? match = PythonVersionMatcher.SelectMatchingVersion( "package", versions, "<=3.1.21" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "3.1.21", match.ToString(), "Highest version at or below 3.1.21" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void SelectMatchingVersionSkipsUnparsableVersionsRatherThanFailing()
		{
			// One bad entry must not hide every good one
			List<string> versions = MakeVersions( "1.0", "not-a-version", "2.0" );

			PythonVersion? match = PythonVersionMatcher.SelectMatchingVersion( "package", versions, ">=1.0" );

			Assert.IsNotNull( match, "The unparsable entry should be skipped, not abandon the selection" );
			Assert.AreEqual( "2.0", match.ToString(), "Highest parsable version" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void SelectMatchingVersionReturnsNullWhenNothingQualifies()
		{
			Assert.IsNull( PythonVersionMatcher.SelectMatchingVersion( "package", MakeVersions( "1.0", "2.0" ), ">=3.0" ), "No version satisfies the specifier" );
			Assert.IsNull( PythonVersionMatcher.SelectMatchingVersion( "package", MakeVersions( "1.0rc1", "1.0.dev2" ), "" ), "Only pre-releases exist" );
			Assert.IsNull( PythonVersionMatcher.SelectMatchingVersion( "package", MakeVersions(), "" ), "There are no versions at all" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		[DataRow( ">=not-a-version" )]
		[DataRow( "~=abc" )]
		[DataRow( "@ https://example.com/package.whl" )]
		public void SelectMatchingVersionInvalidSpecifierReturnsNull( string specifier )
		{
			PythonVersion? match = PythonVersionMatcher.SelectMatchingVersion( "package", MakeVersions( "1.0", "2.0" ), specifier );

			Assert.IsNull( match, $"'{specifier}' should log and return null rather than throw" );
		}

		// The cases below read live data from PyPI.org. The expected versions are all in release lines that have been closed for years, so they are stable.

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void FindMatchingVersionUpperBound()
		{
			PythonVersion? match = PythonVersionMatcher.FindMatchingVersion( "requests", ">=2.20,<2.26" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "2.25.1", match.ToString(), "Highest requests release below 2.26" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void FindMatchingVersionCompatibleRelease()
		{
			PythonVersion? match = PythonVersionMatcher.FindMatchingVersion( "requests", "~=2.25.0" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "2.25.1", match.ToString(), "Highest requests 2.25.x release" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void FindMatchingVersionPrefixMatching()
		{
			PythonVersion? match = PythonVersionMatcher.FindMatchingVersion( "requests", "==1.*" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "1.2.3", match.ToString(), "Highest requests 1.x release" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void FindMatchingVersionComparesNumericallyRatherThanAsText()
		{
			// six published 1.9.0 before 1.10.0, and 1.10.0 sorts before 1.9.0 as text
			PythonVersion? match = PythonVersionMatcher.FindMatchingVersion( "six", ">=1.9.0,<1.11.0" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "1.10.0", match.ToString(), "1.10.0 is higher than 1.9.0" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void FindMatchingVersionClassicDependencyPin()
		{
			// The pin requests carried for years, which excludes the whole of urllib3 2.x
			PythonVersion? match = PythonVersionMatcher.FindMatchingVersion( "urllib3", ">=1.21.1,<1.27" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "1.26.20", match.ToString(), "Highest urllib3 1.26.x release" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void FindMatchingVersionNoSpecifierReturnsLatestFinal()
		{
			PythonVersion? match = PythonVersionMatcher.FindMatchingVersion( "requests", "" );

			Assert.IsNotNull( match, "No version found" );
			Assert.IsGreaterThanOrEqualTo( 0, match.CompareTo( new PythonVersion( "2.32.3" ) ), $"'{match}' should be at least 2.32.3" );
			Assert.IsFalse( match.IsPreRelease, $"'{match}' should not be a pre-release" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void FindMatchingVersionNoSatisfyingVersionReturnsNull()
		{
			PythonVersion? match = PythonVersionMatcher.FindMatchingVersion( "requests", ">=99.0" );

			Assert.IsNull( match, "No requests version is >= 99.0" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void FindMatchingVersionUnknownPackageReturnsNull()
		{
			PythonVersion? match = PythonVersionMatcher.FindMatchingVersion( "eternal-no-such-package-9f3a7c", ">=0" );

			Assert.IsNull( match, "A package that does not exist has no versions" );
		}

		// ---- PythonPackage.GetPythonPackage: blank, a specific version, or a specifier (live, as above) ----

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		public void GetPythonPackageBlankRequirementsUseTheLatestVersion()
		{
			PythonPackage? latest_package = PythonPackage.GetPythonPackage( "requests", "", false );
			PythonPackage? whitespace_package = PythonPackage.GetPythonPackage( "requests", "   ", false );

			Assert.IsNotNull( latest_package, "No package found for blank requirements" );
			Assert.IsNotNull( whitespace_package, "No package found for whitespace requirements" );
			Assert.IsGreaterThanOrEqualTo( 0, new PythonVersion( latest_package.Version ).CompareTo( new PythonVersion( "2.32.3" ) ), $"'{latest_package.Version}' should be at least 2.32.3" );
			Assert.AreEqual( latest_package.Version, whitespace_package.Version, "Whitespace should be the same as blank" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		[DataRow( "requests", "2.25.1", "2.25.1" )]
		[DataRow( "six", "1.10.0", "1.10.0" )]
		// A pre-release can be requested by its exact number, even though it can never satisfy a specifier
		[DataRow( "requests", "2.0.0", "2.0.0" )]
		public void GetPythonPackageSpecificVersion( string packageName, string requirements, string expectedVersion )
		{
			PythonPackage? python_package = PythonPackage.GetPythonPackage( packageName, requirements, false );

			Assert.IsNotNull( python_package, $"No package found for '{packageName}' '{requirements}'" );
			Assert.AreEqual( expectedVersion, python_package.Version, $"Version for '{packageName}' '{requirements}'" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		[DataRow( "requests", ">=2.20,<2.26", "2.25.1" )]
		[DataRow( "requests", "~=2.25.0", "2.25.1" )]
		[DataRow( "requests", "==1.*", "1.2.3" )]
		[DataRow( "requests", "<2.10", "2.9.2" )]
		[DataRow( "six", ">=1.9.0,<1.11.0", "1.10.0" )]
		[DataRow( "six", "~=1.9", "1.17.0" )]
		[DataRow( "urllib3", ">=1.21.1,<1.27", "1.26.20" )]
		[DataRow( "chardet", "<3", "2.3.0" )]
		public void GetPythonPackageSpecifierSelectsTheHighestSatisfyingVersion( string packageName, string requirements, string expectedVersion )
		{
			PythonPackage? python_package = PythonPackage.GetPythonPackage( packageName, requirements, false );

			Assert.IsNotNull( python_package, $"No package found for '{packageName}' '{requirements}'" );
			Assert.AreEqual( expectedVersion, python_package.Version, $"Version for '{packageName}' '{requirements}'" );
		}

		[TestMethod]
		[TestCategory( "PythonVersion" )]
		[DataRow( "requests", ">=99.0" )]
		[DataRow( "requests", "99.0.0" )]
		[DataRow( "requests", ">=not-a-version" )]
		[DataRow( "eternal-no-such-package-9f3a7c", "" )]
		public void GetPythonPackageWithNoSatisfyingVersionReturnsNull( string packageName, string requirements )
		{
			PythonPackage? python_package = PythonPackage.GetPythonPackage( packageName, requirements, false );

			Assert.IsNull( python_package, $"No package should be found for '{packageName}' '{requirements}'" );
		}
	}
}
