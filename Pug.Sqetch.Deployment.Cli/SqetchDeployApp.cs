using Pug.Sqetch.Bundling;
using Pug.Sqetch.Bundling.BundleTypes;
using Pug.Sqetch.Bundling.Layouts;
using Pug.Sqetch.Cli.Commands.Deployment;
using Pug.Sqetch.Deployment.DatabaseDriver;
using Pug.Sqetch.Deployment.DatabaseDrivers.PostgreSql;
using Pug.Sqetch.Deployment.DatabaseDrivers.Sqlite;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pug.Sqetch.Deployment.Cli;

/// <summary>
/// The 'sqetch-deploy' command tree. Kept separate from the entry point so tests can host
/// the exact same configuration in a <c>CommandAppTester</c>, swapping the registrar's
/// component registries for fakes.
/// </summary>
public static class SqetchDeployApp
{
	/// <summary>
	/// The production composition: every registry carries the built-ins this host offers. A driver
	/// is registered here and nowhere else, so 'sqetch-deploy' can ship with one set of drivers
	/// while another host — a pipeline embedding the engine — offers its own.
	/// </summary>
	public static TypeRegistrar CreateDefaultRegistrar()
	{
		BundleTypeRegistry bundleTypes = new ();
		bundleTypes.RegisterBundleTypes();

		BundleLayoutRegistry layouts = new ();
		layouts.RegisterLayouts();

		TypeRegistrar registrar = new ();

		DatabaseDriverRegistry drivers = new ();
		drivers.RegisterPostgreSqlDriver();
		drivers.RegisterSqliteDriver();

		registrar.RegisterInstance( typeof(IDatabaseDriverRegistry), drivers );
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

			DeployCliErrors.Report( console, exception );

			// nothing here can know whether a deployment got far enough to change the database, so
			// the deploy command returns its own code and lets escape only what it never reached
			return DeployExitCodes.For( exception );
		} );

		config.AddCommand<DeployCommand>( "deploy" )
				.WithDescription(
					"Deploy a bundle's plans in manifest order; driver parameters are passed as "
					+ "--<driver>-<parameter> <value> (use --<driver>-<parameter>=<value> for values starting with '-'). "
					+ "Run 'drivers parameters <driver>' to see which a driver takes" );

		// the driver switches cannot appear in 'deploy --help': they are the selected driver's, and
		// no driver is selected while help is being printed
		config.AddBranch( "drivers", drivers =>
		{
			drivers.SetDescription( "Inspect the database drivers this host offers" );

			drivers.AddCommand<DriverListCommand>( "list" )
					.WithDescription( "List the driver names '--driver' accepts" );

			drivers.AddCommand<DriverParametersCommand>( "parameters" )
					.WithDescription(
						"Show a driver's --<driver>-<parameter> switches, and the parameter sets it requires "
						+ "— any one of which must be supplied in full" );
		} );

		config.AddCommand<RollbackCommand>( "rollback" )
				.WithDescription( "Roll back deployed releases (not yet supported)" );
	}
}
