// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;

namespace Eternal.PackageCheck
{
	/// <summary>
	/// A Cargo (SemVer 2.0) version number. Cargo versions are always major.minor.patch, optionally followed by a prerelease ("-rc.1") and build metadata ("+build.5").
	/// Requirement versions may omit the minor and patch components, so the number of components actually written is retained in <see cref="SpecifiedParts"/> - the
	/// caret, tilde, and wildcard operators all bound differently depending on how much of the version was spelled out.
	/// </summary>
	internal class CargoVersion
		: IComparable<CargoVersion>
	{
		public int Major { get; }
		public int Minor { get; }
		public int Patch { get; }

		/// <summary>The dot separated prerelease identifiers, e.g. ["rc", "1"] for "1.0.0-rc.1"; empty for a stable release.</summary>
		public string[] PreRelease { get; }

		/// <summary>How many of major, minor, and patch were written out; 3 for any published version, but as few as 1 in a requirement such as "^1".</summary>
		public int SpecifiedParts { get; }

		private string OriginalVersion { get; }

		public bool IsPreRelease
		{
			get { return PreRelease.Length > 0; }
		}

		/// <summary>
		/// Parses a SemVer version or the version part of a Cargo requirement.
		/// </summary>
		/// <param name="version">The version text, e.g. "1.2.3", "1.2.3-rc.1+build.5", or the partial "1.2" as found in a requirement.</param>
		/// <exception cref="FormatException">The text is empty, has more than three numeric components, or a component is not a number.</exception>
		public CargoVersion( string version )
		{
			OriginalVersion = version;

			string remainder = version.Trim();
			if( remainder.Length == 0 )
			{
				throw new FormatException( "An empty string is not a valid version" );
			}

			// Build metadata is ignored entirely for both ordering and matching, so it is discarded up front
			int build_index = remainder.IndexOf( '+' );
			if( build_index >= 0 )
			{
				remainder = remainder[..build_index];
			}

			int prerelease_index = remainder.IndexOf( '-' );
			if( prerelease_index >= 0 )
			{
				string prerelease_text = remainder[( prerelease_index + 1 )..];
				remainder = remainder[..prerelease_index];

				PreRelease = prerelease_text.Split( '.', StringSplitOptions.RemoveEmptyEntries );
			}
			else
			{
				PreRelease = [];
			}

			string[] core_parts = remainder.Split( '.' );
			if( core_parts.Length > 3 )
			{
				throw new FormatException( $"'{version}' has more than three numeric components" );
			}

			SpecifiedParts = core_parts.Length;
			Major = ParsePart( core_parts, 0, version );
			Minor = ParsePart( core_parts, 1, version );
			Patch = ParsePart( core_parts, 2, version );
		}

		/// <summary>
		/// Builds a version from its numeric components, used when deriving the bounds implied by a caret, tilde, or wildcard requirement.
		/// </summary>
		public CargoVersion( int major, int minor, int patch )
		{
			Major = major;
			Minor = minor;
			Patch = patch;
			PreRelease = [];
			SpecifiedParts = 3;
			OriginalVersion = $"{major}.{minor}.{patch}";
		}

		private static int ParsePart( string[] parts, int index, string version )
		{
			if( index >= parts.Length )
			{
				return 0;
			}

			if( !int.TryParse( parts[index], out int value ) || value < 0 )
			{
				throw new FormatException( $"'{version}' has a non-numeric component '{parts[index]}'" );
			}

			return value;
		}

		/// <summary>
		/// Orders two versions by SemVer precedence: major, minor, and patch numerically, then a prerelease as lower than the release it precedes.
		/// </summary>
		public int CompareTo( CargoVersion? other )
		{
			if( other == null )
			{
				return 1;
			}

			int core_comparison = CompareCore( other );
			if( core_comparison != 0 )
			{
				return core_comparison;
			}

			return ComparePreRelease( other );
		}

		/// <summary>
		/// Compares only the major.minor.patch components, ignoring any prerelease. Used where a prerelease and its release count as the same version, such as when
		/// deciding whether a requirement that names a prerelease is talking about the same release as the version being tested.
		/// </summary>
		public int CompareCore( CargoVersion other )
		{
			int comparison = Major.CompareTo( other.Major );
			if( comparison != 0 )
			{
				return comparison;
			}

			comparison = Minor.CompareTo( other.Minor );
			if( comparison != 0 )
			{
				return comparison;
			}

			return Patch.CompareTo( other.Patch );
		}

		private int ComparePreRelease( CargoVersion other )
		{
			// A version without a prerelease outranks the same version with one
			if( PreRelease.Length == 0 && other.PreRelease.Length == 0 )
			{
				return 0;
			}

			if( PreRelease.Length == 0 )
			{
				return 1;
			}

			if( other.PreRelease.Length == 0 )
			{
				return -1;
			}

			int shared_length = Math.Min( PreRelease.Length, other.PreRelease.Length );
			for( int i = 0; i < shared_length; i++ )
			{
				int comparison = ComparePreReleaseIdentifier( PreRelease[i], other.PreRelease[i] );
				if( comparison != 0 )
				{
					return comparison;
				}
			}

			// Everything shared is equal, so the version with more identifiers is the higher of the two
			return PreRelease.Length.CompareTo( other.PreRelease.Length );
		}

		private static int ComparePreReleaseIdentifier( string left, string right )
		{
			bool left_is_numeric = int.TryParse( left, out int left_value );
			bool right_is_numeric = int.TryParse( right, out int right_value );

			if( left_is_numeric && right_is_numeric )
			{
				return left_value.CompareTo( right_value );
			}

			// SemVer orders numeric identifiers below alphanumeric ones
			if( left_is_numeric )
			{
				return -1;
			}

			if( right_is_numeric )
			{
				return 1;
			}

			return string.CompareOrdinal( left, right );
		}

		public override string ToString()
		{
			return OriginalVersion;
		}
	}

	/// <summary>
	/// A Cargo version requirement, as written in a Cargo.toml dependency - one or more comma separated constraints that must all be satisfied, e.g. "^1.2", ">=1.2, &lt;1.5".
	/// </summary>
	internal class CargoRequirement
	{
		private readonly List<(string Operator, CargoVersion Version)> Constraints = new List<(string Operator, CargoVersion Version)>();

		/// <summary>True if no constraint was supplied, in which case every version matches.</summary>
		private bool MatchesEverything { get; set; }

		public CargoRequirement( string[] constraints )
		{
			if( constraints.Length == 0 )
			{
				MatchesEverything = true;
			}

			foreach( string constraint in constraints )
			{
				ParseConstraint( constraint.Trim() );
			}
		}

		private void ParseConstraint( string constraint )
		{
			if( constraint.Length == 0 )
			{
				return;
			}

			// A bare "*" places no restriction at all, as does a requirement of "" which Cargo treats the same way
			if( constraint == "*" )
			{
				MatchesEverything = true;
				return;
			}

			string op;
			string version_text;

			if( constraint.StartsWith( ">=" ) )
			{
				op = ">=";
				version_text = constraint[2..].Trim();
			}
			else if( constraint.StartsWith( "<=" ) )
			{
				op = "<=";
				version_text = constraint[2..].Trim();
			}
			else if( constraint.StartsWith( "^" ) )
			{
				op = "^";
				version_text = constraint[1..].Trim();
			}
			else if( constraint.StartsWith( "~" ) )
			{
				op = "~";
				version_text = constraint[1..].Trim();
			}
			else if( constraint.StartsWith( ">" ) )
			{
				op = ">";
				version_text = constraint[1..].Trim();
			}
			else if( constraint.StartsWith( "<" ) )
			{
				op = "<";
				version_text = constraint[1..].Trim();
			}
			else if( constraint.StartsWith( "=" ) )
			{
				op = "=";
				version_text = constraint[1..].Trim();
			}
			else if( constraint.Contains( '*' ) )
			{
				op = "*";
				version_text = constraint;
			}
			else
			{
				// An operator-less requirement is a caret requirement, so "1.2.3" means the same thing as "^1.2.3"
				op = "^";
				version_text = constraint;
			}

			if( op == "*" )
			{
				Constraints.Add( (op, ParseWildcard( version_text )) );
				return;
			}

			Constraints.Add( (op, new CargoVersion( version_text )) );
		}

		/// <summary>
		/// Turns the numeric prefix of a wildcard requirement into a version whose <see cref="CargoVersion.SpecifiedParts"/> records how much was pinned, so "1.*"
		/// becomes 1.0.0 with one specified part and "1.2.*" becomes 1.2.0 with two.
		/// </summary>
		private static CargoVersion ParseWildcard( string constraint )
		{
			string[] parts = constraint.Split( '.' );
			List<string> numeric_parts = new List<string>();

			foreach( string part in parts )
			{
				if( part == "*" || part == "x" || part == "X" )
				{
					break;
				}

				numeric_parts.Add( part );
			}

			if( numeric_parts.Count == 0 )
			{
				return new CargoVersion( "0" );
			}

			return new CargoVersion( string.Join( '.', numeric_parts ) );
		}

		/// <summary>
		/// Determines whether a version satisfies every constraint in this requirement.
		/// </summary>
		/// <param name="version">The version to test.</param>
		/// <returns>true if the version satisfies all of the constraints.</returns>
		public bool SatisfiedBy( CargoVersion version )
		{
			if( MatchesEverything )
			{
				// A prerelease is still only acceptable when it was asked for, and a bare "*" never asks for one
				return !version.IsPreRelease;
			}

			if( version.IsPreRelease && !AllowsPreRelease( version ) )
			{
				return false;
			}

			foreach( (string op, CargoVersion constraint_version) in Constraints )
			{
				bool satisfied = op switch
				{
					"^" => SatisfiesRange( version, constraint_version, CaretUpperBound( constraint_version ) ),
					"~" => SatisfiesRange( version, constraint_version, TildeUpperBound( constraint_version ) ),
					"*" => SatisfiesRange( version, constraint_version, WildcardUpperBound( constraint_version ) ),
					">=" => version.CompareTo( constraint_version ) >= 0,
					"<=" => version.CompareTo( constraint_version ) <= 0,
					">" => version.CompareTo( constraint_version ) > 0,
					"<" => version.CompareTo( constraint_version ) < 0,
					"=" => SatisfiesEquals( version, constraint_version ),
					_ => false
				};

				if( !satisfied )
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Cargo only ever resolves to a prerelease when the requirement itself names one for the same major.minor.patch, so that "^1.0.0" never silently picks up
		/// "2.0.0-rc.1" while "^1.0.0-rc.1" can still match "1.0.0-rc.2".
		/// </summary>
		private bool AllowsPreRelease( CargoVersion version )
		{
			foreach( (string _, CargoVersion constraint_version) in Constraints )
			{
				if( constraint_version.IsPreRelease && constraint_version.CompareCore( version ) == 0 )
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// An "=" requirement that omits components pins only what it spells out, so "=1.2" accepts any 1.2.x.
		/// </summary>
		private static bool SatisfiesEquals( CargoVersion version, CargoVersion constraint )
		{
			if( constraint.SpecifiedParts == 3 )
			{
				return version.CompareTo( constraint ) == 0;
			}

			return SatisfiesRange( version, constraint, WildcardUpperBound( constraint ) );
		}

		private static bool SatisfiesRange( CargoVersion version, CargoVersion lowerBound, CargoVersion upperBound )
		{
			// The lower bound is inclusive and the upper bound is exclusive, which is how Cargo describes every one of its range operators
			return version.CompareTo( lowerBound ) >= 0 && version.CompareTo( upperBound ) < 0;
		}

		/// <summary>
		/// Works out the exclusive upper bound of a caret requirement. The bound increments the leftmost non-zero component, which makes "^1.2.3" mean "&lt;2.0.0" but
		/// "^0.2.3" mean "&lt;0.3.0" - a leading zero major version gets no compatibility guarantee beyond its minor version.
		/// </summary>
		private static CargoVersion CaretUpperBound( CargoVersion constraint )
		{
			if( constraint.Major != 0 )
			{
				return new CargoVersion( constraint.Major + 1, 0, 0 );
			}

			// "^0" is the whole of 0.x
			if( constraint.SpecifiedParts == 1 )
			{
				return new CargoVersion( 1, 0, 0 );
			}

			if( constraint.Minor != 0 )
			{
				return new CargoVersion( 0, constraint.Minor + 1, 0 );
			}

			// "^0.0" is the whole of 0.0.x
			if( constraint.SpecifiedParts == 2 )
			{
				return new CargoVersion( 0, 1, 0 );
			}

			// "^0.0.3" allows nothing but 0.0.3 itself
			return new CargoVersion( 0, 0, constraint.Patch + 1 );
		}

		/// <summary>
		/// Works out the exclusive upper bound of a tilde requirement, which permits patch level changes when a minor version is given and minor level changes when
		/// only a major version is given.
		/// </summary>
		private static CargoVersion TildeUpperBound( CargoVersion constraint )
		{
			if( constraint.SpecifiedParts == 1 )
			{
				return new CargoVersion( constraint.Major + 1, 0, 0 );
			}

			return new CargoVersion( constraint.Major, constraint.Minor + 1, 0 );
		}

		/// <summary>
		/// Works out the exclusive upper bound of a wildcard requirement from however much of the version was pinned before the "*".
		/// </summary>
		private static CargoVersion WildcardUpperBound( CargoVersion constraint )
		{
			if( constraint.SpecifiedParts == 1 )
			{
				return new CargoVersion( constraint.Major + 1, 0, 0 );
			}

			if( constraint.SpecifiedParts == 2 )
			{
				return new CargoVersion( constraint.Major, constraint.Minor + 1, 0 );
			}

			return new CargoVersion( constraint.Major, constraint.Minor, constraint.Patch + 1 );
		}
	}

	internal static class CargoVersionMatcher
	{
		/// <summary>
		/// Splits a Cargo requirement string, as found in a Cargo.toml dependency (e.g. "&gt;=1.2, &lt;1.5"), into its individual constraints.
		/// </summary>
		/// <param name="requirements">The comma separated requirement string.</param>
		/// <returns>The trimmed, non-empty constraints; empty if there are none.</returns>
		internal static string[] SplitRequirements( string requirements )
		{
			return requirements.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
		}

		/// <summary>
		/// Finds the highest version in an already retrieved list of a crate's versions that satisfies every one of the given constraints. Yanked versions are never
		/// selected, and prereleases are only selected when a constraint names a prerelease of the same release. The order of the list is irrelevant.
		/// </summary>
		/// <param name="crateName">The crate name, used for logging.</param>
		/// <param name="versionInfos">The published versions of the crate.</param>
		/// <param name="constraints">The version constraints, one operator-and-version string per element; none matches the highest stable version.</param>
		/// <returns>The highest matching version, or null if there are no selectable versions, none satisfy the constraints, or a constraint could not be parsed.</returns>
		internal static CargoVersion? SelectMatchingVersion( string crateName, List<CargoCrateVersionRaw> versionInfos, string[] constraints )
		{
			try
			{
				CargoRequirement requirement = new CargoRequirement( constraints );

				if( versionInfos.Count == 0 )
				{
					ConsoleLogger.Warning( $"Crate '{crateName}' has no available versions." );
					return null;
				}

				// A yanked version is still served by the registry but must never be resolved to, so it is dropped before anything else is considered
				List<CargoVersion> available_versions = new List<CargoVersion>();
				foreach( CargoCrateVersionRaw version_info in versionInfos )
				{
					if( version_info.yanked )
					{
						continue;
					}

					try
					{
						available_versions.Add( new CargoVersion( version_info.num ) );
					}
					catch( FormatException )
					{
						ConsoleLogger.Warning( $"Crate '{crateName}' has an unparsable version '{version_info.num}'; ignoring it." );
					}
				}

				if( available_versions.Count == 0 )
				{
					ConsoleLogger.Warning( $"Crate '{crateName}' has no selectable versions." );
					return null;
				}

				List<CargoVersion> matching_versions = available_versions
					.Where( x => requirement.SatisfiedBy( x ) )
					.ToList();

				CargoVersion? matching_version = matching_versions.Max();
				if( matching_version != null )
				{
					ConsoleLogger.Log( $"Crate '{crateName}' matches '{string.Join( ", ", constraints )}' with version: '{matching_version}'" );
				}
				else
				{
					ConsoleLogger.Warning( $"No version of '{crateName}' satisfies '{string.Join( ", ", constraints )}'." );
				}

				return matching_version;
			}
			catch( Exception e )
			{
				ConsoleLogger.Error( $"{e.GetType().Name} - {e.Message}" );
				return null;
			}
		}
	}
}
