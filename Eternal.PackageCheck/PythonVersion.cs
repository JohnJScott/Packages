// Copyright Eternal Developments, LLC. All Rights Reserved.

using Eternal.ConsoleUtilities;
using System.Text.RegularExpressions;

namespace Eternal.PackageCheck
{
	/// <summary>
	/// A single published PyPI version, e.g. "2.32.3", "1.0rc1", "2.0.post1" or "1!1.0". PEP 440 versions are not SemVer: the release segment may hold any
	/// number of parts, an epoch may precede it, and a pre-release, post-release and development release may follow it, each spelt several ways. A local
	/// version after a '+' takes no part in ordering here, as PyPI refuses to publish one.
	/// </summary>
	internal partial class PythonVersion
		: IComparable<PythonVersion>
	{
		/// <summary>
		/// The version scheme from the PEP 440 appendix, which is the normative definition of what PyPI accepts.
		/// </summary>
		[GeneratedRegex(
			@"^\s*v?(?:(?:(?<epoch>[0-9]+)!)?(?<release>[0-9]+(?:\.[0-9]+)*)" +
			@"(?<pre>[-_\.]?(?<pre_l>alpha|a|beta|b|preview|pre|c|rc)[-_\.]?(?<pre_n>[0-9]+)?)?" +
			@"(?<post>(?:-(?<post_n1>[0-9]+))|(?:[-_\.]?(?<post_l>post|rev|r)[-_\.]?(?<post_n2>[0-9]+)?))?" +
			@"(?<dev>[-_\.]?(?<dev_l>dev)[-_\.]?(?<dev_n>[0-9]+)?)?)" +
			@"(?:\+(?<local>[a-z0-9]+(?:[-_\.][a-z0-9]+)*))?\s*$",
			RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture )]
		private static partial Regex VersionPattern();

		/// <summary>Sorts below every pre-release, used by a development release that has no pre-release or post-release of its own.</summary>
		private const int DevOnlyPreRank = -1;

		/// <summary>Sorts above every pre-release, used by a version that is not a pre-release at all.</summary>
		private const int NoPreRank = int.MaxValue;

		/// <summary>Sorts below every post-release, used by a version that is not a post-release.</summary>
		private const int NoPostNumber = -1;

		/// <summary>Sorts above every development release, used by a version that is not a development release.</summary>
		private const int NoDevNumber = int.MaxValue;

		public int Epoch { get; }
		public int[] Parts { get; }
		private int PreRank { get; }
		private int PreNumber { get; }
		private int PostNumber { get; }
		private int DevNumber { get; }
		private string PublishedVersion { get; }

		/// <summary>
		/// True for a pre-release or a development release. A post-release is not a pre-release; it comes after the release it follows.
		/// </summary>
		public bool IsPreRelease { get; }

		public PythonVersion( string version )
		{
			PublishedVersion = version;

			Match match = VersionPattern().Match( version );
			if( !match.Success )
			{
				throw new FormatException( $"'{version}' is not a valid PEP 440 version" );
			}

			Epoch = match.Groups["epoch"].Success ? int.Parse( match.Groups["epoch"].Value ) : 0;
			Parts = match.Groups["release"].Value.Split( '.' ).Select( int.Parse ).ToArray();

			bool has_pre = match.Groups["pre_l"].Success;
			bool has_post = match.Groups["post"].Success;
			bool has_dev = match.Groups["dev"].Success;

			if( has_pre )
			{
				PreRank = PreReleaseRank( match.Groups["pre_l"].Value );
				PreNumber = match.Groups["pre_n"].Success ? int.Parse( match.Groups["pre_n"].Value ) : 0;
			}
			else
			{
				// A development release with nothing else attached precedes every pre-release of the same version, so "1.0.dev1" is below "1.0a1".
				PreRank = ( has_dev && !has_post ) ? DevOnlyPreRank : NoPreRank;
				PreNumber = 0;
			}

			if( has_post )
			{
				// "1.0-1" is the implicit spelling of "1.0.post1", and "post", "rev" and "r" are the explicit ones.
				PostNumber = ReadOptionalNumber( match, "post_n1", "post_n2" );
			}
			else
			{
				PostNumber = NoPostNumber;
			}

			DevNumber = has_dev ? ReadOptionalNumber( match, "dev_n" ) : NoDevNumber;

			IsPreRelease = has_pre || has_dev;
		}

		/// <summary>
		/// Reads the number attached to a post-release or development release. "1.0.post" and "1.0.dev" are both legal and mean the zeroth of their kind.
		/// </summary>
		/// <param name="match">The matched version.</param>
		/// <param name="groups">The capture groups the number may have landed in, in the order they should be tried.</param>
		/// <returns>The number that was written, or 0 if none was.</returns>
		private static int ReadOptionalNumber( Match match, params string[] groups )
		{
			foreach( string group in groups )
			{
				if( match.Groups[group].Success )
				{
					return int.Parse( match.Groups[group].Value );
				}
			}

			return 0;
		}

		/// <summary>
		/// Reduces the several spellings of a pre-release to a rank: "alpha" and "a" are the lowest, then "beta" and "b", then "c", "pre", "preview" and
		/// "rc", which PEP 440 all normalize to "rc".
		/// </summary>
		/// <param name="preReleaseLabel">The pre-release label as it was written.</param>
		/// <returns>0 for an alpha, 1 for a beta, 2 for a release candidate.</returns>
		private static int PreReleaseRank( string preReleaseLabel )
		{
			string normalized_label = preReleaseLabel.ToLowerInvariant();

			if( normalized_label == "a" || normalized_label == "alpha" )
			{
				return 0;
			}

			if( normalized_label == "b" || normalized_label == "beta" )
			{
				return 1;
			}

			return 2;
		}

		/// <summary>
		/// Creates a version from a published version string, reporting failure rather than throwing when it is not a version PyPI could have published.
		/// </summary>
		/// <param name="version">The version string to parse.</param>
		/// <param name="pythonVersion">The parsed version, or null if it could not be parsed.</param>
		/// <returns>True if the version was parsed.</returns>
		internal static bool TryParse( string version, out PythonVersion? pythonVersion )
		{
			try
			{
				pythonVersion = new PythonVersion( version );
				return true;
			}
			catch( Exception )
			{
				pythonVersion = null;
				return false;
			}
		}

		public int CompareTo( PythonVersion? other )
		{
			if( other == null )
			{
				return 1;
			}

			// An epoch outranks everything else, which is how a project restarts its numbering, e.g. "1!1.0" is later than "2023.1".
			int epoch_comparison = Epoch.CompareTo( other.Epoch );
			if( epoch_comparison != 0 )
			{
				return epoch_comparison;
			}

			// Missing trailing parts count as 0, so "1.0" and "1.0.0" are equal as they are in PEP 440
			int max_length = Math.Max( Parts.Length, other.Parts.Length );
			for( int i = 0; i < max_length; i++ )
			{
				int comparison = PartAt( i ).CompareTo( other.PartAt( i ) );
				if( comparison != 0 )
				{
					return comparison;
				}
			}

			// Within one release the order is development release, pre-release, the release itself, then post-release.
			int pre_rank_comparison = PreRank.CompareTo( other.PreRank );
			if( pre_rank_comparison != 0 )
			{
				return pre_rank_comparison;
			}

			int pre_number_comparison = PreNumber.CompareTo( other.PreNumber );
			if( pre_number_comparison != 0 )
			{
				return pre_number_comparison;
			}

			int post_comparison = PostNumber.CompareTo( other.PostNumber );
			if( post_comparison != 0 )
			{
				return post_comparison;
			}

			return DevNumber.CompareTo( other.DevNumber );
		}

		public int PartAt( int index )
		{
			return index < Parts.Length ? Parts[index] : 0;
		}

		/// <summary>
		/// True when this version's release segment begins with every part of the given one, which is the test PEP 440 prefix matching ("== 1.2.*") makes.
		/// </summary>
		/// <param name="prefix">The version written before the ".*".</param>
		/// <returns>True if this version is in the release line the prefix names.</returns>
		public bool HasReleasePrefix( PythonVersion prefix )
		{
			if( Epoch != prefix.Epoch )
			{
				return false;
			}

			for( int i = 0; i < prefix.Parts.Length; i++ )
			{
				if( PartAt( i ) != prefix.PartAt( i ) )
				{
					return false;
				}
			}

			return true;
		}

		public override string ToString()
		{
			return PublishedVersion;
		}
	}

	/// <summary>
	/// A PEP 440 version specifier, as found in a package's "Requires-Dist" entries, e.g. "&gt;=1.21.0,&lt;2.0a0" or "~=1.4.2". Every comma separated clause
	/// must hold, as the clauses of a RubyGems requirement do. The compatible release operator "~=" and prefix matching ("== 1.2.*") are reduced to the
	/// same operator-and-version constraints a GemRequirement holds, so that satisfying them is the same test.
	/// </summary>
	internal class PythonRequirement
	{
		private readonly List<(string Operator, PythonVersion Version)> Constraints = new List<(string Operator, PythonVersion Version)>();

		/// <summary>The right hand sides of any "===" clauses, which PEP 440 compares as text and so need not parse as versions at all.</summary>
		private readonly List<string> ArbitraryEqualityVersions = new List<string>();

		public PythonRequirement( string specifier )
		{
			foreach( string clause in specifier.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
			{
				ParseClause( clause );
			}
		}

		private void ParseClause( string clause )
		{
			string clause_operator;
			string version_text;

			// The three character operator has to be tested before the two character one, and that before the one character one.
			if( clause.StartsWith( "===" ) )
			{
				clause_operator = "===";
				version_text = clause[3..].Trim();
			}
			else if( clause.StartsWith( "==" ) || clause.StartsWith( "!=" ) || clause.StartsWith( ">=" ) || clause.StartsWith( "<=" ) || clause.StartsWith( "~=" ) )
			{
				clause_operator = clause[..2];
				version_text = clause[2..].Trim();
			}
			else if( clause.StartsWith( ">" ) || clause.StartsWith( "<" ) )
			{
				clause_operator = clause[..1];
				version_text = clause[1..].Trim();
			}
			else
			{
				// A bare version in a specifier means exact equality.
				clause_operator = "==";
				version_text = clause;
			}

			if( version_text.Length == 0 )
			{
				throw new FormatException( $"'{clause}' names no version" );
			}

			// "=== 1.0" compares the text of the version rather than its value, and is the one operator that need not parse as PEP 440.
			if( clause_operator == "===" )
			{
				ArbitraryEqualityVersions.Add( version_text );
				return;
			}

			// "== 1.2.*" and "!= 1.2.*" match against the start of the release segment rather than the whole version.
			if( version_text.EndsWith( ".*" ) )
			{
				PythonVersion prefix = new PythonVersion( version_text[..^2] );
				Constraints.Add( (clause_operator == "!=" ? "!=prefix" : "==prefix", prefix) );
				return;
			}

			if( clause_operator == "~=" )
			{
				AddCompatibleRelease( version_text );
				return;
			}

			Constraints.Add( (clause_operator, new PythonVersion( version_text )) );
		}

		/// <summary>
		/// Expands the compatible release operator, which allows the last named part to grow but nothing to its left to change: "~= 1.2.3" is ">= 1.2.3"
		/// together with "== 1.2.*", and "~= 1.2" is ">= 1.2" together with "== 1.*".
		/// </summary>
		/// <param name="versionText">The version written after the "~=".</param>
		private void AddCompatibleRelease( string versionText )
		{
			PythonVersion lower_bound = new PythonVersion( versionText );
			if( lower_bound.Parts.Length < 2 )
			{
				throw new FormatException( $"'~= {versionText}' needs at least two release parts" );
			}

			Constraints.Add( (">=", lower_bound) );

			// The upper bound drops the last named part and increments the one before it, so "~= 1.2.3" stops below 1.3.
			int[] upper_parts = lower_bound.Parts[..^1];
			upper_parts[^1]++;
			string epoch_prefix = lower_bound.Epoch == 0 ? string.Empty : $"{lower_bound.Epoch}!";
			Constraints.Add( ("<", new PythonVersion( epoch_prefix + string.Join( '.', upper_parts ) )) );
		}

		public bool SatisfiedBy( PythonVersion version )
		{
			foreach( string arbitrary_version in ArbitraryEqualityVersions )
			{
				if( version.ToString() != arbitrary_version )
				{
					return false;
				}
			}

			foreach( (string clause_operator, PythonVersion constraint_version) in Constraints )
			{
				bool satisfied = clause_operator switch
				{
					">=" => version.CompareTo( constraint_version ) >= 0,
					"<=" => version.CompareTo( constraint_version ) <= 0,
					">" => version.CompareTo( constraint_version ) > 0,
					"<" => version.CompareTo( constraint_version ) < 0,
					"==" => version.CompareTo( constraint_version ) == 0,
					"!=" => version.CompareTo( constraint_version ) != 0,
					"==prefix" => version.HasReleasePrefix( constraint_version ),
					"!=prefix" => !version.HasReleasePrefix( constraint_version ),
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

	internal static class PythonVersionMatcher
	{
		private static List<string>? GetAllVersions( string packageName )
		{
			string get_request = $"https://pypi.org/pypi/{Uri.EscapeDataString( packageName )}/json";
			string content = PackageCheck.Request( get_request ).Result;
			if( string.IsNullOrEmpty( content ) )
			{
				ConsoleLogger.Error( $"Failed to retrieve versions for package '{packageName}' from PyPI.org" );
				return null;
			}

			PythonPackageRaw? raw_package = JsonHelper.ReadJson<PythonPackageRaw>( content );
			return raw_package?.releases?.Keys.ToList();
		}

		/// <summary>
		/// Finds the highest final (non pre-release) version of a package that satisfies a PEP 440 specifier, e.g. "&gt;=1.21.0,&lt;2.0", "~=1.4.2".
		/// </summary>
		/// <param name="packageName">The PyPI project name, e.g. "requests".</param>
		/// <param name="specifier">The version specifier; blank matches every version.</param>
		/// <returns>The highest matching version, or null if the package has no final versions or none satisfy the specifier.</returns>
		public static PythonVersion? FindMatchingVersion( string packageName, string specifier )
		{
			List<string>? versions = GetAllVersions( packageName );
			if( versions == null )
			{
				ConsoleLogger.Warning( $"Package '{packageName}' has no available versions." );
				return null;
			}

			return SelectMatchingVersion( packageName, versions, specifier );
		}

		/// <summary>
		/// Finds the highest final (non pre-release) version in an already retrieved list of a package's versions that satisfies the given specifier. The
		/// order of the list is irrelevant.
		/// </summary>
		/// <param name="packageName">The PyPI project name, used for logging.</param>
		/// <param name="versions">The published versions of the package.</param>
		/// <param name="specifier">The PEP 440 version specifier; blank matches every version.</param>
		/// <returns>The highest matching version, or null if there are no final versions, none satisfy the specifier, or it could not be parsed.</returns>
		internal static PythonVersion? SelectMatchingVersion( string packageName, List<string> versions, string specifier )
		{
			try
			{
				// Create requirement from the specifier
				PythonRequirement requirement = new PythonRequirement( specifier );

				if( versions.Count == 0 )
				{
					ConsoleLogger.Warning( $"Package '{packageName}' has no available versions." );
					return null;
				}

				// Convert to PythonVersion objects and filter pre-releases. A version that cannot be parsed is skipped rather than abandoning the whole
				// selection, as one malformed entry would otherwise hide every good one.
				List<PythonVersion> available_versions = new List<PythonVersion>();
				foreach( string version in versions )
				{
					if( !PythonVersion.TryParse( version, out PythonVersion? parsed_version ) || parsed_version == null )
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
					ConsoleLogger.Warning( $"Package '{packageName}' has no final versions." );
					return null;
				}

				// Find matching versions
				List<PythonVersion> matching_versions = available_versions
					.Where( v => requirement.SatisfiedBy( v ) )
					.ToList();

				// Get the highest matching version
				PythonVersion? matching_version = matching_versions.Max();
				if( matching_version != null )
				{
					ConsoleLogger.Log( $"Package '{packageName}' matches '{specifier}' with version: '{matching_version}'" );
				}
				else
				{
					ConsoleLogger.Warning( $"No version of '{packageName}' satisfies '{specifier}'." );
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
