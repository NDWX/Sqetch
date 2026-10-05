using Pug.Sqetch.Bundling;
using Pug.Sqetch.Deployment;
using Pug.Sqetch.DatabaseDriver;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pug.Sqetch.Cli.Commands.Deployment;

public static class DeployCliErrors
{
	/// <summary>
	/// Writes the one line a failure gets. Both the central exception handler and the deploy
	/// command call it: the command reports its own failures because only it knows what the
	/// deployment left behind, which is what decides its exit code.
	/// </summary>
	public static void Report( IAnsiConsole console, Exception exception )
	{
		if( exception is CommandAppException { Pretty: not null } parseError )
			console.Write( parseError.Pretty );
		else
			console.MarkupLineInterpolated( $"[red]error:[/] {Describe( exception )}" );
	}

	public static string Describe( Exception exception )
		=> exception switch
		{
			UnknownDatabaseDriverException unknownDriver =>
				unknownDriver.Known.Count == 0
					? $"unknown database driver '{unknownDriver.Name}'; no drivers are registered"
					: $"unknown database driver '{unknownDriver.Name}'; known drivers: {string.Join( ", ", unknownDriver.Known )}",
			DriverCreationException creation =>
				$"failed to create database driver '{creation.Driver}': {creation.InnerException?.Message}",
			StepScriptFailedException failed =>
				$"step '{failed.Step}' of plan '{failed.Plan}' in release '{failed.Release}' failed: "
				+ $"{failed.InnerException?.Message}; the transaction has been rolled back",
			DeploymentException deployment => deployment.Message,
			BundlingException bundling => bundling.Message,
			_ => exception.Message
		};
}
