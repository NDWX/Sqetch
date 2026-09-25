using Pug.Sqetch.Bundling;
using Pug.Sqetch.Deployment;
using Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;
using static Pug.Sqetch.Tests.Deployment.Manifests;

namespace Pug.Sqetch.Tests.Deployment;

public class DeploymentEngineTests
{
	private readonly DefaultBundleLayout _layout = new ();
	private readonly FakeDatabaseDriver _driver = new ();
	private readonly RecordingListener _listener = new ();
	private readonly FakeJournalWriter _journal;

	public DeploymentEngineTests() => _journal = new FakeJournalWriter( _driver );

	[Fact]
	public void FreshDatabaseDeploysEverythingInterleavingJournalEventsAndScripts()
	{
		BundleManifest manifest = Manifest(
			[Release( "r1" )],
			[Plan( "alpha", "r1", "s1", "s2" ), Plan( "beta", "r1", "s1" )] );

		Deploy( manifest, DeploymentCommitLevel.Release );

		Assert.Equal(
			[
				"begin #1",
				"deploying-release #1 r1",
				"deploying-plan #1 r1/alpha",
				"deploying-step #1 r1/alpha/s1",
				"script #1 -- deploy alpha/s1",
				"step-deployed #1 r1/alpha/s1",
				"deploying-step #1 r1/alpha/s2",
				"script #1 -- deploy alpha/s2",
				"step-deployed #1 r1/alpha/s2",
				"plan-deployed #1 r1/alpha",
				"deploying-plan #1 r1/beta",
				"deploying-step #1 r1/beta/s1",
				"script #1 -- deploy beta/s1",
				"step-deployed #1 r1/beta/s1",
				"plan-deployed #1 r1/beta",
				"release-deployed #1 r1",
				"commit #1"
			],
			_driver.Events );

		Assert.DoesNotContain( "nothing", _listener.Events );
		Assert.Equal( "committed", _listener.Events.Last() );
	}

	[Fact]
	public void ReleaseCommitLevelCommitsOncePerRelease()
	{
		BundleManifest manifest = Manifest(
			[Release( "r1" ), Release( "r2", "r1" )],
			[Plan( "alpha", "r1", "s1" ), Plan( "beta", "r2", "s1" )] );

		Deploy( manifest, DeploymentCommitLevel.Release );

		Assert.Equal(
			["begin #1", "commit #1", "begin #2", "commit #2"],
			_driver.Events.Where( x => x.StartsWith( "begin" ) || x.StartsWith( "commit" ) ) );

		// each release's completion is journaled inside that release's transaction
		Assert.Contains( "release-deployed #1 r1", _driver.Events );
		Assert.Contains( "release-deployed #2 r2", _driver.Events );
	}

	[Fact]
	public void PlanCommitLevelCommitsBetweenPlansAndJournalsTheReleaseWithItsLastPlan()
	{
		BundleManifest manifest = Manifest(
			[Release( "r1" )],
			[Plan( "alpha", "r1", "s1" ), Plan( "beta", "r1", "s1" )] );

		Deploy( manifest, DeploymentCommitLevel.Plan );

		Assert.Equal( "plan-deployed #1 r1/alpha", _driver.Events[_driver.Events.IndexOf( "commit #1" ) - 1] );
		Assert.Contains( "plan-deployed #2 r1/beta", _driver.Events );
		Assert.Contains( "release-deployed #2 r1", _driver.Events );
		Assert.Equal( "commit #2", _driver.Events.Last() );
	}

	[Fact]
	public void ResumesAfterTheJournaledReleaseAndPlans()
	{
		BundleManifest manifest = Manifest(
			[Release( "r0" ), Release( "r1", "r0" ), Release( "r2", "r1" )],
			[Plan( "p0", "r0", "s1" ), Plan( "a", "r1", "s1" ), Plan( "b", "r1", "s1" ), Plan( "c", "r2", "s1" )] );

		_journal.Latest = new JournaledRelease( "r1", Complete: false );
		_journal.DeployedPlans["r1"] = ["a"];

		Deploy( manifest, DeploymentCommitLevel.Release );

		Assert.Contains( "skip-release r0", _listener.Events );
		Assert.Contains( "skip-plan r1/a", _listener.Events );

		// the partially deployed release resumes without journaling its start again
		Assert.DoesNotContain( _driver.Events, x => x.StartsWith( "deploying-release" ) && x.EndsWith( " r1" ) );
		Assert.DoesNotContain( _driver.Events, x => x.Contains( "p0" ) || x.Contains( "r1/a" ) );
		Assert.Contains( "deploying-plan #1 r1/b", _driver.Events );
		Assert.Contains( "release-deployed #1 r1", _driver.Events );
		Assert.Contains( "deploying-release #2 r2", _driver.Events );
		Assert.Contains( "release-deployed #2 r2", _driver.Events );
	}

	[Fact]
	public void FullyDeployedLatestReleaseIsSkippedEntirely()
	{
		BundleManifest manifest = Manifest(
			[Release( "r1" ), Release( "r2", "r1" )],
			[Plan( "a", "r1", "s1" ), Plan( "b", "r2", "s1" )] );

		_journal.Latest = new JournaledRelease( "r1", Complete: true );
		_journal.DeployedPlans["r1"] = ["a"];

		Deploy( manifest, DeploymentCommitLevel.Release );

		Assert.Contains( "skip-release r1", _listener.Events );
		Assert.DoesNotContain( _driver.Events, x => x.Contains( "r1" ) );
		Assert.Contains( "deploying-release #1 r2", _driver.Events );
	}

	/// <summary>
	/// A completed release is handed over from, never resumed into, so plans a bundle gained for
	/// one are not deployed. Only an open release can gain plans — finalized ones are frozen —
	/// and a database that has deployed unreleased plans is single-use by design: the next test
	/// deployment runs against a restored backup.
	/// </summary>
	[Fact]
	public void ACompletedReleaseIsNotRevisitedEvenWhenTheBundleGainedPlansForIt()
	{
		BundleManifest manifest = Manifest(
			[Release( "r1" )],
			[Plan( "a", "r1", "s1" ), Plan( "b", "r1", "s1" )] );

		_journal.Latest = new JournaledRelease( "r1", Complete: true );
		_journal.DeployedPlans["r1"] = ["a"];

		Deploy( manifest, DeploymentCommitLevel.Release );

		Assert.Equal( ["skip-release r1", "nothing"], _listener.Events );
		Assert.Empty( _driver.Events );
	}

	[Fact]
	public void AnIncompleteReleaseWhoseLastDeployedPlanEndsItHasItsCompletionJournaled()
	{
		BundleManifest manifest = Manifest(
			[Release( "r1" ), Release( "r2", "r1" )],
			[Plan( "a", "r1", "s1" ), Plan( "b", "r2", "s1" )] );

		// every plan of r1 is deployed, but its completion never was
		_journal.Latest = new JournaledRelease( "r1", Complete: false );
		_journal.DeployedPlans["r1"] = ["a"];

		Deploy( manifest, DeploymentCommitLevel.Release );

		Assert.Equal(
			[
				"begin #1",
				"release-deployed #1 r1",
				"commit #1",
				"begin #2",
				"deploying-release #2 r2",
				"deploying-plan #2 r2/b",
				"deploying-step #2 r2/b/s1",
				"script #2 -- deploy b/s1",
				"step-deployed #2 r2/b/s1",
				"plan-deployed #2 r2/b",
				"release-deployed #2 r2",
				"commit #2"
			],
			_driver.Events );

		Assert.DoesNotContain( "nothing", _listener.Events );
	}

	[Fact]
	public void AnIncompleteReleaseWithNoDeployedPlanDeploysFromItsFirstPlan()
	{
		BundleManifest manifest = Manifest(
			[Release( "r1" ), Release( "r2", "r1" )],
			[Plan( "a", "r1", "s1" ), Plan( "b", "r2", "s1" )] );

		// journaled as started with nothing committed, which only a writer journaling outside
		// the deployment transaction can produce
		_journal.Latest = new JournaledRelease( "r1", Complete: false );

		Deploy( manifest, DeploymentCommitLevel.Release );

		Assert.Contains( "deploying-plan #1 r1/a", _driver.Events );
		Assert.Contains( "deploying-plan #2 r2/b", _driver.Events );
		Assert.DoesNotContain( "skip-plan r1/a", _listener.Events );
	}

	[Fact]
	public void AJournaledLastPlanMissingFromTheBundleIsRefused()
	{
		BundleManifest manifest = Manifest( [Release( "r1" )], [Plan( "a", "r1", "s1" )] );

		// the bundle was rebuilt without the plan the database stopped at
		_journal.Latest = new JournaledRelease( "r1", Complete: false );
		_journal.DeployedPlans["r1"] = ["scrapped"];

		IncompatibleBundleException error = Assert.Throws<IncompatibleBundleException>(
			() => Deploy( manifest, DeploymentCommitLevel.Release ) );

		Assert.Contains( "last deployed plan 'scrapped'", error.Message );
		Assert.Contains( "restore the database", error.Message );
		Assert.Empty( _driver.Events );
	}

	/// <summary>
	/// Nothing can depend on the pseudo-release, so a bundle that starts the lineage must not be
	/// matched to it through their shared empty dependency and redeployed over a test database.
	/// </summary>
	[Fact]
	public void ADatabaseLeftAtThePseudoReleaseCannotBeFollowedByALineageStartingBundle()
	{
		BundleManifest manifest = Manifest( [Release( "r1" )], [Plan( "a", "r1", "s1" )] );

		_journal.Latest = new JournaledRelease( "", Complete: true );

		IncompatibleBundleException error = Assert.Throws<IncompatibleBundleException>(
			() => Deploy( manifest, DeploymentCommitLevel.Release ) );

		Assert.Contains( "the unreleased-plans pseudo-release", error.Message );
		Assert.Contains( "neither contains nor continues from", error.Message );
		Assert.Empty( _driver.Events );
	}

	[Fact]
	public void UnreleasedPlansAreReachedThroughTheDependencyChain()
	{
		BundleManifest manifest = Manifest(
			[Release( "r1" )],
			[Plan( "a", "r1", "s1" ), Plan( "z", "", "s1" )] );

		_journal.Latest = new JournaledRelease( "r1", Complete: true );
		_journal.DeployedPlans["r1"] = ["a"];

		Deploy( manifest, DeploymentCommitLevel.Release );

		// the pseudo-release depends on the last real release, so it is what follows r1
		Assert.Contains( "skip-release r1", _listener.Events );
		Assert.Contains( "deploying-plan #1 /z", _driver.Events );
		Assert.Contains( "release-deployed #1 ", _driver.Events );
		Assert.DoesNotContain( _driver.Events, x => x.Contains( "r1/a" ) );
	}

	[Fact]
	public void ContinuationBundleFollowingACompletelyDeployedReleaseIsDeployedWhole()
	{
		BundleManifest manifest = Manifest( [Release( "r2", "r1" )], [Plan( "c", "r2", "s1" )] );

		_journal.Latest = new JournaledRelease( "r1", Complete: true );

		// the bundle's own release is not the journaled one, so its deployed plans are never
		// consulted: 'c' deploys even though the journal claims it
		_journal.DeployedPlans["r2"] = ["c"];

		Deploy( manifest, DeploymentCommitLevel.Release );

		Assert.Equal(
			[
				"begin #1",
				"deploying-release #1 r2",
				"deploying-plan #1 r2/c",
				"deploying-step #1 r2/c/s1",
				"script #1 -- deploy c/s1",
				"step-deployed #1 r2/c/s1",
				"plan-deployed #1 r2/c",
				"release-deployed #1 r2",
				"commit #1"
			],
			_driver.Events );

		// the continuation is announced once and nothing is skipped
		Assert.Equal(
			[
				"continuing r1",
				"release r2",
				"plan r2/c",
				"step r2/c/s1",
				"step-done r2/c/s1",
				"plan-done r2/c",
				"release-done r2",
				"committed"
			],
			_listener.Events );
	}

	[Fact]
	public void ContinuationBundleIsRefusedWhenThePredecessorIsOnlyPartiallyDeployed()
	{
		BundleManifest manifest = Manifest( [Release( "r2", "r1" )], [Plan( "c", "r2", "s1" )] );

		_journal.Latest = new JournaledRelease( "r1", Complete: false );

		IncompatibleBundleException error = Assert.Throws<IncompatibleBundleException>(
			() => Deploy( manifest, DeploymentCommitLevel.Release ) );

		Assert.Contains( "release 'r1' is only partially deployed", error.Message );
		Assert.Contains( "release 'r2'", error.Message );
		Assert.Equal( new JournaledRelease( "r1", Complete: false ), error.Journaled );

		Assert.Empty( _driver.Events );
		Assert.Empty( _listener.Events );
	}

	[Fact]
	public void ContinuationBundleIsRefusedOnAFreshDatabase()
	{
		BundleManifest manifest = Manifest( [Release( "r2", "r1" )], [Plan( "c", "r2", "s1" )] );

		IncompatibleBundleException error = Assert.Throws<IncompatibleBundleException>(
			() => Deploy( manifest, DeploymentCommitLevel.Release ) );

		Assert.Contains( "continues from release 'r1'", error.Message );
		Assert.Contains( "no deployed releases", error.Message );
		Assert.Null( error.Journaled );

		Assert.Empty( _driver.Events );
		Assert.Empty( _listener.Events );
	}

	[Fact]
	public void APartiallyDeployedReleaseContainedInTheBundleResumesInstead()
	{
		BundleManifest manifest = Manifest(
			[Release( "r1" ), Release( "r2", "r1" )],
			[Plan( "a", "r1", "s1" ), Plan( "b", "r1", "s1" ), Plan( "c", "r2", "s1" )] );

		_journal.Latest = new JournaledRelease( "r1", Complete: false );
		_journal.DeployedPlans["r1"] = ["a"];

		Deploy( manifest, DeploymentCommitLevel.Release );

		Assert.Contains( "skip-plan r1/a", _listener.Events );
		Assert.DoesNotContain( "continuing r1", _listener.Events );
		Assert.Contains( "deploying-plan #1 r1/b", _driver.Events );
		Assert.Contains( "release-deployed #1 r1", _driver.Events );
		Assert.Contains( "deploying-release #2 r2", _driver.Events );
	}

	[Fact]
	public void JournaledReleaseTheBundleNeitherContainsNorContinuesFromIsRefused()
	{
		BundleManifest manifest = Manifest( [Release( "r1" )], [Plan( "a", "r1", "s1" )] );

		_journal.Latest = new JournaledRelease( "r9", Complete: true );

		IncompatibleBundleException error = Assert.Throws<IncompatibleBundleException>(
			() => Deploy( manifest, DeploymentCommitLevel.Release ) );

		Assert.Equal(
			"database is at release 'r9', which this bundle neither contains nor continues from.",
			error.Message );
		Assert.Equal( "r9", error.Journaled!.Name );
		Assert.Empty( _driver.Events );
	}

	[Fact]
	public void ReleasesThatDoNotFormAContiguousChainAreRefused()
	{
		BundleManifest manifest = Manifest(
			[Release( "r0" ), Release( "r2", "r1" )],
			[Plan( "a", "r0", "s1" ), Plan( "c", "r2", "s1" )] );

		InvalidBundleException error = Assert.Throws<InvalidBundleException>(
			() => Deploy( manifest, DeploymentCommitLevel.Release ) );

		Assert.Equal(
			"Release 'r2' does not depend on 'r0', the release preceding it in the bundle.",
			error.Message );
		Assert.Empty( _driver.Events );
	}

	[Fact]
	public void UnreleasedPlansDeployLastAsThePseudoRelease()
	{
		BundleManifest manifest = Manifest(
			[Release( "r1" )],
			[Plan( "a", "r1", "s1" ), Plan( "z", "", "s1" )] );

		Deploy( manifest, DeploymentCommitLevel.Release );

		Assert.Contains( "deploying-release #2 ", _driver.Events );
		Assert.Contains( "deploying-plan #2 /z", _driver.Events );
		Assert.Contains( "release-deployed #2 ", _driver.Events );
		Assert.True(
			_driver.Events.IndexOf( "release-deployed #1 r1" ) < _driver.Events.IndexOf( "deploying-plan #2 /z" ) );
	}

	[Fact]
	public void ResumesInsideThePseudoRelease()
	{
		BundleManifest manifest = Manifest(
			[Release( "r1" )],
			[Plan( "a", "r1", "s1" ), Plan( "y", "", "s1" ), Plan( "z", "", "s1" )] );

		_journal.Latest = new JournaledRelease( "", Complete: false );
		_journal.DeployedPlans[""] = ["y"];

		Deploy( manifest, DeploymentCommitLevel.Release );

		Assert.Contains( "skip-release r1", _listener.Events );
		Assert.Contains( "skip-plan /y", _listener.Events );
		Assert.Contains( "deploying-plan #1 /z", _driver.Events );
		Assert.Contains( "release-deployed #1 ", _driver.Events );
	}

	[Fact]
	public void StepFailureRollsBackTheOpenTransactionAndKeepsEarlierCommits()
	{
		BundleManifest manifest = Manifest(
			[Release( "r1" ), Release( "r2", "r1" )],
			[Plan( "a", "r1", "s1" ), Plan( "b", "r2", "s1" )] );

		_driver.FailingScripts.Add( "-- deploy b/s1" );

		StepScriptFailedException error = Assert.Throws<StepScriptFailedException>(
			() => Deploy( manifest, DeploymentCommitLevel.Release ) );

		Assert.Equal( ( "r2", "b", "s1" ), (error.Release, error.Plan, error.Step) );

		Assert.True( _driver.Transactions[0].Committed );
		Assert.True( _driver.Transactions[1].RolledBack );
		Assert.False( _driver.Transactions[1].Committed );
		Assert.Equal( "rollback #2", _driver.Events.Last() );

		// no completion events for the failed step, plan or release
		Assert.DoesNotContain( _driver.Events, x => x.StartsWith( "step-deployed" ) && x.Contains( "r2" ) );
		Assert.DoesNotContain( _driver.Events, x => x.StartsWith( "plan-deployed" ) && x.Contains( "r2" ) );
		Assert.DoesNotContain( _driver.Events, x => x.StartsWith( "release-deployed" ) && x.Contains( "r2" ) );
	}

	[Fact]
	public void UpToDateDatabaseTouchesNoTransactionAndReportsNothingToDeploy()
	{
		BundleManifest manifest = Manifest( [Release( "r1" )], [Plan( "a", "r1", "s1" )] );

		_journal.Latest = new JournaledRelease( "r1", Complete: true );
		_journal.DeployedPlans["r1"] = ["a"];

		Deploy( manifest, DeploymentCommitLevel.Release );

		Assert.Empty( _driver.Events );
		Assert.Equal( ["skip-release r1", "nothing"], _listener.Events );
	}

	[Fact]
	public void PlanReferencingAnUnlistedReleaseIsRefused()
	{
		BundleManifest manifest = Manifest( [Release( "r1" )], [Plan( "a", "r9", "s1" )] );

		InvalidBundleException error = Assert.Throws<InvalidBundleException>(
			() => Deploy( manifest, DeploymentCommitLevel.Release ) );

		Assert.Contains( "'r9'", error.Message );
	}

	private void Deploy( BundleManifest manifest, DeploymentCommitLevel commitLevel )
		=> new DeploymentEngine(
				_driver, _journal, ReaderFor( manifest, _layout ), _layout, commitLevel, _listener )
			.Deploy( manifest );
}
