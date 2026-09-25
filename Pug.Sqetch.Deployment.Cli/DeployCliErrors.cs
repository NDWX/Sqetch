using Pug.Sqetch.Bundling;
using Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

namespace Pug.Sqetch.Deployment;

internal static class DeployCliErrors
{
	public static string Describe( Exception exception )
		=> exception switch
		{
			UnknownDatabaseDriverException unknownDriver =>
				unknownDriver.Known.Count == 0
					? $"unknown database driver '{unknownDriver.Name}'; no drivers are registered"
					: $"unknown database driver '{unknownDriver.Name}'; known drivers: {string.Join( ", ", unknownDriver.Known )}",
			UnknownChangeJournalWriterException { Name: null } noJournal =>
				noJournal.Known.Any()
					? $"no change journal writer specified and none is the default; use --journal with one of: {string.Join( ", ", noJournal.Known )}"
					: "no change journal writer is registered",
			UnknownChangeJournalWriterException unknownJournal =>
				$"unknown change journal writer '{unknownJournal.Name}'; known writers: {string.Join( ", ", unknownJournal.Known )}",
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
