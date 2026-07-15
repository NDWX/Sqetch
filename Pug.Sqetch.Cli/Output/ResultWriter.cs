using System.Text;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Pug.Sqetch;

/// <summary>
/// Renders complex command results. Scalars are printed directly by commands; anything
/// tabular goes through here so every list honors the same '--output' contract: JSON and
/// CSV on request, otherwise a table on an interactive console and header-less
/// tab-separated rows when output is piped or redirected.
/// </summary>
internal static class ResultWriter
{
	private static readonly JsonSerializerOptions JsonOptions = new ()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
		NewLine = "\n"
	};

	public static void WriteRows<T>(
		IAnsiConsole console, OutputFormat format,
		string[] headers, IReadOnlyList<T> items, Func<T, string[]> row, Func<T, object> json )
	{
		switch( format )
		{
			case OutputFormat.Json:
				console.WriteLine( JsonSerializer.Serialize( items.Select( json ), JsonOptions ) );
				break;

			case OutputFormat.Csv:
				console.WriteLine( ToCsvLine( headers ) );

				foreach( T item in items )
					console.WriteLine( ToCsvLine( row( item ) ) );

				break;

			case OutputFormat.Auto when console.Profile.Capabilities.Interactive:
				WriteTable( console, headers, items.Select( row ) );
				break;

			default:
				foreach( T item in items )
					console.WriteLine( string.Join( '\t', row( item ) ) );

				break;
		}
	}

	/// <summary>
	/// Single-item detail view: a label/value table (or 'label\tvalue' lines when piped);
	/// JSON serializes <paramref name="details"/> itself, CSV emits one header + one row.
	/// </summary>
	public static void WriteDetails(
		IAnsiConsole console, OutputFormat format,
		object details, (string Label, string Value)[] pairs )
	{
		switch( format )
		{
			case OutputFormat.Json:
				console.WriteLine( JsonSerializer.Serialize( details, JsonOptions ) );
				break;

			case OutputFormat.Csv:
				console.WriteLine( ToCsvLine( pairs.Select( x => x.Label ).ToArray() ) );
				console.WriteLine( ToCsvLine( pairs.Select( x => x.Value ).ToArray() ) );
				break;

			case OutputFormat.Auto when console.Profile.Capabilities.Interactive:
				Table table = new Table().HideHeaders().AddColumns( "", "" );

				foreach( (string label, string value) in pairs )
					table.AddRow( new Text( label, new Style( decoration: Decoration.Bold ) ), new Text( value ) );

				console.Write( table );
				break;

			default:
				foreach( (string label, string value) in pairs )
					console.WriteLine( $"{label}\t{value}" );

				break;
		}
	}

	private static void WriteTable( IAnsiConsole console, string[] headers, IEnumerable<string[]> rows )
	{
		Table table = new ();

		foreach( string header in headers )
			table.AddColumn( new TableColumn( new Text( header, new Style( decoration: Decoration.Bold ) ) ) );

		foreach( string[] row in rows )
			table.AddRow( row.Select( IRenderable ( x ) => new Text( x ) ).ToArray() );

		console.Write( table );
	}

	private static string ToCsvLine( string[] cells )
	{
		StringBuilder line = new ();

		for( int index = 0; index < cells.Length; index++ )
		{
			if( index > 0 )
				line.Append( ',' );

			string cell = cells[index];

			if( cell.Contains( ',' ) || cell.Contains( '"' ) || cell.Contains( '\n' ) )
				line.Append( '"' ).Append( cell.Replace( "\"", "\"\"" ) ).Append( '"' );
			else
				line.Append( cell );
		}

		return line.ToString();
	}
}
