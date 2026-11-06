// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;

namespace Eternal.PackageCheck
{
	internal class GemVersion
		: IComparable<GemVersion>
	{
		public int[] Parts { get; }
		private string ConstrainedVersion { get; }

		public GemVersion( string version )
		{
			ConstrainedVersion = version;

			// Split on '-' to handle prerelease versions
			string main_version = version.Split( '-' )[0];
			Parts = main_version.Split( '.' ).Select( int.Parse ).ToArray();
		}

		public int CompareTo( GemVersion? other )
		{
			if( other == null )
			{
				return 1;
			}

			// Missing trailing parts count as 0, so "1.0" and "1.0.0" are equal as they are in RubyGems
			int max_length = Math.Max( Parts.Length, other.Parts.Length );
			for( int i = 0; i < max_length; i++ )
			{
				int comparison = PartAt( i ).CompareTo( other.PartAt( i ) );
				if( comparison != 0 )
				{
					return comparison;
				}
			}

			return 0;
		}

		public int PartAt( int index )
		{
			return index < Parts.Length ? Parts[index] : 0;
		}

		public override string ToString()
		{
			return ConstrainedVersion;
		}
	}

	internal class GemRequirement
	{
		private readonly List<(string Operator, GemVersion Version)> Constraints = new List<(string Operator, GemVersion Version)>();

		public GemRequirement( string[] constraints )
		{
			foreach( string constraint in constraints )
			{
				ParseConstraint( constraint.Trim() );
			}
		}

		private void ParseConstraint( string constraint )
		{
			string op;
			string version_str;

			if( constraint.StartsWith( "~>" ) )
			{
				op = "~>";
				version_str = constraint[2..].Trim();
			}
			else if( constraint.StartsWith( ">=" ) )
			{
				op = ">=";
				version_str = constraint[2..].Trim();
			}
			else if( constraint.StartsWith( "<=" ) )
			{
				op = "<=";
				version_str = constraint[2..].Trim();
			}
			else if( constraint.StartsWith( "!=" ) )
			{
				op = "!=";
				version_str = constraint[2..].Trim();
			}
			else if( constraint.StartsWith( ">" ) )
			{
				op = ">";
				version_str = constraint[1..].Trim();
			}
			else if( constraint.StartsWith( "<" ) )
			{
				op = "<";
				version_str = constraint[1..].Trim();
			}
			else if( constraint.StartsWith( "=" ) )
			{
				op = "=";
				version_str = constraint[1..].Trim();
			}
			else
			{
				op = "=";
				version_str = constraint;
			}

			Constraints.Add( (op, new GemVersion( version_str )) );
		}

		public bool SatisfiedBy( GemVersion version )
		{
			foreach( (string op, GemVersion constraint_version) in Constraints )
			{
				bool satisfied = op switch
				{
					"~>" => SatisfiesPessimistic( version, constraint_version ),
					">=" => version.CompareTo( constraint_version ) >= 0,
					"<=" => version.CompareTo( constraint_version ) <= 0,
					">" => version.CompareTo( constraint_version ) > 0,
					"<" => version.CompareTo( constraint_version ) < 0,
					"=" => version.CompareTo( constraint_version ) == 0,
					"!=" => version.CompareTo( constraint_version ) != 0,
					_ => false
				};

				if( !satisfied )
				{
					return false;
				}
			}

			return true;
		}

		private bool SatisfiesPessimistic( GemVersion version, GemVersion constraint )
		{
			// ~> 1 means >= 1 and < 2
			// ~> 13.0 means >= 13.0 and < 14.0
			// ~> 13.0.5 means >= 13.0.5 and < 13.1
			if( version.CompareTo( constraint ) < 0 )
			{
				return false;
			}

			int lock_length = Math.Max( 1, constraint.Parts.Length - 1 );
			for( int i = 0; i < lock_length; i++ )
			{
				if( version.PartAt( i ) != constraint.PartAt( i ) )
				{
					return false;
				}
			}

			return true;
		}
	}

	internal static class RubyGemVersionMatcher
	{
		private static List<RubyGemVersionRaw>? GetAllVersions( string gemName )
		{
			string get_request = $"https://rubygems.org/api/v1/versions/{gemName}.json";
			string content = PackageCheck.Request( get_request ).Result;
			if( string.IsNullOrEmpty( content ) )
			{
				ConsoleLogger.Error( $"Failed to retrieve versions for gem '{gemName}' from RubyGems.org" );
				return null;
			}

			return JsonHelper.ReadJson<List<RubyGemVersionRaw>>( content );
		}

		/// <summary>
		/// Splits a RubyGems requirement string, as found in a gem's dependency list (e.g. ">= 1.6.0, &lt; 4"), into its individual constraints.
		/// </summary>
		/// <param name="requirements">The comma separated requirement string.</param>
		/// <returns>The trimmed, non-empty constraints; empty if there are none.</returns>
		internal static string[] SplitRequirements( string requirements )
		{
			return requirements.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
		}

		/// <summary>
		/// Finds the highest stable (non-prerelease) version of a gem that satisfies every one of the given RubyGems version constraints, e.g. "~> 1.2", ">= 0".
		/// </summary>
		/// <param name="gemName">The gem name, e.g. "rails".</param>
		/// <param name="constraints">The version constraints, one operator-and-version string per element.</param>
		/// <returns>The highest matching version, or null if the gem has no stable versions or none satisfy the constraints.</returns>
		public static GemVersion? FindMatchingVersion( string gemName, params string[] constraints )
		{
			// Get all versions from RubyGems.org
			List<RubyGemVersionRaw>? version_infos = GetAllVersions( gemName );
			if( version_infos == null )
			{
				ConsoleLogger.Warning( $"Gem '{gemName}' has no available versions." );
				return null;
			}

			return SelectMatchingVersion( gemName, version_infos, constraints );
		}

		/// <summary>
		/// Finds the highest stable (non-prerelease) version in an already retrieved list of a gem's versions that satisfies every one of the given constraints. The order of
		/// the list is irrelevant, and the platform of each entry is not considered.
		/// </summary>
		/// <param name="gemName">The gem name, used for logging.</param>
		/// <param name="versionInfos">The published versions of the gem.</param>
		/// <param name="constraints">The version constraints, one operator-and-version string per element; none matches every version.</param>
		/// <returns>The highest matching version, or null if there are no stable versions, none satisfy the constraints, or a constraint or version could not be parsed.</returns>
		internal static GemVersion? SelectMatchingVersion( string gemName, List<RubyGemVersionRaw> versionInfos, string[] constraints )
		{
			try
			{
				// Create requirement from constraints
				GemRequirement requirement = new GemRequirement( constraints );

				if( versionInfos.Count == 0 )
				{
					ConsoleLogger.Warning( $"Gem '{gemName}' has no available versions." );
					return null;
				}

				// Convert to GemVersion objects and filter prereleases
				List<GemVersion> available_versions = versionInfos
					.Where( v => !v.prerelease )
					.Select( v => new GemVersion( v.number ) )
					.ToList();

				if( available_versions.Count == 0 )
				{
					ConsoleLogger.Warning( $"Gem '{gemName}' has no stable versions." );
					return null;
				}

				// Find matching versions
				List<GemVersion> matching_versions = available_versions
					.Where( v => requirement.SatisfiedBy( v ) )
					.ToList();

				// Get the highest matching version
				GemVersion? matching_version = matching_versions.Max();
				if( matching_version != null )
				{
					ConsoleLogger.Log( $"Gem '{gemName}' matches '{string.Join( ", ", constraints )}' with version: '{matching_version}'" );
				}
				else
				{
					ConsoleLogger.Warning( $"No version of '{gemName}' satisfies '{string.Join( ", ", constraints )}'." );
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
