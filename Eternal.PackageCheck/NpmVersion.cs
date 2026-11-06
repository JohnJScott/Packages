// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;
using System.Text.Json;

namespace Eternal.PackageCheck
{
	/// <summary>
	/// A single published npm version, e.g. "4.17.21" or "7.0.0-beta.3". npm versions are strict SemVer 2.0 - exactly three numeric parts, an optional
	/// prerelease label after a '-', and optional build metadata after a '+' that takes no part in ordering.
	/// </summary>
	internal class NpmVersion
		: IComparable<NpmVersion>
	{
		public int[] Parts { get; }
		private string[] PreReleaseParts { get; }
		private string PublishedVersion { get; }

		public bool IsPreRelease
		{
			get { return PreReleaseParts.Length > 0; }
		}

		public NpmVersion( string version )
		{
			PublishedVersion = version;

			// A leading 'v' is accepted by npm and is not part of the number.
			string trimmed_version = version.Trim();
			if( trimmed_version.StartsWith( "v", StringComparison.OrdinalIgnoreCase ) )
			{
				trimmed_version = trimmed_version[1..];
			}

			// Build metadata plays no part in ordering, so it is discarded before anything else is looked at.
			string without_metadata = trimmed_version.Split( '+' )[0];

			// Everything after the first '-' is the prerelease label, so "1.0.0-rc-1" has the single label "rc-1".
			int prerelease_index = without_metadata.IndexOf( '-' );
			string main_version = prerelease_index < 0 ? without_metadata : without_metadata[..prerelease_index];
			string prerelease = prerelease_index < 0 ? string.Empty : without_metadata[( prerelease_index + 1 )..];

			Parts = main_version.Split( '.' ).Select( int.Parse ).ToArray();
			PreReleaseParts = prerelease.Length == 0 ? [] : prerelease.Split( '.' );
		}

		/// <summary>
		/// Creates a version from a published version string, reporting failure rather than throwing when it is not a version npm could have published.
		/// </summary>
		/// <param name="version">The version string to parse.</param>
		/// <param name="npmVersion">The parsed version, or null if it could not be parsed.</param>
		/// <returns>True if the version was parsed.</returns>
		internal static bool TryParse( string version, out NpmVersion? npmVersion )
		{
			try
			{
				npmVersion = new NpmVersion( version );
				return true;
			}
			catch( Exception )
			{
				npmVersion = null;
				return false;
			}
		}

		public int CompareTo( NpmVersion? other )
		{
			if( other == null )
			{
				return 1;
			}

			// Missing trailing parts count as 0, so "1.0" and "1.0.0" are equal as they are in npm
			int max_length = Math.Max( Parts.Length, other.Parts.Length );
			for( int i = 0; i < max_length; i++ )
			{
				int comparison = PartAt( i ).CompareTo( other.PartAt( i ) );
				if( comparison != 0 )
				{
					return comparison;
				}
			}

			return ComparePreRelease( other );
		}

		/// <summary>
		/// Applies the SemVer 2.0 prerelease rules to two versions whose numeric parts are identical: a release outranks a prerelease, a numeric label ranks
		/// below an alphanumeric one, and a shorter run of labels ranks below a longer run that shares its prefix. Unlike NuGet, npm compares the
		/// alphanumeric labels case sensitively.
		/// </summary>
		/// <param name="other">The version to compare against.</param>
		/// <returns>Less than zero if this version is lower, zero if they rank equally, greater than zero if this version is higher.</returns>
		private int ComparePreRelease( NpmVersion other )
		{
			if( !IsPreRelease && !other.IsPreRelease )
			{
				return 0;
			}

			// "1.0.0" is a later version than "1.0.0-rc1".
			if( !IsPreRelease )
			{
				return 1;
			}

			if( !other.IsPreRelease )
			{
				return -1;
			}

			int max_length = Math.Max( PreReleaseParts.Length, other.PreReleaseParts.Length );
			for( int i = 0; i < max_length; i++ )
			{
				// "1.0.0-rc" is an earlier version than "1.0.0-rc.1".
				if( i >= PreReleaseParts.Length )
				{
					return -1;
				}

				if( i >= other.PreReleaseParts.Length )
				{
					return 1;
				}

				int comparison = ComparePreReleasePart( PreReleaseParts[i], other.PreReleaseParts[i] );
				if( comparison != 0 )
				{
					return comparison;
				}
			}

			return 0;
		}

		/// <summary>
		/// Ranks one dot separated prerelease label against another.
		/// </summary>
		/// <param name="left">The label from the first version.</param>
		/// <param name="right">The label from the second version.</param>
		/// <returns>Less than zero if the first label is lower, zero if they rank equally, greater than zero if the first label is higher.</returns>
		private static int ComparePreReleasePart( string left, string right )
		{
			bool left_is_numeric = int.TryParse( left, out int left_number );
			bool right_is_numeric = int.TryParse( right, out int right_number );

			if( left_is_numeric && right_is_numeric )
			{
				return left_number.CompareTo( right_number );
			}

			// A numeric label always ranks below an alphanumeric one, so "1.0.0-1" is earlier than "1.0.0-alpha".
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

		public int PartAt( int index )
		{
			return index < Parts.Length ? Parts[index] : 0;
		}

		public override string ToString()
		{
			return PublishedVersion;
		}
	}

	/// <summary>
	/// An npm version range, as found in a package's dependency map. npm's grammar is far richer than the single interval NuGet writes: "^1.2.3" and
	/// "~1.2.3" expand to a lower and an upper bound, "1.2.x" and "1" are wildcards, "1.2.3 - 2.3.4" is an inclusive pair, several comparators separated
	/// by spaces must all hold, and "||" offers alternatives of which only one need hold. Every form is reduced to alternative sets of the same
	/// operator-and-version constraints a GemRequirement holds, so that satisfying one set is the same test.
	/// </summary>
	internal class NpmRequirement
	{
		private readonly List<List<(string Operator, NpmVersion Version)>> ComparatorSets = new List<List<(string Operator, NpmVersion Version)>>();

		public NpmRequirement( string range )
		{
			ParseRange( range.Trim() );
		}

		private void ParseRange( string range )
		{
			// "||" offers alternatives, of which only one need be satisfied. A range with no alternatives still yields a single set, and a blank range
			// yields a single empty set, which accepts any version.
			foreach( string alternative in range.Split( "||" ) )
			{
				List<(string Operator, NpmVersion Version)> constraints = new List<(string Operator, NpmVersion Version)>();
				ParseComparatorSet( alternative.Trim(), constraints );
				ComparatorSets.Add( constraints );
			}
		}

		private void ParseComparatorSet( string comparatorSet, List<(string Operator, NpmVersion Version)> constraints )
		{
			// "1.2.3 - 2.3.4" is an inclusive pair rather than two comparators, so it is recognised before anything is tokenized.
			int hyphen_index = comparatorSet.IndexOf( " - ", StringComparison.Ordinal );
			if( hyphen_index >= 0 )
			{
				AddHyphenRange( comparatorSet[..hyphen_index].Trim(), comparatorSet[( hyphen_index + 3 )..].Trim(), constraints );
				return;
			}

			int index = 0;
			while( index < comparatorSet.Length )
			{
				// Comparators are separated by whitespace; a comma is tolerated even though npm does not write one.
				while( index < comparatorSet.Length && ( char.IsWhiteSpace( comparatorSet[index] ) || comparatorSet[index] == ',' ) )
				{
					index++;
				}

				if( index >= comparatorSet.Length )
				{
					return;
				}

				string comparator_operator = ReadOperator( comparatorSet, ref index );

				// npm tolerates whitespace between the operator and the version, as in ">= 1.0.0".
				while( index < comparatorSet.Length && char.IsWhiteSpace( comparatorSet[index] ) )
				{
					index++;
				}

				int version_start = index;
				while( index < comparatorSet.Length && !char.IsWhiteSpace( comparatorSet[index] ) && comparatorSet[index] != ',' )
				{
					index++;
				}

				AddComparator( comparator_operator, comparatorSet[version_start..index], constraints );
			}
		}

		/// <summary>
		/// Reads the operator a comparator begins with, advancing past it.
		/// </summary>
		/// <param name="comparatorSet">The text being tokenized.</param>
		/// <param name="index">The position to read from, advanced past the operator.</param>
		/// <returns>The operator, or "" when the comparator is a bare version.</returns>
		private static string ReadOperator( string comparatorSet, ref int index )
		{
			char first_character = comparatorSet[index];

			if( first_character == '>' || first_character == '<' )
			{
				index++;
				if( index < comparatorSet.Length && comparatorSet[index] == '=' )
				{
					index++;
					return first_character == '>' ? ">=" : "<=";
				}

				return first_character == '>' ? ">" : "<";
			}

			if( first_character == '^' || first_character == '=' )
			{
				index++;
				return first_character.ToString();
			}

			if( first_character == '~' )
			{
				index++;

				// "~>1.2" is accepted by npm as a synonym for "~1.2".
				if( index < comparatorSet.Length && comparatorSet[index] == '>' )
				{
					index++;
				}

				return "~";
			}

			return string.Empty;
		}

		/// <summary>
		/// Splits a comparator's version into its numeric parts, treating 'x', 'X' and '*' as unspecified.
		/// </summary>
		/// <param name="version">The version text from a comparator, e.g. "1.2.3", "1.2.x" or "*".</param>
		/// <param name="parts">The three numeric parts, with any unspecified part left as 0.</param>
		/// <param name="prerelease">The prerelease label, or "" if there is none.</param>
		/// <returns>How many leading parts were given, from 0 for a bare wildcard to 3 for a complete version.</returns>
		private static int ParsePartialVersion( string version, out int[] parts, out string prerelease )
		{
			parts = [0, 0, 0];
			prerelease = string.Empty;

			string trimmed_version = version.Trim();
			if( trimmed_version.StartsWith( "v", StringComparison.OrdinalIgnoreCase ) )
			{
				trimmed_version = trimmed_version[1..];
			}

			// Build metadata is not part of the number and takes no part in a comparison.
			trimmed_version = trimmed_version.Split( '+' )[0];

			int prerelease_index = trimmed_version.IndexOf( '-' );
			if( prerelease_index >= 0 )
			{
				prerelease = trimmed_version[( prerelease_index + 1 )..];
				trimmed_version = trimmed_version[..prerelease_index];
			}

			if( trimmed_version.Length == 0 )
			{
				return 0;
			}

			string[] numbers = trimmed_version.Split( '.' );
			int specified_parts = 0;
			for( int i = 0; i < numbers.Length && i < 3; i++ )
			{
				string number = numbers[i].Trim();
				if( number.Length == 0 || number == "*" || number.Equals( "x", StringComparison.OrdinalIgnoreCase ) )
				{
					// Everything from the first wildcard onwards is unspecified.
					return specified_parts;
				}

				parts[i] = int.Parse( number );
				specified_parts++;
			}

			return specified_parts;
		}

		private static NpmVersion MakeVersion( int major, int minor, int patch )
		{
			return new NpmVersion( $"{major}.{minor}.{patch}" );
		}

		private static NpmVersion MakeVersion( int[] parts, string prerelease )
		{
			string version = $"{parts[0]}.{parts[1]}.{parts[2]}";
			return new NpmVersion( prerelease.Length == 0 ? version : version + "-" + prerelease );
		}

		/// <summary>
		/// The first version above a partially specified one, e.g. "1.2" excludes 1.3.0 and above, and "1" excludes 2.0.0 and above.
		/// </summary>
		/// <param name="parts">The numeric parts of the comparator's version.</param>
		/// <param name="specifiedParts">How many of those parts were given.</param>
		/// <returns>The exclusive upper bound, or null if nothing was specified and every version qualifies.</returns>
		private static NpmVersion? UpperBoundOfPartial( int[] parts, int specifiedParts )
		{
			if( specifiedParts == 1 )
			{
				return MakeVersion( parts[0] + 1, 0, 0 );
			}

			if( specifiedParts == 2 )
			{
				return MakeVersion( parts[0], parts[1] + 1, 0 );
			}

			return null;
		}

		/// <summary>
		/// The exclusive upper bound of a caret comparator, which allows changes that do not alter the leftmost non-zero part: "^1.2.3" stops below 2.0.0,
		/// "^0.2.3" below 0.3.0, and "^0.0.3" below 0.0.4.
		/// </summary>
		/// <param name="parts">The numeric parts of the comparator's version.</param>
		/// <param name="specifiedParts">How many of those parts were given.</param>
		/// <returns>The exclusive upper bound.</returns>
		private static NpmVersion CaretUpperBound( int[] parts, int specifiedParts )
		{
			if( parts[0] != 0 )
			{
				return MakeVersion( parts[0] + 1, 0, 0 );
			}

			if( specifiedParts >= 2 && parts[1] != 0 )
			{
				return MakeVersion( 0, parts[1] + 1, 0 );
			}

			if( specifiedParts == 3 )
			{
				return MakeVersion( 0, parts[1], parts[2] + 1 );
			}

			if( specifiedParts == 2 )
			{
				return MakeVersion( 0, parts[1] + 1, 0 );
			}

			return MakeVersion( 1, 0, 0 );
		}

		/// <summary>
		/// The exclusive upper bound of a tilde comparator, which allows patch level changes when a minor version is given and minor level changes when it
		/// is not: "~1.2.3" and "~1.2" both stop below 1.3.0, while "~1" stops below 2.0.0.
		/// </summary>
		/// <param name="parts">The numeric parts of the comparator's version.</param>
		/// <param name="specifiedParts">How many of those parts were given.</param>
		/// <returns>The exclusive upper bound.</returns>
		private static NpmVersion TildeUpperBound( int[] parts, int specifiedParts )
		{
			if( specifiedParts >= 2 )
			{
				return MakeVersion( parts[0], parts[1] + 1, 0 );
			}

			return MakeVersion( parts[0] + 1, 0, 0 );
		}

		private static void AddHyphenRange( string lowerText, string upperText, List<(string Operator, NpmVersion Version)> constraints )
		{
			int lower_specified = ParsePartialVersion( lowerText, out int[] lower_parts, out string lower_prerelease );
			if( lower_specified > 0 )
			{
				constraints.Add( (">=", MakeVersion( lower_parts, lower_prerelease )) );
			}

			int upper_specified = ParsePartialVersion( upperText, out int[] upper_parts, out string upper_prerelease );
			if( upper_specified == 0 )
			{
				return;
			}

			// A partial upper bound takes in the whole of that release line, so "1.2.3 - 2.3" stops below 2.4.0 rather than at 2.3.0.
			NpmVersion? upper_bound = UpperBoundOfPartial( upper_parts, upper_specified );
			if( upper_bound != null )
			{
				constraints.Add( ("<", upper_bound) );
			}
			else
			{
				constraints.Add( ("<=", MakeVersion( upper_parts, upper_prerelease )) );
			}
		}

		private static void AddComparator( string comparatorOperator, string versionText, List<(string Operator, NpmVersion Version)> constraints )
		{
			if( versionText.Trim().Length == 0 )
			{
				return;
			}

			int specified_parts = ParsePartialVersion( versionText, out int[] parts, out string prerelease );

			// "*", "x" and "" accept any version, whatever operator was written in front of them.
			if( specified_parts == 0 )
			{
				return;
			}

			NpmVersion lower_bound = MakeVersion( parts, prerelease );

			if( comparatorOperator == "^" )
			{
				constraints.Add( (">=", lower_bound) );
				constraints.Add( ("<", CaretUpperBound( parts, specified_parts )) );
				return;
			}

			if( comparatorOperator == "~" )
			{
				constraints.Add( (">=", lower_bound) );
				constraints.Add( ("<", TildeUpperBound( parts, specified_parts )) );
				return;
			}

			// A partially specified version stands for the whole of its release line, which shifts where the bounds of an inequality fall.
			NpmVersion? partial_upper_bound = UpperBoundOfPartial( parts, specified_parts );

			switch( comparatorOperator )
			{
				case ">":
					constraints.Add( partial_upper_bound != null ? (">=", partial_upper_bound) : (">", lower_bound) );
					break;

				case ">=":
					constraints.Add( (">=", lower_bound) );
					break;

				case "<":
					constraints.Add( ("<", lower_bound) );
					break;

				case "<=":
					constraints.Add( partial_upper_bound != null ? ("<", partial_upper_bound) : ("<=", lower_bound) );
					break;

				default:
					if( partial_upper_bound != null )
					{
						constraints.Add( (">=", lower_bound) );
						constraints.Add( ("<", partial_upper_bound) );
					}
					else
					{
						constraints.Add( ("=", lower_bound) );
					}

					break;
			}
		}

		private static bool SetSatisfiedBy( List<(string Operator, NpmVersion Version)> constraints, NpmVersion version )
		{
			foreach( (string op, NpmVersion constraint_version) in constraints )
			{
				bool satisfied = op switch
				{
					">=" => version.CompareTo( constraint_version ) >= 0,
					"<=" => version.CompareTo( constraint_version ) <= 0,
					">" => version.CompareTo( constraint_version ) > 0,
					"<" => version.CompareTo( constraint_version ) < 0,
					"=" => version.CompareTo( constraint_version ) == 0,
					_ => false
				};

				if( !satisfied )
				{
					return false;
				}
			}

			return true;
		}

		public bool SatisfiedBy( NpmVersion version )
		{
			// Only one of the "||" alternatives need hold, but every comparator within that alternative must.
			foreach( List<(string Operator, NpmVersion Version)> comparator_set in ComparatorSets )
			{
				if( SetSatisfiedBy( comparator_set, version ) )
				{
					return true;
				}
			}

			return false;
		}
	}

	internal static class NpmVersionMatcher
	{
		private static List<string>? GetAllVersions( string packageName )
		{
			string get_request = $"https://registry.npmjs.org/{packageName}";
			string content = PackageCheck.Request( get_request ).Result;
			if( string.IsNullOrEmpty( content ) )
			{
				ConsoleLogger.Error( $"Failed to retrieve versions for package '{packageName}' from npmjs.org" );
				return null;
			}

			// NpmPackageRaw carries System.Text.Json converters for npm's polymorphic "author", "license" and "repository" fields, so it must be
			// deserialized with System.Text.Json rather than the Newtonsoft based JsonHelper, which would ignore them and fail on the object forms.
			NpmPackageRaw? package;
			try
			{
				package = JsonSerializer.Deserialize<NpmPackageRaw>( content );
			}
			catch( JsonException json_exception )
			{
				ConsoleLogger.Error( $"Exception during deserialization of npm package '{packageName}' with exception '{json_exception.Message}'" );
				return null;
			}

			return package?.versions?.Keys.ToList();
		}

		/// <summary>
		/// Finds the highest stable (non-prerelease) version of a package that satisfies an npm version range, e.g. "^4.17.0", "~1.2.3", "&gt;=2 &lt;4".
		/// </summary>
		/// <param name="packageName">The npm package name, e.g. "lodash".</param>
		/// <param name="range">The version range in npm's semver notation; blank matches every version.</param>
		/// <returns>The highest matching version, or null if the package has no stable versions or none satisfy the range.</returns>
		public static NpmVersion? FindMatchingVersion( string packageName, string range )
		{
			List<string>? versions = GetAllVersions( packageName );
			if( versions == null )
			{
				ConsoleLogger.Warning( $"Package '{packageName}' has no available versions." );
				return null;
			}

			return SelectMatchingVersion( packageName, versions, range );
		}

		/// <summary>
		/// Finds the highest stable (non-prerelease) version in an already retrieved list of a package's versions that satisfies the given range. The order
		/// of the list is irrelevant.
		/// </summary>
		/// <param name="packageName">The npm package name, used for logging.</param>
		/// <param name="versions">The published versions of the package.</param>
		/// <param name="range">The version range in npm's semver notation; blank matches every version.</param>
		/// <returns>The highest matching version, or null if there are no stable versions, none satisfy the range, or the range could not be parsed.</returns>
		internal static NpmVersion? SelectMatchingVersion( string packageName, List<string> versions, string range )
		{
			try
			{
				// Create requirement from the range
				NpmRequirement requirement = new NpmRequirement( range );

				if( versions.Count == 0 )
				{
					ConsoleLogger.Warning( $"Package '{packageName}' has no available versions." );
					return null;
				}

				// Convert to NpmVersion objects and filter prereleases. A version that cannot be parsed is skipped rather than abandoning the whole
				// selection, as one malformed entry would otherwise hide every good one.
				List<NpmVersion> available_versions = new List<NpmVersion>();
				foreach( string version in versions )
				{
					if( !NpmVersion.TryParse( version, out NpmVersion? parsed_version ) || parsed_version == null )
					{
						ConsoleLogger.Warning( $"Could not parse version '{version}' of '{packageName}'; ignoring it." );
						continue;
					}

					if( parsed_version.IsPreRelease )
					{
						continue;
					}

					available_versions.Add( parsed_version );
				}

				if( available_versions.Count == 0 )
				{
					ConsoleLogger.Warning( $"Package '{packageName}' has no stable versions." );
					return null;
				}

				// Find matching versions
				List<NpmVersion> matching_versions = available_versions
					.Where( v => requirement.SatisfiedBy( v ) )
					.ToList();

				// Get the highest matching version
				NpmVersion? matching_version = matching_versions.Max();
				if( matching_version != null )
				{
					ConsoleLogger.Log( $"Package '{packageName}' matches '{range}' with version: '{matching_version}'" );
				}
				else
				{
					ConsoleLogger.Warning( $"No version of '{packageName}' satisfies '{range}'." );
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
