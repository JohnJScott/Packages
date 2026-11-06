// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;

namespace Eternal.PackageCheck
{
	/// <summary>
	/// A single published NuGet version, e.g. "13.0.4", "4.5.0.1" or "8.0.0-preview.2". NuGet versions are SemVer 2.0 with an optional fourth numeric part;
	/// build metadata (everything after a '+') takes no part in ordering, exactly as it does not for NuGet itself.
	/// </summary>
	internal class NuGetVersion
		: IComparable<NuGetVersion>
	{
		public int[] Parts { get; }
		private string[] PreReleaseParts { get; }
		private string PublishedVersion { get; }

		public bool IsPreRelease
		{
			get { return PreReleaseParts.Length > 0; }
		}

		public NuGetVersion( string version )
		{
			PublishedVersion = version;

			// Build metadata plays no part in ordering, so it is discarded before anything else is looked at.
			string without_metadata = version.Split( '+' )[0];

			// Everything after the first '-' is the prerelease label, so "1.0.0-rc-1" has the single label "rc-1".
			int prerelease_index = without_metadata.IndexOf( '-' );
			string main_version = prerelease_index < 0 ? without_metadata : without_metadata[..prerelease_index];
			string prerelease = prerelease_index < 0 ? string.Empty : without_metadata[( prerelease_index + 1 )..];

			Parts = main_version.Split( '.' ).Select( int.Parse ).ToArray();
			PreReleaseParts = prerelease.Length == 0 ? [] : prerelease.Split( '.' );
		}

		/// <summary>
		/// Creates a version from a published version string, reporting failure rather than throwing when it is not a version NuGet could have published.
		/// </summary>
		/// <param name="version">The version string to parse.</param>
		/// <param name="nugetVersion">The parsed version, or null if it could not be parsed.</param>
		/// <returns>True if the version was parsed.</returns>
		internal static bool TryParse( string version, out NuGetVersion? nugetVersion )
		{
			try
			{
				nugetVersion = new NuGetVersion( version );
				return true;
			}
			catch( Exception )
			{
				nugetVersion = null;
				return false;
			}
		}

		public int CompareTo( NuGetVersion? other )
		{
			if( other == null )
			{
				return 1;
			}

			// Missing trailing parts count as 0, so "1.0" and "1.0.0" are equal as they are in NuGet
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
		/// below an alphanumeric one, and a shorter run of labels ranks below a longer run that shares its prefix. NuGet compares the alphanumeric labels
		/// without regard to case.
		/// </summary>
		/// <param name="other">The version to compare against.</param>
		/// <returns>Less than zero if this version is lower, zero if they rank equally, greater than zero if this version is higher.</returns>
		private int ComparePreRelease( NuGetVersion other )
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

			return string.Compare( left, right, StringComparison.OrdinalIgnoreCase );
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
	/// A NuGet version range, as found in a package's dependency list. NuGet writes a single interval rather than the comma separated list of operators
	/// RubyGems uses, and the bare form is a minimum rather than an exact match: "1.0" means "&gt;= 1.0", "[1.0]" pins 1.0 exactly, "[1.0, 2.0)" accepts
	/// 1.0 but not 2.0, "(, 2.0]" sets only an upper bound, and "(, )" accepts anything. The interval is reduced to the same operator-and-version
	/// constraints a GemRequirement holds, so that satisfying it is the same test.
	/// </summary>
	internal class NuGetRequirement
	{
		private readonly List<(string Operator, NuGetVersion Version)> Constraints = new List<(string Operator, NuGetVersion Version)>();

		public NuGetRequirement( string range )
		{
			ParseRange( range.Trim() );
		}

		private void ParseRange( string range )
		{
			// An absent range accepts any version.
			if( range.Length == 0 )
			{
				return;
			}

			// A bare version is the minimum acceptable version in NuGet, not an exact match.
			if( range[0] != '[' && range[0] != '(' )
			{
				Constraints.Add( (">=", new NuGetVersion( range )) );
				return;
			}

			char opening_bracket = range[0];
			char closing_bracket = range[^1];
			if( closing_bracket != ']' && closing_bracket != ')' )
			{
				ConsoleLogger.Warning( $"Malformed version range '{range}'; accepting any version." );
				return;
			}

			string[] bounds = range[1..^1].Split( ',' );

			// A single bound names one version, which only "[1.0]" may do; "(1.0)" is not legal NuGet.
			if( bounds.Length == 1 )
			{
				string exact_bound = bounds[0].Trim();
				if( opening_bracket == '[' && closing_bracket == ']' && exact_bound.Length > 0 )
				{
					Constraints.Add( ("=", new NuGetVersion( exact_bound )) );
				}
				else
				{
					ConsoleLogger.Warning( $"Malformed version range '{range}'; accepting any version." );
				}

				return;
			}

			if( bounds.Length > 2 )
			{
				ConsoleLogger.Warning( $"Malformed version range '{range}'; accepting any version." );
				return;
			}

			// Either bound may be left out, as "[1.0, )" and "(, 2.0]" do.
			string minimum_bound = bounds[0].Trim();
			if( minimum_bound.Length > 0 )
			{
				Constraints.Add( (opening_bracket == '[' ? ">=" : ">", new NuGetVersion( minimum_bound )) );
			}

			string maximum_bound = bounds[1].Trim();
			if( maximum_bound.Length > 0 )
			{
				Constraints.Add( (closing_bracket == ']' ? "<=" : "<", new NuGetVersion( maximum_bound )) );
			}
		}

		public bool SatisfiedBy( NuGetVersion version )
		{
			foreach( (string op, NuGetVersion constraint_version) in Constraints )
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
	}

	internal static class NuGetVersionMatcher
	{
		private static List<string>? GetAllVersions( string packageName )
		{
			// NuGet's v3 API requires the package id lowercased in every URL segment.
			string get_request = $"https://api.nuget.org/v3-flatcontainer/{packageName.ToLowerInvariant()}/index.json";
			string content = PackageCheck.Request( get_request ).Result;
			if( string.IsNullOrEmpty( content ) )
			{
				ConsoleLogger.Error( $"Failed to retrieve versions for package '{packageName}' from NuGet.org" );
				return null;
			}

			NuGetFlatContainerVersions? versions = JsonHelper.ReadJson<NuGetFlatContainerVersions>( content );
			return versions?.versions;
		}

		/// <summary>
		/// Finds the highest stable (non-prerelease) version of a package that satisfies a NuGet version range, e.g. "[8.0.1, )", "[2.0.1, 3.0.0)", "[4.0.0]".
		/// </summary>
		/// <param name="packageName">The NuGet package id, e.g. "Newtonsoft.Json".</param>
		/// <param name="range">The version range in NuGet interval notation; blank matches every version.</param>
		/// <returns>The highest matching version, or null if the package has no stable versions or none satisfy the range.</returns>
		public static NuGetVersion? FindMatchingVersion( string packageName, string range )
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
		/// <param name="packageName">The NuGet package id, used for logging.</param>
		/// <param name="versions">The published versions of the package.</param>
		/// <param name="range">The version range in NuGet interval notation; blank matches every version.</param>
		/// <returns>The highest matching version, or null if there are no stable versions, none satisfy the range, or the range could not be parsed.</returns>
		internal static NuGetVersion? SelectMatchingVersion( string packageName, List<string> versions, string range )
		{
			try
			{
				// Create requirement from the range
				NuGetRequirement requirement = new NuGetRequirement( range );

				if( versions.Count == 0 )
				{
					ConsoleLogger.Warning( $"Package '{packageName}' has no available versions." );
					return null;
				}

				// Convert to NuGetVersion objects and filter prereleases. A version that cannot be parsed is skipped rather than abandoning the whole
				// selection, as one malformed entry would otherwise hide every good one.
				List<NuGetVersion> available_versions = new List<NuGetVersion>();
				foreach( string version in versions )
				{
					if( !NuGetVersion.TryParse( version, out NuGetVersion? parsed_version ) || parsed_version == null )
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
				List<NuGetVersion> matching_versions = available_versions
					.Where( v => requirement.SatisfiedBy( v ) )
					.ToList();

				// Get the highest matching version
				NuGetVersion? matching_version = matching_versions.Max();
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
