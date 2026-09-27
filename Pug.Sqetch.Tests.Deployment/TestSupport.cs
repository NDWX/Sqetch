using System.Data;
using System.Text;
using Pug.Sqetch.Bundling;
using Pug.Sqetch.Deployment;
using Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

namespace Pug.Sqetch.Tests.Deployment;

/// <summary>
/// Fakes for the deployment tier. They model two different things, and a test should know which
/// one it is asserting:
/// <list type="bullet">
/// <item><description>the <em>result</em> of a deployment — the journal state a later run would
/// read back (<see cref="FakeJournalWriter"/>) and the step scripts the database actually kept
/// (<see cref="FakeDatabaseDriver.AppliedScripts"/>). Both become visible only when the
/// transaction that produced them commits, as a real database's would.</description></item>
/// <item><description>how it got there — <see cref="FakeDatabaseDriver.Events"/> for the exact
/// interleaving of scripts, journal entries and transaction boundaries, and
/// <see cref="FakeTransaction.Journaled"/> for what one transaction made durable
/// together.</description></item>
/// </list>
/// <see cref="RecordingListener"/> is neither: it is the host's progress channel.
/// </summary>
public sealed class FakeDatabaseDriver : IDatabaseDriver
{
	/// <summary>
	/// Every driver, transaction and journal call in the order it was made, committed or not.
	/// Assert against this only where the interleaving or the transaction boundaries are the
	/// subject of the test.
	/// </summary>
	public List<string> Events { get; } = [];

	/// <summary>
	/// Step scripts of committed transactions, in the order they ran — what the database kept.
	/// </summary>
	public List<string> AppliedScripts { get; } = [];

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
	private readonly List<Action> _onCommit = [];

	public int Number { get; } = number;

	public bool Committed { get; private set; }

	public bool RolledBack { get; private set; }

	/// <summary>
	/// The journal entries written in this transaction, so a test can assert what became durable
	/// together — notably that a release's completion shares its last plan's transaction.
	/// </summary>
	public List<string> Journaled { get; } = [];

	/// <summary>Registers state this transaction makes durable when, and only when, it commits.</summary>
	public void OnCommit( Action apply ) => _onCommit.Add( apply );

	public void ExecuteStepScript( string script )
	{
		if( driver.FailingScripts.Contains( script ) )
			throw new InvalidOperationException( $"script rejected: {script}" );

		driver.Events.Add( $"script #{Number} {script}" );
		OnCommit( () => driver.AppliedScripts.Add( script ) );
	}

	public void ExecuteJournalingStatement( string statement ) => driver.Events.Add( $"journal-statement #{Number} {statement}" );

	public IDataReader ExecuteJournalingQuery( string query ) => throw new NotSupportedException();

	public void Rollback()
	{
		RolledBack = true;

		// nothing this transaction wrote survives, journal entries included
		_onCommit.Clear();

		driver.Events.Add( $"rollback #{Number}" );
	}

	public void Commit()
	{
		Committed = true;

		foreach( Action apply in _onCommit )
			apply();

		_onCommit.Clear();

		driver.Events.Add( $"commit #{Number}" );
	}
}

/// <summary>
/// A journal whose state is the deployment's result: seed it to describe an earlier run, and read
/// it back afterwards to assert what this run recorded. Writes land only when their transaction
/// commits, so a rolled-back release leaves no trace.
/// </summary>
public sealed class FakeJournalWriter( FakeDatabaseDriver driver ) : IChangeJournalWriter
{
	public JournaledRelease? Latest { get; set; }

	/// <summary>
	/// Per release, the plans of it the journal holds. Order is deliberately not significant:
	/// the engine treats them as a set, so tests may seed them in any order to prove that.
	/// </summary>
	public Dictionary<string, List<string>> DeployedPlans { get; } = [];

	/// <summary>
	/// Releases whose <em>start</em> this journal recorded, in order. A release deployment
	/// resumes into is absent: the run that began it recorded that already.
	/// </summary>
	public List<string> StartedReleases { get; } = [];

	public void DeployingRelease( DeploymentUnit unit, IDatabaseTransaction transaction )
		=> Record(
			transaction, "deploying-release", unit.Name,
			() =>
			{
				StartedReleases.Add( unit.Name );
				Latest = new JournaledRelease( unit.Name, Completed: false );
			} );

	public void DeployingPlan( Plan unit, IDatabaseTransaction transaction )
		=> Record( transaction, "deploying-plan", $"{unit.Release}/{unit.Name}" );

	public void DeployingStep( Step unit, IDatabaseTransaction transaction )
		=> Record( transaction, "deploying-step", $"{unit.Release}/{unit.Plan}/{unit.Name}" );

	public void StepDeployed( string release, string plan, string name, IDatabaseTransaction transaction )
		=> Record( transaction, "step-deployed", $"{release}/{plan}/{name}" );

	public void PlanDeployed( string release, string name, IDatabaseTransaction transaction )
		=> Record( transaction, "plan-deployed", $"{release}/{name}", () => Plans( release ).Add( name ) );

	public void ReleaseDeployed( string name, IDatabaseTransaction transaction )
		=> Record(
			transaction, "release-deployed", name,
			() => Latest = new JournaledRelease( name, Completed: true ) );

	public void RollingBackRelease( string name, IDatabaseTransaction transaction ) => throw new NotSupportedException();

	public void RollingBackPlan( string release, string planname, IDatabaseTransaction transaction ) => throw new NotSupportedException();

	public void RollingBackStep( string release, string plan, string name, IDatabaseTransaction transaction ) => throw new NotSupportedException();

	public void RolledBackStep( string release, string plan, string name, IDatabaseTransaction transaction ) => throw new NotSupportedException();

	public void RolledBackPlan( string release, string name, IDatabaseTransaction transaction ) => throw new NotSupportedException();

	public void RolledBackRelease( string name, IDatabaseTransaction transaction ) => throw new NotSupportedException();

	/// <summary>Resets the journal to a fresh database, for a test that deploys more than once.</summary>
	public void Forget()
	{
		Latest = null;

		DeployedPlans.Clear();
		StartedReleases.Clear();
	}

	public JournaledRelease? GetLatestRelease( IDatabaseDriver _ ) => Latest;

	public IEnumerable<string> GetDeployedPlans( string release, IDatabaseDriver _ )
		=> DeployedPlans.TryGetValue( release, out List<string>? plans ) ? plans : [];

	private List<string> Plans( string release )
		=> DeployedPlans.TryGetValue( release, out List<string>? plans ) ? plans : DeployedPlans[release] = [];

	private void Record( IDatabaseTransaction transaction, string verb, string target, Action? durable = null )
	{
		FakeTransaction fake = (FakeTransaction)transaction;

		driver.Events.Add( $"{verb} #{fake.Number} {target}" );
		fake.Journaled.Add( $"{verb} {target}" );

		if( durable is not null )
			fake.OnCommit( durable );
	}
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
