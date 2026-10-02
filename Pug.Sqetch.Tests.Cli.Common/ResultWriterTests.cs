using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Pug.Sqetch.Tests.Cli.Common;

/// <summary>
/// The machine formats both CLIs share. Every test runs against a console of a fixed, narrow width
/// with interactivity off — which is what a redirected stdout looks like, and therefore exactly the
/// case the JSON, CSV and tab-separated forms exist for.
/// </summary>
public class ResultWriterTests
{
	private const int ConsoleWidth = 80;

	private readonly TestConsole _console = new TestConsole().Width( ConsoleWidth );

	private static readonly string Long = new ( 'x', 400 );

	private readonly record struct Item( string Name, string Note );

	private static readonly Item[] Items = [new Item( "first", Long ), new Item( "second", "short" )];

	private static void WriteItems( IAnsiConsole console, OutputFormat format )
		=> ResultWriter.WriteRows(
			console, format, ["Name", "Note"], Items,
			x => [x.Name, x.Note],
			x => new { name = x.Name, note = x.Note } );

	/// <summary>
	/// Spectre wraps what it writes to the console's width. A wrapped line is not merely ugly here:
	/// the newline lands inside a JSON string, where it is an invalid control character, so a value
	/// longer than the console made '-o json' emit a document nothing could parse.
	/// </summary>
	[Fact]
	public void JsonSurvivesAValueWiderThanTheConsole()
	{
		WriteItems( _console, OutputFormat.Json );

		JsonElement[] items = JsonSerializer.Deserialize<JsonElement[]>( _console.Output )!;

		Assert.Equal( Long, items[0].GetProperty( "note" ).GetString() );
	}

	/// <summary>A wrapped row gains a newline inside a field, and 'cut -f2' stops working.</summary>
	[Fact]
	public void PipedRowsKeepOneRecordPerLine()
	{
		WriteItems( _console, OutputFormat.Auto );

		Assert.Equal( [$"first\t{Long}", "second\tshort"], Lines() );
	}

	[Fact]
	public void CsvKeepsOneRecordPerLine()
	{
		WriteItems( _console, OutputFormat.Csv );

		Assert.Equal( ["Name,Note", $"first,{Long}", "second,short"], Lines() );
	}

	[Fact]
	public void DetailsKeepOneRecordPerLine()
	{
		ResultWriter.WriteDetails(
			_console, OutputFormat.Auto, new { note = Long }, [( "Note", Long )] );

		Assert.Equal( [$"Note\t{Long}"], Lines() );
	}

	private string[] Lines()
		=> _console.Output
					.Split( '\n', StringSplitOptions.RemoveEmptyEntries )
					.Select( x => x.TrimEnd( '\r' ) )
					.ToArray();

}
