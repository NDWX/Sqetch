using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pug.Sqetch;

internal static class OptionParsing
{
	/// <summary>Splits a comma- or semicolon-separated option value.</summary>
	public static string[] SplitList( string? value )
		=> value is null
			? []
			: value.Split( [',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );

	public static bool TryParseDate( string? value, out DateTime? date )
	{
		date = null;

		if( value is null )
			return true;

		if( !DateTime.TryParseExact(
				value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed ) )
			return false;

		date = parsed;
		return true;
	}

	/// <summary>
	/// Half-open day-boundary window: 'after' includes the given day onward (≥ 00:00 of
	/// that day), 'before' excludes the given day (&lt; 00:00 of that day).
	/// </summary>
	public static Range<DateTime>? Window( DateTime? after, DateTime? before )
		=> after is null && before is null
			? null
			: new Range<DateTime>
			{
				Start = after ?? DateTime.MinValue,
				End = before?.AddTicks( -1 ) ?? DateTime.MaxValue
			};
}

/// <summary>Base settings for commands producing complex results: the '--output' switch.</summary>
public class OutputSettings : CommandSettings
{
	[CommandOption( "-o|--output <FORMAT>" )]
	public string? Output { get; init; }

	internal OutputFormat Format { get; private set; }

	public override ValidationResult Validate()
	{
		if( !OutputFormats.TryParse( Output, out OutputFormat format ) )
			return ValidationResult.Error( "--output must be 'json' or 'csv'" );

		Format = format;

		return ValidationResult.Success();
	}
}
