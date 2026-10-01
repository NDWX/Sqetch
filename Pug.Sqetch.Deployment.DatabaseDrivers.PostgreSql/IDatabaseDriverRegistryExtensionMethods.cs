using Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

namespace Pug.Sqetch.Deployment.DatabaseDriver.PostgreSql;

public static class IDatabaseDriverRegistryExtensionMethods
{
	public static void RegisterPostgreSqlDriver( this IDatabaseDriverRegistry registry )
		=> registry.Register(
			PostgreSqlDatabaseDriverFactory.DriverName, () => new PostgreSqlDatabaseDriverFactory() );
}
