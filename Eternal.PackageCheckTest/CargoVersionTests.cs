// Copyright Eternal Developments LLC. All Rights Reserved.

using Eternal.PackageCheck;

namespace Eternal.PackageCheckTest
{
	[TestClass]
	public sealed class CargoVersionTests
	{
		private static int Compare( string left, string right )
		{
			return Math.Sign( new CargoVersion( left ).CompareTo( new CargoVersion( right ) ) );
		}

		private static bool Satisfies( string version, params string[] constraints )
		{
			CargoRequirement requirement = new CargoRequirement( constraints );
			return requirement.SatisfiedBy( new CargoVersion( version ) );
		}

		private static List<CargoCrateVersionRaw> MakeVersions( params string[] numbers )
		{
			List<CargoCrateVersionRaw> versions = new List<CargoCrateVersionRaw>();
			foreach( string number in numbers )
			{
				versions.Add( new CargoCrateVersionRaw { num = number, yanked = false } );
			}

			return versions;
		}

		// ---- CargoVersion: parsing ----

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		public void CargoVersionParsesDottedParts()
		{
			CargoVersion version = new CargoVersion( "1.2.3" );

			Assert.AreEqual( 1, version.Major, "Major mismatch" );
			Assert.AreEqual( 2, version.Minor, "Minor mismatch" );
			Assert.AreEqual( 3, version.Patch, "Patch mismatch" );
			Assert.AreEqual( 3, version.SpecifiedParts, "All three parts were written out" );
			Assert.IsFalse( version.IsPreRelease, "A plain version is not a prerelease" );
			Assert.AreEqual( "1.2.3", version.ToString(), "ToString should return the original text" );
		}

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		public void CargoVersionDefaultsOmittedPartsToZero()
		{
			CargoVersion major_only = new CargoVersion( "1" );
			Assert.AreEqual( 1, major_only.SpecifiedParts, "Only the major version was written out" );
			Assert.AreEqual( 0, major_only.Minor, "An omitted minor version defaults to zero" );
			Assert.AreEqual( 0, major_only.Patch, "An omitted patch version defaults to zero" );

			CargoVersion major_minor = new CargoVersion( "1.2" );
			Assert.AreEqual( 2, major_minor.SpecifiedParts, "The major and minor versions were written out" );
			Assert.AreEqual( 0, major_minor.Patch, "An omitted patch version defaults to zero" );
		}

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		public void CargoVersionParsesPreReleaseIdentifiers()
		{
			CargoVersion version = new CargoVersion( "1.0.0-rc.1" );

			Assert.IsTrue( version.IsPreRelease, "A hyphenated suffix marks a prerelease" );
			CollectionAssert.AreEqual( new string[] { "rc", "1" }, version.PreRelease, "Prerelease identifiers mismatch" );
			Assert.AreEqual( "1.0.0-rc.1", version.ToString(), "ToString should keep the suffix" );
		}

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		public void CargoVersionIgnoresBuildMetadata()
		{
			CargoVersion version = new CargoVersion( "1.2.3+build.5" );

			Assert.AreEqual( 3, version.Patch, "Build metadata should not affect the numeric parts" );
			Assert.IsFalse( version.IsPreRelease, "Build metadata is not a prerelease" );
			Assert.AreEqual( 0, Compare( "1.2.3+build.5", "1.2.3" ), "Build metadata is ignored when ordering" );
		}

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		[DataRow( "" )]
		[DataRow( "abc" )]
		[DataRow( "1.2.3.4" )]
		[DataRow( "1.x.3" )]
		public void CargoVersionRejectsMalformedVersions( string version )
		{
			Assert.ThrowsExactly<FormatException>( () => new CargoVersion( version ), $"'{version}' should not parse" );
		}

		// ---- CargoVersion: ordering ----

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		[DataRow( "1.2.3", "1.2.3", 0 )]
		[DataRow( "1.2.4", "1.2.3", 1 )]
		[DataRow( "1.3.0", "1.2.9", 1 )]
		[DataRow( "2.0.0", "1.9.9", 1 )]
		[DataRow( "1.2.3", "1.2.4", -1 )]
		[DataRow( "1.2", "1.2.0", 0 )]
		[DataRow( "1", "1.0.0", 0 )]
		public void CargoVersionOrdersNumericParts( string left, string right, int expected )
		{
			Assert.AreEqual( expected, Compare( left, right ), $"'{left}' vs '{right}' mismatch" );
		}

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		[DataRow( "1.0.0-rc.1", "1.0.0", -1 )]
		[DataRow( "1.0.0", "1.0.0-rc.1", 1 )]
		[DataRow( "1.0.0-rc.1", "1.0.0-rc.2", -1 )]
		[DataRow( "1.0.0-rc.2", "1.0.0-rc.10", -1 )]
		[DataRow( "1.0.0-alpha", "1.0.0-beta", -1 )]
		[DataRow( "1.0.0-alpha", "1.0.0-alpha.1", -1 )]
		[DataRow( "1.0.0-1", "1.0.0-alpha", -1 )]
		public void CargoVersionOrdersPreReleasesBelowTheirRelease( string left, string right, int expected )
		{
			Assert.AreEqual( expected, Compare( left, right ), $"'{left}' vs '{right}' mismatch" );
		}

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		public void CargoVersionCompareCoreIgnoresPreRelease()
		{
			CargoVersion prerelease = new CargoVersion( "1.0.0-rc.1" );
			CargoVersion release = new CargoVersion( "1.0.0" );

			Assert.AreEqual( 0, prerelease.CompareCore( release ), "The numeric parts are the same" );
			Assert.AreEqual( -1, Math.Sign( prerelease.CompareTo( release ) ), "Full ordering still ranks the prerelease lower" );
		}

		// ---- CargoRequirement: caret, the Cargo default ----

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		[DataRow( "1.2.3", "^1.2.3", true )]
		[DataRow( "1.9.9", "^1.2.3", true )]
		[DataRow( "1.2.2", "^1.2.3", false )]
		[DataRow( "2.0.0", "^1.2.3", false )]
		[DataRow( "1.2.0", "^1.2", true )]
		[DataRow( "1.9.9", "^1.2", true )]
		[DataRow( "2.0.0", "^1.2", false )]
		[DataRow( "1.0.0", "^1", true )]
		[DataRow( "1.9.9", "^1", true )]
		[DataRow( "2.0.0", "^1", false )]
		public void CaretRequirementAllowsCompatibleUpdates( string version, string constraint, bool expected )
		{
			Assert.AreEqual( expected, Satisfies( version, constraint ), $"'{version}' against '{constraint}' mismatch" );
		}

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		[DataRow( "0.2.3", "^0.2.3", true )]
		[DataRow( "0.2.9", "^0.2.3", true )]
		[DataRow( "0.3.0", "^0.2.3", false )]
		[DataRow( "0.2.0", "^0.2", true )]
		[DataRow( "0.2.9", "^0.2", true )]
		[DataRow( "0.3.0", "^0.2", false )]
		[DataRow( "0.0.3", "^0.0.3", true )]
		[DataRow( "0.0.4", "^0.0.3", false )]
		[DataRow( "0.0.9", "^0.0", true )]
		[DataRow( "0.1.0", "^0.0", false )]
		[DataRow( "0.9.9", "^0", true )]
		[DataRow( "1.0.0", "^0", false )]
		public void CaretRequirementTreatsLeadingZeroesAsBreaking( string version, string constraint, bool expected )
		{
			// A zero major version gets no compatibility guarantee beyond the leftmost non-zero component, so "^0.2.3" stops at 0.3.0 rather than 1.0.0
			Assert.AreEqual( expected, Satisfies( version, constraint ), $"'{version}' against '{constraint}' mismatch" );
		}

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		[DataRow( "1.9.9", "1.2.3", true )]
		[DataRow( "2.0.0", "1.2.3", false )]
		public void BareRequirementIsACaretRequirement( string version, string constraint, bool expected )
		{
			Assert.AreEqual( expected, Satisfies( version, constraint ), $"'{version}' against the bare '{constraint}' mismatch" );
		}

		// ---- CargoRequirement: tilde and wildcard ----

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		[DataRow( "1.2.3", "~1.2.3", true )]
		[DataRow( "1.2.9", "~1.2.3", true )]
		[DataRow( "1.3.0", "~1.2.3", false )]
		[DataRow( "1.2.0", "~1.2", true )]
		[DataRow( "1.2.9", "~1.2", true )]
		[DataRow( "1.3.0", "~1.2", false )]
		[DataRow( "1.0.0", "~1", true )]
		[DataRow( "1.9.9", "~1", true )]
		[DataRow( "2.0.0", "~1", false )]
		public void TildeRequirementBoundsAtTheNextMinor( string version, string constraint, bool expected )
		{
			Assert.AreEqual( expected, Satisfies( version, constraint ), $"'{version}' against '{constraint}' mismatch" );
		}

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		[DataRow( "1.0.0", "1.*", true )]
		[DataRow( "1.9.9", "1.*", true )]
		[DataRow( "2.0.0", "1.*", false )]
		[DataRow( "1.2.0", "1.2.*", true )]
		[DataRow( "1.2.9", "1.2.*", true )]
		[DataRow( "1.3.0", "1.2.*", false )]
		[DataRow( "9.9.9", "*", true )]
		public void WildcardRequirementBoundsAtTheLastPinnedPart( string version, string constraint, bool expected )
		{
			Assert.AreEqual( expected, Satisfies( version, constraint ), $"'{version}' against '{constraint}' mismatch" );
		}

		// ---- CargoRequirement: comparison operators ----

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		[DataRow( "1.2.3", ">=1.2.3", true )]
		[DataRow( "1.2.2", ">=1.2.3", false )]
		[DataRow( "1.2.3", "<=1.2.3", true )]
		[DataRow( "1.2.4", "<=1.2.3", false )]
		[DataRow( "1.2.4", ">1.2.3", true )]
		[DataRow( "1.2.3", ">1.2.3", false )]
		[DataRow( "1.2.2", "<1.2.3", true )]
		[DataRow( "1.2.3", "<1.2.3", false )]
		[DataRow( "1.2.3", "=1.2.3", true )]
		[DataRow( "1.2.4", "=1.2.3", false )]
		public void ComparisonRequirementsMatchExactBounds( string version, string constraint, bool expected )
		{
			Assert.AreEqual( expected, Satisfies( version, constraint ), $"'{version}' against '{constraint}' mismatch" );
		}

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		public void EqualsRequirementPinsOnlyWhatIsWritten()
		{
			// "=1.2" leaves the patch version free, unlike "=1.2.0"
			Assert.IsTrue( Satisfies( "1.2.9", "=1.2" ), "'=1.2' should accept any 1.2.x" );
			Assert.IsFalse( Satisfies( "1.3.0", "=1.2" ), "'=1.2' should not accept 1.3.0" );
			Assert.IsFalse( Satisfies( "1.2.9", "=1.2.0" ), "'=1.2.0' should pin the patch version" );
		}

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		public void MultipleConstraintsMustAllBeSatisfied()
		{
			Assert.IsTrue( Satisfies( "1.3.0", ">=1.2", "<1.5" ), "1.3.0 is inside the range" );
			Assert.IsFalse( Satisfies( "1.5.0", ">=1.2", "<1.5" ), "1.5.0 is above the range" );
			Assert.IsFalse( Satisfies( "1.1.0", ">=1.2", "<1.5" ), "1.1.0 is below the range" );
		}

		// ---- CargoRequirement: prereleases are opt-in ----

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		public void PreReleasesAreOnlyMatchedWhenRequested()
		{
			// Cargo never resolves to a prerelease unless the requirement names one for the same release
			Assert.IsFalse( Satisfies( "2.0.0-rc.1", "^1.0.0" ), "A prerelease of a later release must not match" );
			Assert.IsFalse( Satisfies( "1.0.0-rc.1", "^1.0.0" ), "A prerelease must not match a stable requirement" );
			Assert.IsFalse( Satisfies( "1.0.0-rc.1", "*" ), "A bare wildcard does not ask for prereleases" );

			Assert.IsTrue( Satisfies( "1.0.0-rc.2", "^1.0.0-rc.1" ), "A later prerelease of the named release matches" );
			Assert.IsTrue( Satisfies( "1.0.0", "^1.0.0-rc.1" ), "The stable release still matches a prerelease requirement" );
		}

		// ---- CargoVersionMatcher ----

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		public void SplitRequirementsTrimsAndDropsEmptyEntries()
		{
			string[] constraints = CargoVersionMatcher.SplitRequirements( ">=1.2 ,, <1.5 " );

			CollectionAssert.AreEqual( new string[] { ">=1.2", "<1.5" }, constraints, "Constraints mismatch" );
			Assert.IsEmpty( CargoVersionMatcher.SplitRequirements( "" ), "An empty requirement yields no constraints" );
		}

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		public void SelectMatchingVersionPicksTheHighestMatchRegardlessOfListOrder()
		{
			// crates.io lists versions in publication order, so a patch to an older line can appear ahead of a newer release
			List<CargoCrateVersionRaw> versions = MakeVersions( "1.0.3", "0.8.8", "1.0.1", "0.9.5", "1.0.2" );

			CargoVersion? selected = CargoVersionMatcher.SelectMatchingVersion( "test-crate", versions, [ "^1.0" ] );

			Assert.IsNotNull( selected, "A matching version should be found" );
			Assert.AreEqual( "1.0.3", selected.ToString(), "The highest matching version should win" );
		}

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		public void SelectMatchingVersionWithNoConstraintsPicksTheHighestStableVersion()
		{
			List<CargoCrateVersionRaw> versions = MakeVersions( "1.0.0", "2.0.0-rc.1", "1.5.0" );

			CargoVersion? selected = CargoVersionMatcher.SelectMatchingVersion( "test-crate", versions, [] );

			Assert.IsNotNull( selected, "A matching version should be found" );
			Assert.AreEqual( "1.5.0", selected.ToString(), "A prerelease should not be selected by default" );
		}

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		public void SelectMatchingVersionSkipsYankedVersions()
		{
			List<CargoCrateVersionRaw> versions = MakeVersions( "1.0.0", "1.1.0", "1.2.0" );
			versions.Find( x => x.num == "1.2.0" )!.yanked = true;

			CargoVersion? selected = CargoVersionMatcher.SelectMatchingVersion( "test-crate", versions, [ "^1.0" ] );

			Assert.IsNotNull( selected, "A matching version should be found" );
			Assert.AreEqual( "1.1.0", selected.ToString(), "A yanked version must never be selected" );
		}

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		public void SelectMatchingVersionIgnoresUnparsableVersions()
		{
			List<CargoCrateVersionRaw> versions = MakeVersions( "1.0.0", "not-a-version", "1.1.0" );

			CargoVersion? selected = CargoVersionMatcher.SelectMatchingVersion( "test-crate", versions, [ "^1.0" ] );

			Assert.IsNotNull( selected, "The parsable versions should still be considered" );
			Assert.AreEqual( "1.1.0", selected.ToString(), "The highest parsable matching version should win" );
		}

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		public void SelectMatchingVersionReturnsNullWhenNothingMatches()
		{
			List<CargoCrateVersionRaw> versions = MakeVersions( "1.0.0", "1.1.0" );

			Assert.IsNull( CargoVersionMatcher.SelectMatchingVersion( "test-crate", versions, [ "^2.0" ] ), "No 2.x version exists" );
			Assert.IsNull( CargoVersionMatcher.SelectMatchingVersion( "test-crate", [], [ "^1.0" ] ), "An empty version list cannot match" );
		}

		[TestMethod]
		[TestCategory( "CargoVersion" )]
		public void SelectMatchingVersionReturnsNullWhenEveryVersionIsYanked()
		{
			List<CargoCrateVersionRaw> versions = MakeVersions( "1.0.0", "1.1.0" );
			foreach( CargoCrateVersionRaw version in versions )
			{
				version.yanked = true;
			}

			Assert.IsNull( CargoVersionMatcher.SelectMatchingVersion( "test-crate", versions, [ "^1.0" ] ), "Every version is yanked" );
		}
	}
}
