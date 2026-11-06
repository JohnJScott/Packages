// Copyright Eternal Developments LLC. All Rights Reserved.

using Eternal.PackageCheck;

namespace Eternal.PackageCheckTest
{
	[TestClass]
	public sealed class RubyVersionTests
	{
		private static int Compare( string left, string right )
		{
			return Math.Sign( new GemVersion( left ).CompareTo( new GemVersion( right ) ) );
		}

		private static bool Satisfies( string version, params string[] constraints )
		{
			GemRequirement requirement = new GemRequirement( constraints );
			return requirement.SatisfiedBy( new GemVersion( version ) );
		}

		private static List<RubyGemVersionRaw> MakeVersions( params string[] numbers )
		{
			List<RubyGemVersionRaw> versions = new List<RubyGemVersionRaw>();
			foreach( string number in numbers )
			{
				versions.Add( new RubyGemVersionRaw { number = number, prerelease = number.Any( char.IsLetter ), platform = "ruby" } );
			}

			return versions;
		}

		// ---- GemVersion: parsing ----

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void GemVersionParsesDottedParts()
		{
			GemVersion version = new GemVersion( "3.1.22" );

			CollectionAssert.AreEqual( new int[] { 3, 1, 22 }, version.Parts, "Parts mismatch" );
			Assert.AreEqual( "3.1.22", version.ToString(), "ToString should return the original text" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void GemVersionIgnoresHyphenatedSuffix()
		{
			GemVersion version = new GemVersion( "1.2.3-rc1" );

			CollectionAssert.AreEqual( new int[] { 1, 2, 3 }, version.Parts, "The suffix after '-' should be ignored" );
			Assert.AreEqual( "1.2.3-rc1", version.ToString(), "ToString should keep the suffix" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		[DataRow( "1.0.0.rc1" )]
		[DataRow( "1.0.0.beta" )]
		[DataRow( "7.0.0.alpha1" )]
		[DataRow( "abc" )]
		[DataRow( "" )]
		public void GemVersionRejectsNonNumericSegments( string version )
		{
			Assert.ThrowsExactly<FormatException>( () => new GemVersion( version ), $"'{version}' should not parse" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void GemVersionPartAtPadsMissingPartsWithZero()
		{
			GemVersion version = new GemVersion( "1.2" );

			Assert.AreEqual( 1, version.PartAt( 0 ), "Part 0" );
			Assert.AreEqual( 2, version.PartAt( 1 ), "Part 1" );
			Assert.AreEqual( 0, version.PartAt( 2 ), "Part 2 is past the end and should be 0" );
			Assert.AreEqual( 0, version.PartAt( 10 ), "Part 10 is past the end and should be 0" );
		}

		// ---- GemVersion: ordering ----

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		[DataRow( "1.0.0", "1.0.0", 0 )]
		[DataRow( "1.0.1", "1.0.0", 1 )]
		[DataRow( "0.9", "1.0", -1 )]
		[DataRow( "2.0", "1.99.99", 1 )]
		// Parts compare numerically, not as text
		[DataRow( "1.10", "1.9", 1 )]
		[DataRow( "10.0", "9.99", 1 )]
		[DataRow( "7.0.10", "7.0.8.7", 1 )]
		// Missing trailing parts are zero, so these are the same version
		[DataRow( "1.0", "1.0.0", 0 )]
		[DataRow( "1", "1.0.0.0", 0 )]
		[DataRow( "3.1.22", "3.1.22.0", 0 )]
		// ...but a non-zero extra part makes the longer version higher
		[DataRow( "1.0", "1.0.1", -1 )]
		[DataRow( "1.0.0.1", "1.0", 1 )]
		public void GemVersionCompareTo( string left, string right, int expected )
		{
			Assert.AreEqual( expected, Compare( left, right ), $"'{left}' compared with '{right}'" );
			Assert.AreEqual( -expected, Compare( right, left ), $"'{right}' compared with '{left}' (should be the mirror image)" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void GemVersionCompareToNullIsGreater()
		{
			Assert.AreEqual( 1, new GemVersion( "1.0" ).CompareTo( null ), "Any version is greater than null" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void GemVersionMaxPicksNumericallyHighest()
		{
			List<GemVersion> versions = new List<GemVersion>
			{
				new GemVersion( "1.9" ),
				new GemVersion( "1.10" ),
				new GemVersion( "1.2" ),
				new GemVersion( "1.0.0" ),
				new GemVersion( "1.0" )
			};

			Assert.AreEqual( "1.10", versions.Max()!.ToString(), "1.10 is higher than 1.9" );
		}

		// ---- GemRequirement: operators ----

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		[DataRow( "= 1.2.3", "1.2.3", true )]
		[DataRow( "= 1.2.3", "1.2.4", false )]
		[DataRow( "1.2.3", "1.2.3", true )]
		[DataRow( "1.2.3", "1.2.4", false )]
		[DataRow( "= 1.0", "1.0.0", true )]
		[DataRow( "!= 1.2.3", "1.2.3", false )]
		[DataRow( "!= 1.2.3", "1.2.4", true )]
		[DataRow( "!= 1.0", "1.0.0", false )]
		[DataRow( ">= 1.2.3", "1.2.3", true )]
		[DataRow( ">= 1.2.3", "1.2.2", false )]
		[DataRow( ">= 1.2.3", "1.10.0", true )]
		[DataRow( ">= 1.0.0", "1.0", true )]
		[DataRow( "> 1.2.3", "1.2.3", false )]
		[DataRow( "> 1.2.3", "1.2.4", true )]
		[DataRow( "> 1.0", "1.0.0", false )]
		[DataRow( "<= 1.2.3", "1.2.3", true )]
		[DataRow( "<= 1.2.3", "1.2.4", false )]
		[DataRow( "<= 1.0", "1.0.0", true )]
		[DataRow( "<= 1.0", "1.0.1", false )]
		[DataRow( "< 1.2.3", "1.2.3", false )]
		[DataRow( "< 1.2.3", "1.2.2", true )]
		[DataRow( "< 1.0", "1.0.0", false )]
		[DataRow( "< 4", "3.2.7", true )]
		[DataRow( "< 4", "4.0.0", false )]
		[DataRow( ">= 0", "0", true )]
		[DataRow( ">= 0", "99.9.9", true )]
		public void GemRequirementOperators( string constraint, string version, bool expected )
		{
			Assert.AreEqual( expected, Satisfies( version, constraint ), $"'{version}' against '{constraint}'" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		[DataRow( ">=1.0", "1.0", true )]
		[DataRow( ">=1.0", "0.9", false )]
		[DataRow( "<=1.0", "1.0", true )]
		[DataRow( "<1.0", "1.0", false )]
		[DataRow( "~>1.2", "1.5", true )]
		[DataRow( "~>1.2", "2.0", false )]
		[DataRow( "  >=   1.0  ", "1.5", true )]
		[DataRow( "  ~>   1.2  ", "1.5", true )]
		public void GemRequirementToleratesWhitespace( string constraint, string version, bool expected )
		{
			Assert.AreEqual( expected, Satisfies( version, constraint ), $"'{version}' against '{constraint}'" );
		}

		// ---- GemRequirement: pessimistic operator ----

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		// A single segment locks the first segment: ~> 1 is >= 1 and < 2
		[DataRow( "~> 1", "1", true )]
		[DataRow( "~> 1", "1.5", true )]
		[DataRow( "~> 1", "1.99.99", true )]
		[DataRow( "~> 1", "2.0", false )]
		[DataRow( "~> 1", "3.1.4", false )]
		[DataRow( "~> 1", "0.9", false )]
		// Two segments: ~> 1.2 is >= 1.2 and < 2.0
		[DataRow( "~> 1.2", "1.2", true )]
		[DataRow( "~> 1.2", "1.9.9", true )]
		[DataRow( "~> 1.2", "2.0", false )]
		[DataRow( "~> 1.2", "1.1.9", false )]
		[DataRow( "~> 13.0", "13.9.2", true )]
		[DataRow( "~> 13.0", "14.0", false )]
		[DataRow( "~> 0.9", "0.9.5", true )]
		[DataRow( "~> 0.9", "0.10.0", true )]
		[DataRow( "~> 0.9", "1.0", false )]
		// Three segments: ~> 1.2.3 is >= 1.2.3 and < 1.3
		[DataRow( "~> 1.2.3", "1.2.3", true )]
		[DataRow( "~> 1.2.3", "1.2.9", true )]
		[DataRow( "~> 1.2.3", "1.2.10", true )]
		[DataRow( "~> 1.2.3", "1.3.0", false )]
		[DataRow( "~> 1.2.3", "1.2.2", false )]
		[DataRow( "~> 1.2.3", "1.2", false )]
		[DataRow( "~> 13.0.5", "13.0.9", true )]
		[DataRow( "~> 13.0.5", "13.1.0", false )]
		[DataRow( "~> 13.0.5", "13.0.4", false )]
		// Four segments: ~> 6.1.7.1 is >= 6.1.7.1 and < 6.1.8
		[DataRow( "~> 6.1.7.1", "6.1.7.10", true )]
		[DataRow( "~> 6.1.7.1", "6.1.8", false )]
		// Trailing zeros are significant for the lock position but not for equality
		[DataRow( "~> 1.2.0", "1.2.9", true )]
		[DataRow( "~> 1.2.0", "1.3.0", false )]
		[DataRow( "~> 2.0", "2", true )]
		[DataRow( "~> 2.0.0", "2.0", true )]
		public void GemRequirementPessimistic( string constraint, string version, bool expected )
		{
			Assert.AreEqual( expected, Satisfies( version, constraint ), $"'{version}' against '{constraint}'" );
		}

		// ---- GemRequirement: combinations ----

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void GemRequirementMultipleConstraintsAreAllRequired()
		{
			Assert.IsTrue( Satisfies( "3.2.7", ">= 3.0.0", "< 4" ), "3.2.7 is inside the range" );
			Assert.IsTrue( Satisfies( "3.0.0", ">= 3.0.0", "< 4" ), "3.0.0 is the inclusive lower bound" );
			Assert.IsFalse( Satisfies( "4.0.0", ">= 3.0.0", "< 4" ), "4.0.0 is the exclusive upper bound" );
			Assert.IsFalse( Satisfies( "2.9.9", ">= 3.0.0", "< 4" ), "2.9.9 is below the range" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void GemRequirementContradictoryConstraintsMatchNothing()
		{
			Assert.IsFalse( Satisfies( "0.5", ">= 2", "< 1" ), "0.5" );
			Assert.IsFalse( Satisfies( "1.5", ">= 2", "< 1" ), "1.5" );
			Assert.IsFalse( Satisfies( "3.0", ">= 2", "< 1" ), "3.0" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void GemRequirementPessimisticWithExclusion()
		{
			Assert.IsFalse( Satisfies( "1.2.5", "~> 1.2", "!= 1.2.5" ), "1.2.5 is excluded" );
			Assert.IsTrue( Satisfies( "1.2.6", "~> 1.2", "!= 1.2.5" ), "1.2.6 is allowed" );
			Assert.IsFalse( Satisfies( "2.0.0", "~> 1.2", "!= 1.2.5" ), "2.0.0 is outside the pessimistic range" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void GemRequirementWithNoConstraintsMatchesEverything()
		{
			Assert.IsTrue( Satisfies( "0" ), "0" );
			Assert.IsTrue( Satisfies( "99.99.99" ), "99.99.99" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		[DataRow( ">= 1.0.0.rc1" )]
		[DataRow( "~> 1.0.beta" )]
		[DataRow( ">= abc" )]
		[DataRow( ">=" )]
		public void GemRequirementRejectsUnparsableConstraints( string constraint )
		{
			Assert.ThrowsExactly<FormatException>( () => new GemRequirement( new string[] { constraint } ), $"'{constraint}' should not parse" );
		}

		// ---- RubyGemVersionMatcher: requirement strings and selection from a version list ----

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void SplitRequirementsSeparatesCommaJoinedConstraints()
		{
			CollectionAssert.AreEqual( new string[] { ">= 1.6.0", "< 4" }, RubyGemVersionMatcher.SplitRequirements( ">= 1.6.0, < 4" ), "Two constraints" );
			CollectionAssert.AreEqual( new string[] { "~> 1.2" }, RubyGemVersionMatcher.SplitRequirements( "~> 1.2" ), "One constraint" );
			CollectionAssert.AreEqual( new string[] { ">= 1", "< 2" }, RubyGemVersionMatcher.SplitRequirements( "  >= 1 ,< 2  " ), "Whitespace should be trimmed" );
			Assert.HasCount( 0, RubyGemVersionMatcher.SplitRequirements( "" ), "A blank string has no constraints" );
			Assert.HasCount( 0, RubyGemVersionMatcher.SplitRequirements( " , " ), "Separators alone have no constraints" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void SelectMatchingVersionComparesNumericallyAndSkipsPrereleases()
		{
			// 7.0.8.7 is lexically higher than 7.0.10, and the prereleases cannot be parsed as numbers
			List<RubyGemVersionRaw> versions = MakeVersions( "7.0.0.alpha1", "7.0.0.rc1", "7.0.0", "7.0.8.7", "7.0.10", "6.1.7.10", "7.1.0" );

			GemVersion? match = RubyGemVersionMatcher.SelectMatchingVersion( "rails", versions, new string[] { ">= 7.0.0", "< 7.1" } );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "7.0.10", match.ToString(), "Highest stable version in range" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void SelectMatchingVersionIgnoresOrderOfTheList()
		{
			List<RubyGemVersionRaw> versions = MakeVersions( "1.9", "1.10", "1.2" );

			GemVersion? forward = RubyGemVersionMatcher.SelectMatchingVersion( "gem", versions, Array.Empty<string>() );
			versions.Reverse();
			GemVersion? backward = RubyGemVersionMatcher.SelectMatchingVersion( "gem", versions, Array.Empty<string>() );

			Assert.IsNotNull( forward, "No version found for the original order" );
			Assert.IsNotNull( backward, "No version found for the reversed order" );
			Assert.AreEqual( "1.10", forward.ToString(), "Original order" );
			Assert.AreEqual( "1.10", backward.ToString(), "Reversed order" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void SelectMatchingVersionWithNoConstraintsReturnsHighestStable()
		{
			List<RubyGemVersionRaw> versions = MakeVersions( "9.9", "10.0", "11.0.0.beta1" );

			GemVersion? match = RubyGemVersionMatcher.SelectMatchingVersion( "gem", versions, Array.Empty<string>() );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "10.0", match.ToString(), "The prerelease must not be chosen even though it is higher" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void SelectMatchingVersionToleratesDuplicateVersionNumbers()
		{
			List<RubyGemVersionRaw> versions = MakeVersions( "3.1.21", "3.1.21", "3.1.20", "3.1.22" );

			GemVersion? match = RubyGemVersionMatcher.SelectMatchingVersion( "bcrypt", versions, new string[] { "<= 3.1.21" } );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "3.1.21", match.ToString(), "Highest version at or below 3.1.21" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void SelectMatchingVersionReturnsNullWhenNothingQualifies()
		{
			Assert.IsNull( RubyGemVersionMatcher.SelectMatchingVersion( "gem", MakeVersions( "1.0", "2.0" ), new string[] { ">= 3.0" } ), "No version satisfies the constraint" );
			Assert.IsNull( RubyGemVersionMatcher.SelectMatchingVersion( "gem", MakeVersions( "1.0.0.rc1", "1.0.0.beta2" ), Array.Empty<string>() ), "Only prereleases exist" );
			Assert.IsNull( RubyGemVersionMatcher.SelectMatchingVersion( "gem", MakeVersions(), Array.Empty<string>() ), "There are no versions at all" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void SelectMatchingVersionInvalidConstraintReturnsNull()
		{
			GemVersion? match = RubyGemVersionMatcher.SelectMatchingVersion( "gem", MakeVersions( "1.0", "2.0" ), new string[] { ">= not-a-version" } );

			Assert.IsNull( match, "An unparsable constraint should log and return null rather than throw" );
		}

		// The cases below read live data from RubyGems.org. The expected versions are all in ranges that have been closed for years, so they are stable.

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void FindMatchingVersionPicksHighestInRange()
		{
			GemVersion? match = RubyGemVersionMatcher.FindMatchingVersion( "sinatra", ">= 4.0", "< 4.2" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "4.1.1", match.ToString(), "Highest sinatra release below 4.2" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void FindMatchingVersionPessimisticRange()
		{
			GemVersion? match = RubyGemVersionMatcher.FindMatchingVersion( "rack", "~> 1.6" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "1.6.13", match.ToString(), "Highest rack 1.x release from 1.6" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void FindMatchingVersionComparesNumericallyAndSkipsPrereleases()
		{
			// The range holds 7.0.0.alpha1 to 7.0.0.rc3 (which cannot be parsed as numbers) and 7.0.8.7, which is lexically higher than 7.0.10
			GemVersion? match = RubyGemVersionMatcher.FindMatchingVersion( "rails", ">= 7.0.0", "< 7.1" );

			Assert.IsNotNull( match, "No version found; prereleases were probably not filtered out" );
			Assert.AreEqual( "7.0.10", match.ToString(), "Highest rails 7.0 release" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void FindMatchingVersionToleratesPlatformSpecificDuplicates()
		{
			// bcrypt publishes every version once for 'ruby' and again for 'java' (and more platforms for some)
			GemVersion? match = RubyGemVersionMatcher.FindMatchingVersion( "bcrypt", ">= 3.1.20", "<= 3.1.21" );

			Assert.IsNotNull( match, "No version found" );
			Assert.AreEqual( "3.1.21", match.ToString(), "Highest bcrypt release in range" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void FindMatchingVersionNoConstraintsReturnsLatestStable()
		{
			GemVersion? match = RubyGemVersionMatcher.FindMatchingVersion( "sinatra" );

			Assert.IsNotNull( match, "No version found" );
			Assert.IsGreaterThanOrEqualTo( 0, match.CompareTo( new GemVersion( "4.1.1" ) ), $"'{match}' should be at least 4.1.1" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void FindMatchingVersionNoSatisfyingVersionReturnsNull()
		{
			GemVersion? match = RubyGemVersionMatcher.FindMatchingVersion( "sinatra", ">= 99.0" );

			Assert.IsNull( match, "No sinatra version is >= 99.0" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void FindMatchingVersionUnknownGemReturnsNull()
		{
			GemVersion? match = RubyGemVersionMatcher.FindMatchingVersion( "eternal-no-such-gem-9f3a7c", ">= 0" );

			Assert.IsNull( match, "A gem that does not exist has no versions" );
		}

		// ---- RubyGem.GetRubyGem: blank, a specific version, or constraints (live, as above) ----

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		public void GetRubyGemBlankRequirementsUseTheLatestVersion()
		{
			RubyGem? latest_gem = RubyGem.GetRubyGem( "sinatra", "", false );
			RubyGem? whitespace_gem = RubyGem.GetRubyGem( "sinatra", "   ", false );

			Assert.IsNotNull( latest_gem, "No gem found for blank requirements" );
			Assert.IsNotNull( whitespace_gem, "No gem found for whitespace requirements" );
			Assert.IsGreaterThanOrEqualTo( 0, new GemVersion( latest_gem.Version ).CompareTo( new GemVersion( "4.2.1" ) ), $"'{latest_gem.Version}' should be at least 4.2.1" );
			Assert.AreEqual( latest_gem.Version, whitespace_gem.Version, "Whitespace should be the same as blank" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		[DataRow( "bcrypt", "3.1.21", "3.1.21" )]
		// A prerelease can be requested by its exact number, even though it can never satisfy a constraint
		[DataRow( "rails", "7.0.0.rc1", "7.0.0.rc1" )]
		// 3.1.21.0 is not a published number, but is the same version as 3.1.21
		[DataRow( "bcrypt", "3.1.21.0", "3.1.21" )]
		public void GetRubyGemSpecificVersion( string gemName, string requirements, string expectedVersion )
		{
			RubyGem? gem = RubyGem.GetRubyGem( gemName, requirements, false );

			Assert.IsNotNull( gem, $"No gem found for '{gemName}' '{requirements}'" );
			Assert.AreEqual( expectedVersion, gem.Version, $"Version for '{gemName}' '{requirements}'" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		[DataRow( "sinatra", ">= 4.0, < 4.2", "4.1.1" )]
		[DataRow( "sinatra", "< 4.2, >= 4.0", "4.1.1" )]
		[DataRow( "rack", "~> 1.6", "1.6.13" )]
		// Includes prereleases (7.0.0.rc1) and a higher number (7.0.10) that sorts before 7.0.8.7 as text
		[DataRow( "rails", ">= 7.0.0, < 7.1", "7.0.10" )]
		[DataRow( "bcrypt", "<= 3.1.21, >= 3.1.20", "3.1.21" )]
		public void GetRubyGemConstraintsSelectTheHighestSatisfyingVersion( string gemName, string requirements, string expectedVersion )
		{
			RubyGem? gem = RubyGem.GetRubyGem( gemName, requirements, false );

			Assert.IsNotNull( gem, $"No gem found for '{gemName}' '{requirements}'" );
			Assert.AreEqual( expectedVersion, gem.Version, $"Version for '{gemName}' '{requirements}'" );
		}

		[TestMethod]
		[TestCategory( "RubyVersion" )]
		[DataRow( "sinatra", ">= 99.0" )]
		[DataRow( "sinatra", "99.0.0" )]
		[DataRow( "sinatra", ">= not-a-version" )]
		[DataRow( "eternal-no-such-gem-9f3a7c", "" )]
		public void GetRubyGemWithNoSatisfyingVersionReturnsNull( string gemName, string requirements )
		{
			RubyGem? gem = RubyGem.GetRubyGem( gemName, requirements, false );

			Assert.IsNull( gem, $"No gem should be found for '{gemName}' '{requirements}'" );
		}
	}
}
