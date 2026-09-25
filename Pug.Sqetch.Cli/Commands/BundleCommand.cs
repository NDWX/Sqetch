using System.ComponentModel;
using Pug.Sqetch.Bundling;
using Pug.Sqetch.Bundling.Layouts;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pug.Sqetch;

public sealed class BundleCommand( IAnsiConsole console ) : Command<BundleCommand.Settings>
{
	public sealed class Settings : CommandSettings
	{
		[CommandOption( "--test" )]
		[Description( "Bundle every plan regardless of release status" )]
		public bool Test { get; init; }

		[CommandOption( "--finalized" )]
		[Description( "Bundle only plans of finalized releases (the default)" )]
		public bool Finalized { get; init; }

		[CommandOption( "-t|--bundle-type <TYPE>" )]
		[Description( "Bundle type: zip (default), tar, tar.gz or directory" )]
		public string? BundleType { get; init; }

		[CommandOption( "-o|--bundle-output-path <PATH>" )]
		[Description( "Output archive file or directory; defaults to <project>-<timestamp> in the current directory" )]
		public string? BundleOutputPath { get; init; }

		[CommandOption( "--since <RELEASE>" )]
		[Description( "Bundle only the releases after the named one (a continuation bundle)" )]
		public string? Since { get; init; }

		internal BundleSelection Selection { get; private set; }

		internal IBundleType Type { get; private set; } = null!;

		public override ValidationResult Validate()
		{
			if( Test && Finalized )
				return ValidationResult.Error( "specify either --test or --finalized, not both" );

			if( !NameValidation.IsValid( Since ) )
				return ValidationResult.Error( NameValidation.Error( "--since" ) );

			BundleTypeRegistry registry = new ();
			registry.RegisterBundleTypes();

			if( !registry.TryCreate( BundleType ?? ZipBundleType.TypeName, out IBundleType? type ) )
				return ValidationResult.Error(
					$"--bundle-type must be one of: {string.Join( ", ", registry.Names )}" );

			Type = type;
			Selection = Test ? BundleSelection.Test : BundleSelection.Finalized;

			return ValidationResult.Success();
		}
	}

	protected override int Execute( CommandContext context, Settings settings, CancellationToken cancellationToken )
	{
		using ProjectSession session = ProjectSession.Open();

		ProjectDefinition definition = session.Stores.InfoStore.GetDefinition();

		// assembling verifies every included plan's step scripts, so failures surface
		// before any output exists
		Bundle bundle = new BundleBuilder( session.Project, definition ).Assemble( settings.Selection, settings.Since );

		string path = settings.BundleOutputPath
					?? settings.Type.DefaultOutputName( $"{definition.Name}-{DateTime.Now:yyyyMMddHHmmss}" );

		IBundleWriter writer = settings.Type.Create( path );

		try
		{
			using (writer)
			{
				BundleLayoutRegistry bundleLayoutRegistry = new BundleLayoutRegistry();
				bundleLayoutRegistry.RegisterLayouts();

				bundleLayoutRegistry.Create(DefaultBundleLayout.LayoutName)
					.Write(bundle, writer);
			}
		}
		catch
		{
			DeleteOutput( path );
			throw;
		}

		console.WriteLine( path );

		return 0;
	}

	private static void DeleteOutput( string path )
	{
		// Create refused pre-existing targets, so whatever sits at the path is our partial
		// output
		if( File.Exists( path ) )
			File.Delete( path );
		else if( Directory.Exists( path ) )
			Directory.Delete( path, recursive: true );
	}
}