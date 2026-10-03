using Pug.Sqetch.Deployment.DatabaseDriver;

namespace Pug.Sqetch.Deployment.DatabaseDrivers.Sqlite;

public static class IDatabaseDriverRegistryExtensionMethods
{
	public static void RegisterSqliteDriver( this IDatabaseDriverRegistry registry )
		=> registry.Register( SqliteDatabaseDriverFactory.DriverName, () => new SqliteDatabaseDriverFactory() );
}
