using Pug.Sqetch.Deployment.DatabaseDriver;

namespace Pug.Sqetch.Deployment.DatabaseDrivers.PostgreSql;

public static class IDatabaseDriverRegistryExtensionMethods
{
	public static void RegisterPostgreSqlDriver( this IDatabaseDriverRegistry registry )
		=> registry.Register(
			PostgreSqlDatabaseDriverFactory.DriverName, () => new PostgreSqlDatabaseDriverFactory() );
}
