namespace Pug.Sqetch.Stores.FileSystem;

/// <summary>
/// Shards releases by the leading segment of the release name, e.g. with the default '.'
/// delimiter release '2026.07' lives in 'releases/2026/2026.07'. Names without the
/// delimiter shard under their own full name.
/// </summary>
public sealed class NamePrefixShardingStrategy : IReleaseShardingStrategy
{
	public const string StrategyName = "name-prefix";
	public const string DelimiterOption = "delimiter";

	private readonly string _delimiter;

	public NamePrefixShardingStrategy( string delimiter = "." )
	{
		ArgumentException.ThrowIfNullOrEmpty( delimiter );

		_delimiter = delimiter;
	}

	public static NamePrefixShardingStrategy FromOptions( IReadOnlyDictionary<string, string>? options )
		=> new ( options is not null && options.TryGetValue( DelimiterOption, out string? delimiter ) ? delimiter : "." );

	public int ShardDepth => 1;

	public string GetShardPath( string releaseName )
	{
		int delimiterIndex = releaseName.IndexOf( _delimiter, StringComparison.Ordinal );

		return delimiterIndex > 0 ? releaseName[..delimiterIndex] : releaseName;
	}

	public bool MayContainMatch( string shardPath, string releaseNamePrefix )
	{
		if( string.IsNullOrEmpty( releaseNamePrefix ) )
			return true;

		int delimiterIndex = releaseNamePrefix.IndexOf( _delimiter, StringComparison.Ordinal );

		// a prefix containing the delimiter carries a complete first segment which must
		// match the shard exactly; otherwise the shard need only start with the prefix
		return delimiterIndex > 0
			? string.Equals( shardPath, releaseNamePrefix[..delimiterIndex], StringComparison.OrdinalIgnoreCase )
			: shardPath.StartsWith( releaseNamePrefix, StringComparison.OrdinalIgnoreCase );
	}
}
