using System.Data;
using System.Text;
using Pug.Sqetch.Bundling;
using Pug.Sqetch.Deployment;
using Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

namespace Pug.Sqetch.Tests.Deployment;

/// <summary>
/// Fakes for the deployment tier: driver, transactions and journal writer share one
/// ordered event log so tests can assert the exact interleaving of scripts, journal
/// entries and transaction boundaries.
/// </summary>
public sealed class FakeDatabaseDriver : IDatabaseDriver
{
	public List<string> Events { get; } = [];

	/// <summary>Scripts whose execution should fail.</summary>
	public HashSet<string> FailingScripts { get; } = [];

	public List<FakeTransaction> Transactions { get; } = [];

	public IDatabaseTransaction BeginTransaction()
	{
		FakeTransaction transaction = new ( this, Transactions.Count + 1 );

		Transactions.Add( transaction );
		Events.Add( $"begin #{transaction.Number}" );

		return transaction;
	}

	public void ExecuteJournalingStatement( string statement ) => Events.Add( $"journal-statement {statement}" );

	public IDataReader ExecuteJournalingQuery( string query ) => throw new NotSupportedException();
}

public sealed class FakeTransaction( FakeDatabaseDriver driver, int number ) : IDatabaseTransaction
{
	public int Number { get; } = number;

	public bool Committed { get; private set; }

	public bool RolledBack { get; private set; }

	public void ExecuteStepScript( string script )
	{
		if( driver.FailingScripts.Contains( script ) )
			throw new InvalidOperationException( $"script rejected: {script}" );

		driver.Events.Add( $"script #{Number} {script}" );
	}

	public void ExecuteJournalingStatement( string statement ) => driver.Events.Add( $"journal-statement #{Number} {statement}" );

	public IDataReader ExecuteJournalingQuery( string query ) => throw new NotSupportedException();

	public void Rollback()
	{
		RolledBack = true;
		driver.Events.Add( $"rollback #{Number}" );
	}

	public void Commit()
	{
		Committed = true;
		driver.Events.Add( $"commit #{Number}" );
	}
}

public sealed class FakeJournalWriter( FakeDatabaseDriver driver ) : IChangeJournalWriter
{
	public JournaledRelease? Latest { get; set; }

	/// <summary>
	/// Per release, its deployed plans in deployment order, oldest first — the order
	/// <see cref="IChangeJournalWriter.GetDeployedPlans"/> promises, and the engine resumes at
	/// the plan following the last of them.
	/// </summary>
	public Dictionary<string, List<string>> DeployedPlans { get; } = [];

	public void DeployingRelease( DeploymentUnit unit, IDatabaseTransaction transaction )
		=> driver.Events.Add( $"deploying-release {Tx( transaction )} {unit.Name}" );

	public void DeployingPlan( Plan unit, IDatabaseTransaction transaction )
		=> driver.Events.Add( $"deploying-plan {Tx( transaction )} {unit.Release}/{unit.Name}" );

	public void DeployingStep( Step unit, IDatabaseTransaction transaction )
		=> driver.Events.Add( $"deploying-step {Tx( transaction )} {unit.Release}/{unit.Plan}/{unit.Name}" );

	public void StepDeployed( string release, string plan, string name, IDatabaseTransaction transaction )
		=> driver.Events.Add( $"step-deployed {Tx( transaction )} {release}/{plan}/{name}" );

	public void PlanDeployed( string release, string name, IDatabaseTransaction transaction )
		=> driver.Events.Add( $"plan-deployed {Tx( transaction )} {release}/{name}" );

	public void ReleaseDeployed( string name, IDatabaseTransaction transaction )
		=> driver.Events.Add( $"release-deployed {Tx( transaction )} {name}" );

	public void RollingBackRelease( string name, IDatabaseTransaction transaction ) => throw new NotSupportedException();

	public void RollingBackPlan( string release, string planname, IDatabaseTransaction transaction ) => throw new NotSupportedException();

	public void RollingBackStep( string release, string plan, string name, IDatabaseTransaction transaction ) => throw new NotSupportedException();

	public void RolledBackStep( string release, string plan, string name, IDatabaseTransaction transaction ) => throw new NotSupportedException();

	public void RolledBackPlan( string release, string name, IDatabaseTransaction transaction ) => throw new NotSupportedException();

	public void RolledBackRelease( string name, IDatabaseTransaction transaction ) => throw new NotSupportedException();

	public JournaledRelease? GetLatestRelease( IDatabaseDriver _ ) => Latest;

	public IEnumerable<string> GetDeployedPlans( string release, IDatabaseDriver _ )
		=> DeployedPlans.TryGetValue( release, out List<string>? plans ) ? plans : [];

	private static string Tx( IDatabaseTransaction transaction ) => $"#{( (FakeTransaction)transaction ).Number}";
}

public sealed class RecordingListener : IDeploymentListener
{
	public List<string> Events { get; } = [];

	public void ContinuingFrom( string release ) => Events.Add( $"continuing {release}" );

	public void SkippingRelease( string release ) => Events.Add( $"skip-release {release}" );

	public void SkippingPlan( string release, string plan ) => Events.Add( $"skip-plan {release}/{plan}" );

	public void DeployingRelease( string release ) => Events.Add( $"release {release}" );

	public void DeployingPlan( string release, string plan ) => Events.Add( $"plan {release}/{plan}" );

	public void DeployingStep( string release, string plan, string step ) => Events.Add( $"step {release}/{plan}/{step}" );

	public void StepDeployed( string release, string plan, string step ) => Events.Add( $"step-done {release}/{plan}/{step}" );

	public void PlanDeployed( string release, string plan ) => Events.Add( $"plan-done {release}/{plan}" );

	public void ReleaseDeployed( string release ) => Events.Add( $"release-done {release}" );

	public void Committed() => Events.Add( "committed" );

	public void NothingToDeploy() => Events.Add( "nothing" );
}

public sealed class InMemoryBundleReader : IBundleReader
{
	private readonly Dictionary<string, string> _entries = new ();

	public InMemoryBundleReader Add( string path, string content )
	{
		_entries[path] = content;

		return this;
	}

	public InMemoryBundleReader Remove( string path )
	{
		_entries.Remove( path );

		return this;
	}

	public IEnumerable<string> Entries => _entries.Keys;

	public bool Contains( string path ) => _entries.ContainsKey( path );

	public Stream Open( string path )
		=> _entries.TryGetValue( path, out string? content )
			? new MemoryStream( Encoding.UTF8.GetBytes( content ) )
			: throw new BundlingException( $"Bundle has no entry '{path}'." );

	public void Dispose()
	{
	}
}

public static class Manifests
{
	public static BundleManifestStep Step( string name ) => new ( name, "", [] );

	public static BundleManifestPlan Plan( string name, string release, params string[] steps )
		=> new ( name, "", release, [], steps.Select( Step ).ToList() );

	public static BundleManifestRelease Release( string name, string dependency = "" )
		=> new ( name, "", dependency, true );

	public static BundleManifest Manifest(
		IReadOnlyList<BundleManifestRelease> releases, IReadOnlyList<BundleManifestPlan> plans )
		=> new ( new BundleManifestProject( "demo", "", "postgres" ), BundleSelection.Finalized,
				DateTime.Now, releases, plans );

	/// <summary>A reader holding all three scripts for every step of the manifest.</summary>
	public static InMemoryBundleReader ReaderFor( BundleManifest manifest, IBundleLayout layout )
	{
		InMemoryBundleReader reader = new ();

		foreach( BundleManifestPlan plan in manifest.Plans )
			foreach( BundleManifestStep step in plan.Steps )
				foreach( StepScriptKind kind in Enum.GetValues<StepScriptKind>() )
					reader.Add(
						layout.ScriptPath( plan.Name, step.Name, kind ),
						$"-- {kind.ToString().ToLowerInvariant()} {plan.Name}/{step.Name}" );

		return reader;
	}
}
