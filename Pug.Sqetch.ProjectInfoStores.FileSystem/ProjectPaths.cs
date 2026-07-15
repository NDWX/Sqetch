namespace Pug.Sqetch.Stores.FileSystem;

/// <summary>
/// Single owner of the project's on-disk layout: every path is constructed here, and
/// release paths are delegated to the configured <see cref="IReleaseShardingStrategy"/>.
/// </summary>
public sealed class ProjectPaths
{
	private readonly IReleaseShardingStrategy _sharding;

	public ProjectPaths( string root, IReleaseShardingStrategy sharding )
	{
		Root = Path.GetFullPath( root );
		_sharding = sharding;
	}

	public string Root { get; }

	public string ProjectFile => Path.Combine( Root, FileNames.ProjectFile );

	public string PlanIndexFile => Path.Combine( Root, FileNames.PlanIndexFile );

	public string PlansDirectory => Path.Combine( Root, FileNames.PlansDirectory );

	public string ReleasesDirectory => Path.Combine( Root, FileNames.ReleasesDirectory );

	public string PlanDirectory( string release, string plan )
		=> string.IsNullOrEmpty( release )
			? Path.Combine( PlansDirectory, plan )
			: Path.Combine( ReleasePlansDirectory( release ), plan );

	public string PlanFile( string release, string plan )
		=> Path.Combine( PlanDirectory( release, plan ), FileNames.PlanFile );

	public string StepsDirectory( string release, string plan )
		=> Path.Combine( PlanDirectory( release, plan ), FileNames.StepsDirectory );

	public string StepDirectory( string release, string plan, string step )
		=> Path.Combine( StepsDirectory( release, plan ), step );

	public string StepFile( string release, string plan, string step )
		=> Path.Combine( StepDirectory( release, plan, step ), FileNames.StepFile );

	public string ReleaseDirectory( string release )
	{
		string shard = _sharding.GetShardPath( release );

		return string.IsNullOrEmpty( shard )
			? Path.Combine( ReleasesDirectory, release )
			: Path.Combine( ReleasesDirectory, shard.Replace( '/', Path.DirectorySeparatorChar ), release );
	}

	public string ReleaseFile( string release )
		=> Path.Combine( ReleaseDirectory( release ), FileNames.ReleaseFile );

	public string ReleasePlansDirectory( string release )
		=> Path.Combine( ReleaseDirectory( release ), FileNames.PlansDirectory );

	public string RelativeToRoot( string path )
		=> Path.GetRelativePath( Root, path ).Replace( Path.DirectorySeparatorChar, '/' );

	/// <summary>
	/// Directories of releases whose name starts with <paramref name="prefix"/>, visiting
	/// only the shards the strategy says may contain a match.
	/// </summary>
	public IEnumerable<string> EnumerateReleaseDirectories( string prefix )
	{
		if( !Directory.Exists( ReleasesDirectory ) )
			yield break;

		foreach( string shardDirectory in EnumerateShardDirectories( ReleasesDirectory, _sharding.ShardDepth, string.Empty, prefix ) )
		{
			foreach( string releaseDirectory in Directory.EnumerateDirectories( shardDirectory ) )
			{
				string name = Path.GetFileName( releaseDirectory );

				if( ( prefix.Length == 0 || name.StartsWith( prefix, StringComparison.Ordinal ) ) &&
					File.Exists( Path.Combine( releaseDirectory, FileNames.ReleaseFile ) ) )
					yield return releaseDirectory;
			}
		}
	}

	private IEnumerable<string> EnumerateShardDirectories( string directory, int remainingDepth, string shardPath, string prefix )
	{
		if( remainingDepth == 0 )
		{
			if( _sharding.MayContainMatch( shardPath, prefix ) )
				yield return directory;

			yield break;
		}

		foreach( string child in Directory.EnumerateDirectories( directory ) )
		{
			string childShard = shardPath.Length == 0
									? Path.GetFileName( child )
									: $"{shardPath}/{Path.GetFileName( child )}";

			foreach( string shard in EnumerateShardDirectories( child, remainingDepth - 1, childShard, prefix ) )
				yield return shard;
		}
	}
}
