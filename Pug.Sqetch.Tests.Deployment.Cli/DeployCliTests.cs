using System.Text;
using System.Text.RegularExpressions;
using Pug.Sqetch.Bundling;
using Pug.Sqetch.Bundling.Layouts;
using Pug.Sqetch.Deployment;
using Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;
using Spectre.Console.Cli.Testing;

namespace Pug.Sqetch.Tests.Deployment.Cli;

/// <summary>
/// Drives 'sqetch-deploy' through <see cref="CommandAppTester"/> hosting the production
/// configuration, with a fake driver and journal writer registered in the registrar.
/// Bundles are written to temp directories and passed by absolute path, so no test changes
/// the process-wide current directory.
/// </summary>
public class DeployCliTests : IDisposable
{
	private readonly string _root;
	private readonly FakeDatabaseDriver _driver = new ();
	private readonly FakeJournal _journal = new ();
	private readonly FakeDriverFactory _factory;
	private readonly DatabaseDriverRegistry _drivers = new ();

	public DeployCliTests()
	{
		_root = Path.Combine( Path.GetTempPath(), "sqetch-deploy-cli-tests", Guid.NewGuid().ToString( "N" ) );
		Directory.CreateDirectory( _root );

		_factory = new FakeDriverFactory( _driver );

		_drivers.Register( _factory.Name, () => _factory );
	}

	public void Dispose()
	{
		try
		{
			Directory.Delete( _root, recursive: true );
		}
		catch( IOException )
		{
		}
	}

	[Fact]
	public void DeploysAZipBundlePassingDriverParametersAndTimeout()
	{
		string bundle = WriteBundle( "demo.zip" );

		CommandAppResult result = Run(
			"deploy", bundle, "--driver", "pg", "--pg-host", "db1", "--pg-database", "app", "--timeout", "60" );

		Assert.Equal( 0, result.ExitCode );
		Assert.Contains( "deployment complete", result.Output );

		Assert.Equal( TimeSpan.FromSeconds( 60 ), _factory.ReceivedTimeout );
		Assert.Equal( "db1", _factory.ReceivedParameters!["host"] );
		Assert.Equal( "app", _factory.ReceivedParameters["database"] );

		// #1 prepares the journal and #2 reads it, so the deployment's own transaction is #3
		Assert.Equal(
			["script #3 create table t ()", "script #3 create index i"],
			_driver.Events.Where( x => x.StartsWith( "script" ) ) );
		Assert.Equal( ["ReleaseDeployed 2026.07"], _driver.Transactions[2].Journaled.Where( x => x.StartsWith( "ReleaseDeployed" ) ) );
		// the driver owns the connection, so the command releases it once — after the last commit
		Assert.Equal( ["commit #3", "dispose"], _driver.Events.TakeLast( 2 ) );
		Assert.True( _driver.Disposed );
	}

	[Fact]
	public void EqualsSyntaxBindsDriverParameterValues()
	{
		CommandAppResult result = Run(
			"deploy", WriteBundle( "demo.zip" ), "--driver", "pg", "--pg-connection-string=Server=x;Db=y" );

		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( "Server=x;Db=y", _factory.ReceivedParameters!["connection-string"] );
	}

	[Fact]
	public void UndefinedDriverParameterListsTheAvailableOnes()
	{
		CommandAppResult result = Run(
			"deploy", WriteBundle( "demo.zip" ), "--driver", "pg", "--pg-bogus", "x", "--pg-host", "db1", "--pg-database", "app" );

		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "no parameter 'bogus'", result.Output );
		Assert.Contains( "--pg-host", result.Output );
	}

	[Fact]
	public void OptionsOutsideTheDriverPrefixAreRejected()
	{
		CommandAppResult result = Run(
			"deploy", WriteBundle( "demo.zip" ), "--driver", "pg", "--nope", "x" );

		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "unknown option '--nope'", result.Output );
	}

	[Fact]
	public void MissingRequiredParameterGroupingListsTheAlternatives()
	{
		CommandAppResult result = Run(
			"deploy", WriteBundle( "demo.zip" ), "--driver", "pg", "--pg-host", "db1" );

		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "(--pg-host, --pg-database) or (--pg-connection-string)", result.Output );
	}

	[Fact]
	public void RepeatedDriverParametersAreRejected()
	{
		CommandAppResult result = Run(
			"deploy", WriteBundle( "demo.zip" ), "--driver", "pg", "--pg-host", "a", "--pg-host", "b" );

		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "more than once", result.Output );
	}

	[Fact]
	public void UnknownDriverListsTheRegisteredOnes()
	{
		CommandAppResult result = Run( "deploy", WriteBundle( "demo.zip" ), "--driver", "oracle" );

		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "unknown database driver 'oracle'", result.Output );
		Assert.Contains( "pg", result.Output );
	}

	[Fact]
	public void DriverCreationFailureIsReported()
	{
		_factory.FailWith = new InvalidOperationException( "bad credentials" );

		CommandAppResult result = Run(
			"deploy", WriteBundle( "demo.zip" ), "--driver", "pg", "--pg-host", "db1", "--pg-database", "app" );

		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "failed to create database driver 'pg': bad credentials", result.Output );
	}

	/// <summary>
	/// The journal is the project's own SQL now and travels in the bundle, so there is nothing to
	/// select. A pipeline still passing --journal must be told, not silently ignored — which is what
	/// would happen if the option were merely unused.
	/// </summary>
	[Fact]
	public void TheRetiredJournalSwitchIsRejected()
	{
		CommandAppResult result = Run(
			"deploy", WriteBundle( "demo.zip" ), "--driver", "pg", "--pg-host", "h", "--pg-database", "d",
			"--journal", "table" );

		Assert.Equal( 1, result.ExitCode );

		// rejected by the driver-parameter parser: every unrecognized option must carry the driver's
		// own '--pg-' prefix, so a stale '--journal' is named rather than quietly ignored
		string message = Regex.Replace( result.Output, @"\s+", " " );

		Assert.Contains( "--journal", message );
		Assert.Contains( "--pg-", message );
	}

	[Fact]
	public void BundleMissingItsManifestOrAScriptFails()
	{
		string empty = Path.Combine( _root, "empty" );
		Directory.CreateDirectory( empty );
		File.WriteAllText( Path.Combine( empty, "readme.txt" ), "no manifest here" );

		CommandAppResult noManifest = Run( "deploy", empty, "--driver", "pg", "--pg-host", "h", "--pg-database", "d" );

		Assert.Equal( 1, noManifest.ExitCode );
		Assert.Contains( "manifest.json", noManifest.Output );

		string incomplete = WriteBundle( "incomplete.zip", withRollbackScripts: false );

		CommandAppResult missingScript = Run( "deploy", incomplete, "--driver", "pg", "--pg-host", "h", "--pg-database", "d" );

		Assert.Equal( 1, missingScript.ExitCode );
		Assert.Contains( "rollback.sql", missingScript.Output );
		Assert.Empty( _driver.Events );
	}

	[Fact]
	public void LogFileReceivesTimestampedProgressLines()
	{
		string log = Path.Combine( _root, "logs", "deploy.log" );

		CommandAppResult result = Run(
			"deploy", WriteBundle( "demo.zip" ), "--driver", "pg", "--pg-host", "h", "--pg-database", "d", "--log", log );

		Assert.Equal( 0, result.ExitCode );

		string[] lines = File.ReadAllLines( log );

		Assert.All( lines, line => Assert.Matches( @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}  ", line ) );
		Assert.Contains( lines, line => line.EndsWith( "deploying plan 'table' of release '2026.07'" ) );
		Assert.Contains( lines, line => line.EndsWith( "committed" ) );
	}

	[Fact]
	public void InvalidSwitchValuesAreRejected()
	{
		string bundle = WriteBundle( "demo.zip" );

		Assert.Equal( 1, Run( "deploy", bundle, "--driver", "pg", "--commit-level", "bogus" ).ExitCode );
		Assert.Equal( 1, Run( "deploy", bundle, "--driver", "pg", "--timeout", "-5" ).ExitCode );
		Assert.Equal( 1, Run( "deploy", bundle ).ExitCode );
		Assert.Equal( 1, Run( "deploy", bundle, "--driver", "pg", "--bundle-type", "rar" ).ExitCode );
	}

	[Fact]
	public void BundleTypeIsInferredFromThePath()
	{
		string directory = WriteBundle( "bundle-dir", DirectoryBundleType.TypeName );

		Assert.Equal(
			0,
			Run( "deploy", directory, "--driver", "pg", "--pg-host", "h", "--pg-database", "d" ).ExitCode );

		string targz = WriteBundle( "demo.tar.gz", TarGzBundleType.TypeName );

		// the two bundles hold the same release, so the second is a deployment in its own right
		// only against a database that has not seen the first
		_journal.Latest = null;
		_journal.DeployedPlans.Clear();
		_journal.StartedReleases.Clear();

		Assert.Equal(
			0,
			Run( "deploy", targz, "--driver", "pg", "--pg-host", "h", "--pg-database", "d" ).ExitCode );
	}

	[Fact]
	public void UpToDateDatabaseReportsSoAndTouchesNothing()
	{
		_journal.Latest = new JournaledRelease( "2026.07", Completed: true );
		_journal.DeployedPlans["2026.07"] = ["table", "index"];

		CommandAppResult result = Run(
			"deploy", WriteBundle( "demo.zip" ), "--driver", "pg", "--pg-host", "h", "--pg-database", "d" );

		Assert.Equal( 0, result.ExitCode );
		Assert.Contains( "database is up to date", result.Output );

		// the journal is prepared and read on every deployment, so those two transactions are
		// expected; nothing beyond them ran
		Assert.Equal( 2, _driver.Transactions.Count );
		Assert.Empty( _driver.AppliedScripts );
	}

	[Fact]
	public void ContinuationBundleIsRefusedWhenTheJournaledPredecessorIsIncomplete()
	{
		_journal.Latest = new JournaledRelease( "2026.06", Completed: false );

		string bundle = WriteBundle( "continuation.zip", dependency: "2026.06" );

		CommandAppResult result = Run( "deploy", bundle, "--driver", "pg", "--pg-host", "h", "--pg-database", "d" );

		Assert.Equal( 1, result.ExitCode );

		// console wrapping breaks the message across lines
		string message = Regex.Replace( result.Output, @"\s+", " " );

		Assert.Contains( "release '2026.06' is only partially deployed", message );
		Assert.Contains( "before release '2026.07'", message );

		// refused from what the journal said, so the journal was prepared and read — and no more
		Assert.Equal( 2, _driver.Transactions.Count );
		Assert.Empty( _driver.AppliedScripts );
	}

	[Fact]
	public void RollbackParsesItsSwitchesButIsNotYetSupported()
	{
		string bundle = WriteBundle( "demo.zip" );

		CommandAppResult result = Run(
			"rollback", bundle, "--driver", "pg", "--pg-host", "h", "--pg-database", "d", "--until-release", "2026.07" );

		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "rollback is not yet supported", result.Output );

		CommandAppResult both = Run(
			"rollback", bundle, "--driver", "pg", "--after-release", "a", "--until-release", "b" );

		Assert.Equal( 1, both.ExitCode );
		Assert.Contains( "not both", both.Output );

		CommandAppResult neither = Run( "rollback", bundle, "--driver", "pg" );

		Assert.Equal( 1, neither.ExitCode );
		Assert.Contains( "--after-release or --until-release", neither.Output );
	}

	private CommandAppResult Run( params string[] args )
	{
		// snapshots the seeded journal state as this run's canned query results, so a test that sets
		// _journal before calling Run gets the database it described
		_journal.Attach( _driver );

		TypeRegistrar registrar = new ();

		registrar.RegisterInstance( typeof(IDatabaseDriverRegistry), _drivers );

		BundleTypeRegistry bundleTypes = new ();
		bundleTypes.RegisterBundleTypes();
		registrar.RegisterInstance( typeof(IBundleTypeRegistry), bundleTypes );

		BundleLayoutRegistry layouts = new ();
		layouts.RegisterLayouts();
		registrar.RegisterInstance( typeof(IBundleLayoutRegistry), layouts );

		CommandAppTester tester = new ( registrar );

		tester.Configure( SqetchDeployApp.Configure );

		return tester.Run( args );
	}

	/// <summary>
	/// Writes a bundle of release 2026.07 with plans 'table' (step 'create') and 'index'
	/// (step 'add') to <paramref name="name"/> under the test root. The release starts the
	/// lineage unless <paramref name="dependency"/> names the release it follows.
	/// </summary>
	private string WriteBundle(
		string name, string type = ZipBundleType.TypeName, bool withRollbackScripts = true,
		string dependency = "" )
	{
		Bundle bundle = new (
			new ProjectDefinition( "demo", "", "postgres" ),
			BundleSelection.Finalized,
			Manifests.Journaling(),
			[new BundleRelease( "2026.07", "", dependency, true )],
			[
				new BundlePlan(
					"table", "", "2026.07", [],
					[
						new BundleStep(
							"create", "", [],
							() => new StepScripts(
								Script( "create table t ()" ), Script( "-- v" ),
								withRollbackScripts ? Script( "-- r" ) : null ) )
					] ),
				new BundlePlan(
					"index", "", "2026.07", ["table"],
					[
						new BundleStep(
							"add", "", ["create"],
							() => new StepScripts(
								Script( "create index i" ), Script( "-- v" ),
								withRollbackScripts ? Script( "-- r" ) : null ) )
					] )
			] );

		string path = Path.Combine( _root, name );

		BundleTypeRegistry registry = new ();
		registry.RegisterBundleTypes();

		using( IBundleWriter writer = registry.Create( type ).Create( path ) )
			new DefaultBundleLayout().Write( bundle, writer );

		return path;
	}

	private static MemoryStream Script( string content ) => new ( Encoding.UTF8.GetBytes( content ) );

	private sealed class FakeDriverFactory( FakeDatabaseDriver driver ) : IDatabaseDriverFactory
	{
		public string Name => "pg";

		public IDictionary<string, string>? ReceivedParameters { get; private set; }

		public TimeSpan? ReceivedTimeout { get; private set; }

		public Exception? FailWith { get; set; }

		public DatabaseDriverParametersDefinition GetParametersDefinition()
			=> new (
				[
					new DatabaseDriverParameterDefinition( "host", "Database host" ),
					new DatabaseDriverParameterDefinition( "database", "Database name" ),
					new DatabaseDriverParameterDefinition( "connection-string", "Full connection string" )
				],
				[["host", "database"], ["connection-string"]] );

		public IDatabaseDriver Create( IDictionary<string, string> parameters, TimeSpan stepScriptTimeout )
		{
			if( FailWith is not null )
				throw FailWith;

			ReceivedParameters = parameters;
			ReceivedTimeout = stepScriptTimeout;

			return driver;
		}
	}
}
