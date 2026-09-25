using Pug.Sqetch.Bundling;
using Pug.Sqetch.Bundling.Layouts;
using Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pug.Sqetch.Deployment;

/// <summary>
/// The 'sqetch-deploy' command tree. Kept separate from the entry point so tests can host
/// the exact same configuration in a <c>CommandAppTester</c>, swapping the registrar's
/// component registries for fakes.
/// </summary>
public static class SqetchDeployApp
{
	/// <summary>
	/// The production composition: bundle types and layouts carry their built-ins, while
	/// the database driver and change journal writer registries ship empty until real
	/// implementations exist.
	/// </summary>
	public static TypeRegistrar CreateDefaultRegistrar()
	{
		BundleTypeRegistry bundleTypes = new ();
		bundleTypes.RegisterBundleTypes();

		BundleLayoutRegistry layouts = new ();
		layouts.RegisterLayouts();

		TypeRegistrar registrar = new ();

		registrar.RegisterInstance( typeof(IDatabaseDriverRegistry), new DatabaseDriverRegistry() );
		registrar.RegisterInstance( typeof(IChangeJournalWriterRegistry), new ChangeJournalWriterRegistry() );
		registrar.RegisterInstance( typeof(IBundleTypeRegistry), bundleTypes );
		registrar.RegisterInstance( typeof(IBundleLayoutRegistry), layouts );

		return registrar;
	}

	public static void Configure( IConfigurator config )
	{
		config.SetApplicationName( "sqetch-deploy" );

		// unknown options must flow into the remaining arguments, where the deploy command
		// picks up the driver's --<driver>-<parameter> switches
		config.Settings.StrictParsing = false;

		config.SetExceptionHandler( ( exception, resolver ) =>
		{
			IAnsiConsole console = resolver?.Resolve( typeof(IAnsiConsole) ) as IAnsiConsole ?? AnsiConsole.Console;

			if( exception is CommandAppException { Pretty: not null } parseError )
				console.Write( parseError.Pretty );
			else
				console.MarkupLineInterpolated( $"[red]error:[/] {DeployCliErrors.Describe( exception )}" );

			return 1;
		} );

		config.AddCommand<DeployCommand>( "deploy" )
				.WithDescription(
					"Deploy a bundle's plans in manifest order; driver parameters are passed as "
					+ "--<driver>-<parameter> <value> (use --<driver>-<parameter>=<value> for values starting with '-')" );

		config.AddCommand<RollbackCommand>( "rollback" )
				.WithDescription( "Roll back deployed releases (not yet supported)" );
	}
}
