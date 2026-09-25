using Pug.Sqetch.Bundling;
using Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pug.Sqetch.Deployment;

public sealed class DeployCommand(
	IAnsiConsole console,
	IDatabaseDriverRegistry drivers,
	IChangeJournalWriterRegistry journals,
	IBundleTypeRegistry bundleTypes,
	IBundleLayoutRegistry layouts )
	: Command<DeploymentSettings>
{
	protected override int Execute( CommandContext context, DeploymentSettings settings, CancellationToken cancellationToken )
	{
		IBundleType type = ResolveBundleType( settings );

		using IBundleReader reader = type.Open( settings.Bundle );

		IBundleLayout layout = layouts.Create( DefaultBundleLayout.LayoutName );

		// validation confirms the manifest and every listed script before anything touches
		// the database
		BundleManifest manifest = BundleValidator.Validate( reader, layout );

		IDatabaseDriverFactory factory = drivers.Create( settings.Driver! );

		IDictionary<string, string> parameters = DriverParameters.Parse( context.Remaining, factory );

		IDatabaseDriver driver;

		try
		{
			driver = factory.Create( parameters, settings.StepScriptTimeout );
		}
		catch( Exception exception )
		{
			throw new DriverCreationException( factory.Name, exception );
		}

		IChangeJournalWriter journal = journals.Create( settings.Journal );

		using ConsoleDeploymentListener listener = new ( console, settings.Log );

		try
		{
			new DeploymentEngine( driver, journal, reader, layout, settings.Level, listener )
				.Deploy( manifest );
		}
		catch( Exception exception )
		{
			listener.Failed( DeployCliErrors.Describe( exception ) );

			throw;
		}

		console.WriteLine( listener.UpToDate ? "database is up to date" : "deployment complete" );

		return 0;
	}

	private IBundleType ResolveBundleType( DeploymentSettings settings )
	{
		if( settings.BundleType is not null )
			return bundleTypes.Create( settings.BundleType );

		if( Directory.Exists( settings.Bundle ) )
			return bundleTypes.Create( DirectoryBundleType.TypeName );

		if( settings.Bundle.EndsWith( ".tar.gz", StringComparison.OrdinalIgnoreCase )
			|| settings.Bundle.EndsWith( ".tgz", StringComparison.OrdinalIgnoreCase ) )
			return bundleTypes.Create( TarGzBundleType.TypeName );

		if( settings.Bundle.EndsWith( ".tar", StringComparison.OrdinalIgnoreCase ) )
			return bundleTypes.Create( TarBundleType.TypeName );

		return bundleTypes.Create( ZipBundleType.TypeName );
	}
}
