internal sealed class Arguments
{
    private readonly List<string> _positional = [];
    private readonly Dictionary<string, string> _options = new ( StringComparer.OrdinalIgnoreCase );

    public static Arguments Parse( IReadOnlyList<string> tokens, IReadOnlySet<string> flags )
    {
        Arguments arguments = new ();

        for( int i = 0; i < tokens.Count; i++ )
        {
            string token = tokens[i];

            if( !token.StartsWith( "--", StringComparison.Ordinal ) )
            {
                arguments._positional.Add( token );
                continue;
            }

            string name = token[2..];

            bool takesValue = !flags.Contains( name ) &&
                              i + 1 < tokens.Count &&
                              !tokens[i + 1].StartsWith( "--", StringComparison.Ordinal );

            arguments._options[name] = takesValue ? tokens[++i] : string.Empty;
        }

        return arguments;
    }

    public string Positional( int index, string name )
        => index < _positional.Count
            ? _positional[index]
            : throw new ArgumentException( $"missing required argument <{name}>" );

    public string? Option( string name ) => _options.GetValueOrDefault( name );

    public bool Has( string name ) => _options.ContainsKey( name );

    public string[] List( string name )
        => Option( name )?.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) ?? [];
}