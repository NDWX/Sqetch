using Pug.Sqetch.Deployment;
using Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

namespace Pug.Sqetch.Tests.Deployment;

public class RegistryTests
{
	[Fact]
	public void DriversResolveCaseInsensitivelyAndUnknownNamesListTheKnownOnes()
	{
		DatabaseDriverRegistry registry = new ();

		Assert.Empty( registry.Names );

		registry.Register( "pg", () => new StubDriverFactory( "pg" ) );

		Assert.True( registry.TryCreate( "PG", out IDatabaseDriverFactory? factory ) );
		Assert.Equal( "pg", factory!.Name );

		UnknownDatabaseDriverException error =
			Assert.Throws<UnknownDatabaseDriverException>( () => registry.Create( "oracle" ) );

		Assert.Equal( "oracle", error.Name );
		Assert.Contains( "pg", error.Message );
	}

	[Fact]
	public void JournalWritersResolveTheDesignatedDefaultWhenNoNameIsGiven()
	{
		ChangeJournalWriterRegistry registry = new ();
		FakeDatabaseDriver driver = new ();

		registry.Register( "table", () => new FakeJournalWriter( driver ) );
		registry.Register(
			"audit",
			() => new FakeJournalWriter( driver ) { Latest = new JournaledRelease( "marker", Completed: true ) },
			asDefault: true );

		Assert.Equal( "audit", registry.DefaultName );
		Assert.Equal( "marker", registry.Create( null ).GetLatestRelease( driver )!.Name );
	}

	[Fact]
	public void ASoleRegisteredJournalWriterIsTheImplicitDefault()
	{
		ChangeJournalWriterRegistry registry = new ();

		registry.Register( "table", () => new FakeJournalWriter( new FakeDatabaseDriver() ) );

		Assert.Null( registry.DefaultName );
		Assert.True( registry.TryCreate( null, out _ ) );
	}

	[Fact]
	public void AmbiguousOrUnknownJournalWriterResolutionFails()
	{
		ChangeJournalWriterRegistry registry = new ();
		FakeDatabaseDriver driver = new ();

		registry.Register( "table", () => new FakeJournalWriter( driver ) );
		registry.Register( "audit", () => new FakeJournalWriter( driver ) );

		UnknownChangeJournalWriterException ambiguous =
			Assert.Throws<UnknownChangeJournalWriterException>( () => registry.Create( null ) );

		Assert.Null( ambiguous.Name );
		Assert.Contains( "--journal", ambiguous.Message );

		UnknownChangeJournalWriterException unknown =
			Assert.Throws<UnknownChangeJournalWriterException>( () => registry.Create( "file" ) );

		Assert.Equal( "file", unknown.Name );
		Assert.Contains( "table", unknown.Message );
	}

	private sealed class StubDriverFactory( string name ) : IDatabaseDriverFactory
	{
		public string Name => name;

		public DatabaseDriverParametersDefinition GetParametersDefinition() => new ( [], [] );

		public IDatabaseDriver Create( IDictionary<string, string> parameters, TimeSpan stepScriptTimeout )
			=> new FakeDatabaseDriver();
	}
}
