using Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

namespace Pug.Sqetch.Deployment.DatabaseDriver.Sqlite;

public static class IDatabaseDriverRegistryExtensionMethods
{
	public static void RegisterSqliteDriver( this IDatabaseDriverRegistry registry )
		=> registry.Register( SqliteDatabaseDriverFactory.DriverName, () => new SqliteDatabaseDriverFactory() );
}
