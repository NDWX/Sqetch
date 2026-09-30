using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pug.Sqetch;

/// <summary>
/// A '&lt;SLOT&gt;' positional argument shared by 'print' and 'set', resolved case-insensitively
/// through <see cref="JournalingSlots.TryParse"/> at validation time so a bad name is reported
/// before the project is even opened.
/// </summary>
internal static class JournalingSlotArgument
{
	public static ValidationResult Resolve( string? name, out JournalingSlot slot )
	{
		if( JournalingSlots.TryParse( name, out slot ) )
			return ValidationResult.Success();

		return ValidationResult.Error(
			$"'{name}' is not a journaling slot; valid slots are: {string.Join( ", ", JournalingSlots.Names )}" );
	}
}

public sealed class JournalingListCommand( IAnsiConsole console ) : Command<JournalingListCommand.Settings>
{
	public sealed class Settings : OutputSettings
	{
		[CommandOption( "--statements" )]
		[Description( "Include each slot's SQL as a further column" )]
		public bool Statements { get; init; }
	}

	private readonly record struct Row( JournalingSlot Slot, bool Set, string? Statement );

	protected override int Execute( CommandContext context, Settings settings, CancellationToken cancellationToken )
	{
		using ProjectSession session = ProjectSession.Open();

		List<Row> rows = JournalingSlots.All
			.Select( slot =>
			{
				string? statement = session.Project.GetJournalingStatement( slot );

				return new Row( slot, statement is not null, statement );
			} )
			.ToList();

		string[] headers = settings.Statements ? ["Slot", "Set", "Statement"] : ["Slot", "Set"];

		Func<Row, string[]> row = settings.Statements
			? x => [x.Slot.ToString(), x.Set ? "yes" : "no", x.Statement ?? ""]
			: x => [x.Slot.ToString(), x.Set ? "yes" : "no"];

		Func<Row, object> json = settings.Statements
			? x => new { slot = x.Slot.ToString(), set = x.Set, statement = x.Statement }
			: x => new { slot = x.Slot.ToString(), set = x.Set };

		ResultWriter.WriteRows( console, settings.Format, headers, rows, row, json );

		return 0;
	}
}

public sealed class JournalingPrintCommand( IAnsiConsole console ) : Command<JournalingPrintCommand.Settings>
{
	public sealed class Settings : CommandSettings
	{
		[CommandArgument( 0, "<slot>" )]
		public string SlotName { get; init; } = "";

		internal JournalingSlot Slot { get; private set; }

		public override ValidationResult Validate()
		{
			ValidationResult result = JournalingSlotArgument.Resolve( SlotName, out JournalingSlot resolved );

			Slot = resolved;

			return result;
		}
	}

	protected override int Execute( CommandContext context, Settings settings, CancellationToken cancellationToken )
	{
		using ProjectSession session = ProjectSession.Open();

		string? statement = session.Project.GetJournalingStatement( settings.Slot );

		if( statement is null )
			throw new InvalidOperationException(
				$"journaling statement '{settings.Slot}' is not set; run 'sqetch journaling set {settings.Slot} ...'" );

		// verbatim and off IAnsiConsole markup: MarkupLineInterpolated would mangle SQL
		// containing '[', and 'journaling print X > x.sql' must round-trip byte for byte
		console.WriteLine( statement );

		return 0;
	}
}

public sealed class JournalingSetCommand : Command<JournalingSetCommand.Settings>
{
	public sealed class Settings : CommandSettings
	{
		[CommandArgument( 0, "<slot>" )]
		public string SlotName { get; init; } = "";

		[CommandArgument( 1, "[sql]" )]
		public string? Sql { get; init; }

		[CommandOption( "--file <PATH>" )]
		[Description( "Read the statement text from this file" )]
		public string? FilePath { get; init; }

		[CommandOption( "--stdin" )]
		[Description( "Read the statement text from standard input, to EOF" )]
		public bool Stdin { get; init; }

		internal JournalingSlot Slot { get; private set; }

		public override ValidationResult Validate()
		{
			ValidationResult slot = JournalingSlotArgument.Resolve( SlotName, out JournalingSlot resolved );

			if( !slot.Successful )
				return slot;

			Slot = resolved;

			int sources = ( Sql is not null ? 1 : 0 ) + ( FilePath is not null ? 1 : 0 ) + ( Stdin ? 1 : 0 );

			if( sources != 1 )
				return ValidationResult.Error( "specify exactly one of <sql>, --file <PATH> or --stdin" );

			return ValidationResult.Success();
		}
	}

	protected override int Execute( CommandContext context, Settings settings, CancellationToken cancellationToken )
	{
		using ProjectSession session = ProjectSession.Open();

		// File.ReadAllText detects and strips a BOM, same as the store's own read
		string text = settings.Sql
						?? ( settings.FilePath is not null ? File.ReadAllText( settings.FilePath ) : Console.In.ReadToEnd() );

		session.Project.SetJournalingStatement( settings.Slot, text );

		return 0;
	}
}
