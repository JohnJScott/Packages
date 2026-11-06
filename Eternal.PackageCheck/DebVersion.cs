// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;

namespace Eternal.PackageCheck
{
	/// <summary>
	/// A Debian package version, "[epoch:]upstream_version[-debian_revision]", ordered as dpkg orders it. See
	/// https://www.debian.org/doc/debian-policy/ch-controlfields.html#version for the format and the comparison algorithm.
	/// </summary>
	internal class DebVersion
		: IComparable<DebVersion>
	{
		/// <summary>The epoch, which outranks everything else and is 0 when omitted.</summary>
		public int Epoch { get; }

		/// <summary>The upstream version, e.g. "8.14.1" or "8.23.0~rc3".</summary>
		public string UpstreamVersion { get; }

		/// <summary>The Debian revision, e.g. "2+deb13u5"; empty for a native package.</summary>
		public string DebianRevision { get; }

		private string OriginalVersion { get; }

		/// <summary>
		/// Parses a Debian version.
		/// </summary>
		/// <param name="version">The version text, e.g. "8.14.1-2+deb13u5" or "1:2.3-4".</param>
		/// <exception cref="FormatException">The text is empty, the epoch is not a number, or there is no upstream version.</exception>
		public DebVersion( string version )
		{
			OriginalVersion = version;

			string remainder = version.Trim();
			if( remainder.Length == 0 )
			{
				throw new FormatException( "An empty string is not a valid version" );
			}

			// The epoch is everything before the first colon, as the upstream version may only contain a colon when there is an epoch
			int epoch_index = remainder.IndexOf( ':' );
			if( epoch_index >= 0 )
			{
				if( !int.TryParse( remainder[..epoch_index], out int epoch ) || epoch < 0 )
				{
					throw new FormatException( $"'{version}' has a non-numeric epoch" );
				}

				Epoch = epoch;
				remainder = remainder[( epoch_index + 1 )..];
			}

			// The revision is everything after the last hyphen, as the upstream version may only contain a hyphen when there is a revision
			int revision_index = remainder.LastIndexOf( '-' );
			if( revision_index >= 0 )
			{
				DebianRevision = remainder[( revision_index + 1 )..];
				remainder = remainder[..revision_index];
			}
			else
			{
				DebianRevision = string.Empty;
			}

			if( remainder.Length == 0 )
			{
				throw new FormatException( $"'{version}' has no upstream version" );
			}

			UpstreamVersion = remainder;
		}

		/// <summary>
		/// Orders two versions as dpkg does: the epoch numerically, then the upstream version, then the Debian revision.
		/// </summary>
		public int CompareTo( DebVersion? other )
		{
			if( other == null )
			{
				return 1;
			}

			int comparison = Epoch.CompareTo( other.Epoch );
			if( comparison != 0 )
			{
				return comparison;
			}

			comparison = CompareFragment( UpstreamVersion, other.UpstreamVersion );
			if( comparison != 0 )
			{
				return comparison;
			}

			return CompareFragment( DebianRevision, other.DebianRevision );
		}

		/// <summary>
		/// The weight dpkg gives a character in the non-digit part of a version. A tilde sorts before anything, even the end of the string, which is what makes
		/// "1.0~rc1" lower than "1.0". Letters sort before every other character.
		/// </summary>
		private static int CharacterOrder( char character )
		{
			if( char.IsAsciiDigit( character ) )
			{
				return 0;
			}

			if( char.IsAsciiLetter( character ) )
			{
				return character;
			}

			if( character == '~' )
			{
				return -1;
			}

			return character + 256;
		}

		/// <summary>
		/// Compares an upstream version or a revision the way dpkg's verrevcmp does: alternately compare a run of non-digits character by character, then a run of
		/// digits numerically, until one side runs out.
		/// </summary>
		private static int CompareFragment( string left, string right )
		{
			int left_index = 0;
			int right_index = 0;

			while( left_index < left.Length || right_index < right.Length )
			{
				while( ( left_index < left.Length && !char.IsAsciiDigit( left[left_index] ) ) || ( right_index < right.Length && !char.IsAsciiDigit( right[right_index] ) ) )
				{
					// The end of the string weighs the same as a digit, which is 0
					int left_order = left_index < left.Length ? CharacterOrder( left[left_index] ) : 0;
					int right_order = right_index < right.Length ? CharacterOrder( right[right_index] ) : 0;
					if( left_order != right_order )
					{
						return Math.Sign( left_order - right_order );
					}

					left_index++;
					right_index++;
				}

				while( left_index < left.Length && left[left_index] == '0' )
				{
					left_index++;
				}

				while( right_index < right.Length && right[right_index] == '0' )
				{
					right_index++;
				}

				// With the leading zeros gone, the longer run of digits is the larger number, and the first differing digit decides between runs of equal length
				int first_difference = 0;
				while( left_index < left.Length && char.IsAsciiDigit( left[left_index] ) && right_index < right.Length && char.IsAsciiDigit( right[right_index] ) )
				{
					if( first_difference == 0 )
					{
						first_difference = left[left_index] - right[right_index];
					}

					left_index++;
					right_index++;
				}

				if( left_index < left.Length && char.IsAsciiDigit( left[left_index] ) )
				{
					return 1;
				}

				if( right_index < right.Length && char.IsAsciiDigit( right[right_index] ) )
				{
					return -1;
				}

				if( first_difference != 0 )
				{
					return Math.Sign( first_difference );
				}
			}

			return 0;
		}

		public override string ToString()
		{
			return OriginalVersion;
		}
	}

	/// <summary>
	/// A single Debian version relation, as found in a Depends or Build-Depends field, e.g. "&gt;= 1.22.5". A version with no operator must match exactly.
	/// </summary>
	internal class DebRequirement
	{
		/// <summary>The relational operators, longest first so that "&gt;=" is not read as the deprecated "&gt;".</summary>
		private static readonly string[] Operators = [">=", "<=", ">>", "<<", "=", ">", "<"];

		private readonly string Operator = string.Empty;
		private readonly DebVersion? Version = null;

		/// <param name="requirement">The relation, e.g. "&gt;= 1.22.5", "8.14.1-2" for an exact match, or "" to match every version.</param>
		/// <exception cref="FormatException">The version in the relation could not be parsed.</exception>
		public DebRequirement( string requirement )
		{
			string trimmed_requirement = requirement.Trim();
			if( trimmed_requirement.Length == 0 )
			{
				return;
			}

			Operator = "=";
			foreach( string op in Operators )
			{
				if( trimmed_requirement.StartsWith( op ) )
				{
					Operator = op;
					trimmed_requirement = trimmed_requirement[op.Length..].Trim();
					break;
				}
			}

			Version = new DebVersion( trimmed_requirement );
		}

		/// <summary>
		/// Determines whether a version satisfies this relation.
		/// </summary>
		/// <param name="version">The version to test.</param>
		/// <returns>true if the version satisfies the relation, or if there is no relation.</returns>
		public bool SatisfiedBy( DebVersion version )
		{
			if( Version == null )
			{
				return true;
			}

			int comparison = version.CompareTo( Version );
			return Operator switch
			{
				"=" => comparison == 0,
				">>" => comparison > 0,
				"<<" => comparison < 0,
				// The deprecated single character forms mean "or equal", not "strictly"
				">=" or ">" => comparison >= 0,
				"<=" or "<" => comparison <= 0,
				_ => false
			};
		}
	}

	internal static class DebVersionMatcher
	{
		/// <summary>
		/// Finds the highest version in a list of a package's versions that satisfies a relation. The order of the list is irrelevant.
		/// </summary>
		/// <param name="packageName">The package name, used for logging.</param>
		/// <param name="versions">The published versions of the package.</param>
		/// <param name="requirement">The relation, e.g. "&gt;= 1.22.5", an exact version, or "" for the highest version.</param>
		/// <returns>The highest matching version, or null if there are no versions, none satisfy the relation, or the relation could not be parsed.</returns>
		internal static DebVersion? SelectMatchingVersion( string packageName, List<string> versions, string requirement )
		{
			try
			{
				DebRequirement deb_requirement = new DebRequirement( requirement );

				List<DebVersion> available_versions = new List<DebVersion>();
				foreach( string version in versions )
				{
					try
					{
						available_versions.Add( new DebVersion( version ) );
					}
					catch( FormatException )
					{
						ConsoleLogger.Warning( $"Package '{packageName}' has an unparsable version '{version}'; ignoring it." );
					}
				}

				if( available_versions.Count == 0 )
				{
					ConsoleLogger.Warning( $"Package '{packageName}' has no selectable versions." );
					return null;
				}

				DebVersion? matching_version = available_versions
					.Where( x => deb_requirement.SatisfiedBy( x ) )
					.Max();

				if( matching_version != null )
				{
					ConsoleLogger.Log( $"Package '{packageName}' matches '{requirement}' with version: '{matching_version}'" );
				}
				else
				{
					ConsoleLogger.Warning( $"No version of '{packageName}' satisfies '{requirement}'." );
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
