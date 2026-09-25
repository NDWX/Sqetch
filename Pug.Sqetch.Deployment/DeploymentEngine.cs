using Pug.Sqetch.Bundling;
using Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

namespace Pug.Sqetch.Deployment;

/// <summary>
/// Executes a bundle's deploy scripts in manifest order against a database, journaling every
/// event through the change journal writer and starting after whatever the journal says is
/// already deployed. Where that start lies follows from the journal alone: nothing journaled
/// deploys the bundle whole, an <em>incompletely</em> deployed release resumes inside itself
/// (so the bundle must contain it), and a <em>completely</em> deployed release hands over to
/// whichever release depends on it — which may be a release the bundle does not contain,
/// making this a continuation bundle.
/// Transactions are committed at plan or release boundaries per
/// <see cref="DeploymentCommitLevel"/>; a release's completion is always journaled in the
/// same transaction as its last plan. On failure the open transaction is rolled back and
/// the failure rethrown — earlier boundary commits stand.
/// </summary>
public class DeploymentEngine(
	IDatabaseDriver driver,
	IChangeJournalWriter journal,
	IBundleReader reader,
	IBundleLayout layout,
	DeploymentCommitLevel commitLevel,
	IDeploymentListener listener )
{
	public void Deploy( BundleManifest manifest )
	{
		List<ReleaseGroup> groups = Group( manifest );

		JournaledRelease? latest = journal.GetLatestRelease( driver );

		if( latest is null )
		{
			Deploy( groups, lastGroup: null, lastPlan: null );

			return;
		}

		if( latest.Complete )
		{
			Deploy( groups, latest.Name, lastPlan: null, latest );

			return;
		}

		// an incompletely deployed release must finish before any later release, so a bundle
		// that does not contain it cannot be deployed at all
		ReleaseGroup? incomplete = groups.Find(
			group => string.Equals( group.Name, latest.Name, StringComparison.Ordinal ) );

		if( incomplete is null )
			throw new IncompatibleBundleException(
				latest,
				$"{Describe( latest.Name )} is only partially deployed; deploy a bundle that contains it "
				+ $"so its remaining plans deploy{( groups.Count > 0 ? $" before {Describe( groups[0].Name )}" : "" )}." );

		string? lastDeployedPlan = journal.GetDeployedPlans( latest.Name, driver ).LastOrDefault();

		// its start was journaled but no plan of it completed, which only a writer journaling
		// outside the deployment transaction can produce: resume at its first plan by starting
		// from the release it follows rather than from itself
		if( lastDeployedPlan is null )
			Deploy( groups, incomplete.Dependency.Length > 0 ? incomplete.Dependency : null, null, latest );
		else
			Deploy( groups, latest.Name, lastDeployedPlan, latest );
	}

	/// <summary>
	/// Deploys the groups the database has not seen yet. <paramref name="lastPlan"/> resumes
	/// inside the release holding it; <paramref name="lastGroup"/> alone hands over to the
	/// group depending on it; neither starts at the bundle's first group.
	/// <paramref name="journaled"/> is diagnostic context for refusals only.
	/// </summary>
	private void Deploy(
		List<ReleaseGroup> groups, string? lastGroup, string? lastPlan, JournaledRelease? journaled = null )
	{
		StartPoint? start = ResolveStart( groups, lastGroup, lastPlan, journaled );

		if( start is null )
		{
			listener.NothingToDeploy();

			return;
		}

		IDatabaseTransaction? transaction = null;
		bool deployedAnything = false;

		IDatabaseTransaction Transaction() => transaction ??= driver.BeginTransaction();

		void CommitBoundary()
		{
			if( transaction is null )
				return;

			transaction.Commit();
			listener.Committed();

			transaction = null;
		}

		try
		{
			for( int index = start.Group; index < groups.Count; index++ )
			{
				ReleaseGroup group = groups[index];

				// only the group deployment resumes into carries skipped plans, and its start
				// was journaled by the run that began it
				bool resumed = index == start.Group && start.Resumed;
				int skipped = resumed ? start.SkippedPlans : 0;

				for( int position = 0; position < skipped; position++ )
					listener.SkippingPlan( group.Name, group.Plans[position].Name );

				List<BundleManifestPlan> pending =
					skipped == 0 ? group.Plans : group.Plans.Skip( skipped ).ToList();

				deployedAnything = true;

				if( !resumed )
				{
					listener.DeployingRelease( group.Name );
					journal.DeployingRelease( new DeploymentUnit( group.Name, group.Description ), Transaction() );
				}

				for( int position = 0; position < pending.Count; position++ )
				{
					DeployPlan( group.Name, pending[position], Transaction, listener );

					if( commitLevel == DeploymentCommitLevel.Plan && position < pending.Count - 1 )
						CommitBoundary();
				}

				// reached with nothing pending when the journal's last plan is the release's
				// last: journaling the completion closes a release that would otherwise stay
				// incomplete forever and block every later bundle
				journal.ReleaseDeployed( group.Name, Transaction() );
				listener.ReleaseDeployed( group.Name );

				CommitBoundary();
			}

			if( !deployedAnything )
				listener.NothingToDeploy();
		}
		catch
		{
			transaction?.Rollback();

			throw;
		}
	}

	/// <summary>
	/// Where deployment starts, or null when the database is already at the end of the bundle.
	/// Reports the releases and plans skipped on the way, and refuses a bundle that does not
	/// meet the database where it stands.
	/// </summary>
	private StartPoint? ResolveStart(
		List<ReleaseGroup> groups, string? lastGroup, string? lastPlan, JournaledRelease? journaled )
	{
		if( lastPlan is not null )
		{
			int holding = groups.FindIndex(
				group => group.Plans.Any( plan => string.Equals( plan.Name, lastPlan, StringComparison.Ordinal ) ) );

			if( holding < 0 )
				throw new IncompatibleBundleException(
					journaled,
					$"the database's last deployed plan '{lastPlan}'"
					+ $"{( lastGroup is null ? "" : $" of {Describe( lastGroup )}" )} is not in this bundle; "
					+ "deploy a bundle that contains it, or restore the database." );

			InformSkip( groups, holding );

			return new StartPoint(
				holding,
				groups[holding].Plans.FindIndex(
					plan => string.Equals( plan.Name, lastPlan, StringComparison.Ordinal ) ) + 1,
				Resumed: true );
		}

		if( lastGroup is null )
		{
			// a continuation bundle cannot be the first thing a database ever sees
			if( groups.Count > 0 && groups[0].Dependency.Length > 0 )
				throw new IncompatibleBundleException(
					null,
					$"this bundle continues from {Describe( groups[0].Dependency )} "
					+ "but the database has no deployed releases." );

			return new StartPoint( 0, 0, Resumed: false );
		}

		// an empty name is the unreleased-plans pseudo-release, which nothing can depend on —
		// matching it here would match the first release of any lineage-starting bundle, whose
		// dependency is empty too, and redeploy the bundle over a database it does not fit
		if( lastGroup.Length > 0 )
		{
			int dependant = groups.FindIndex(
				group => string.Equals( group.Dependency, lastGroup, StringComparison.Ordinal ) );

			if( dependant >= 0 )
			{
				if( !groups.Any( group => string.Equals( group.Name, lastGroup, StringComparison.Ordinal ) ) )
					listener.ContinuingFrom( lastGroup );

				InformSkip( groups, dependant );

				return new StartPoint( dependant, 0, Resumed: false );
			}
		}

		// nothing in the bundle follows the journaled release: it is either the bundle's own
		// end, or a release this bundle has nothing to do with
		if( groups.Count > 0 && string.Equals( groups[^1].Name, lastGroup, StringComparison.Ordinal ) )
		{
			InformSkip( groups, groups.Count );

			return null;
		}

		throw new IncompatibleBundleException(
			journaled,
			$"database is at {Describe( lastGroup )}, which this bundle neither contains nor continues from." );
	}

	private void InformSkip( List<ReleaseGroup> groups, int upTo )
	{
		for( int index = 0; index < upTo; index++ )
			listener.SkippingRelease( groups[index].Name );
	}

	private void DeployPlan(
		string release, BundleManifestPlan plan,
		Func<IDatabaseTransaction> transaction, IDeploymentListener listener )
	{
		listener.DeployingPlan( release, plan.Name );
		journal.DeployingPlan( new Plan( release, plan.Name, plan.Description ), transaction() );

		foreach( BundleManifestStep step in plan.Steps )
		{
			listener.DeployingStep( release, plan.Name, step.Name );
			journal.DeployingStep( new Step( release, plan.Name, step.Name, step.Description ), transaction() );

			string script = ReadDeployScript( plan.Name, step.Name );

			try
			{
				transaction().ExecuteStepScript( script );
			}
			catch( Exception exception )
			{
				throw new StepScriptFailedException( release, plan.Name, step.Name, exception );
			}

			journal.StepDeployed( release, plan.Name, step.Name, transaction() );
			listener.StepDeployed( release, plan.Name, step.Name );
		}

		journal.PlanDeployed( release, plan.Name, transaction() );
		listener.PlanDeployed( release, plan.Name );
	}

	private string ReadDeployScript( string plan, string step )
	{
		using Stream entry = reader.Open( layout.ScriptPath( plan, step, StepScriptKind.Deploy ) );
		using StreamReader content = new ( entry );

		return content.ReadToEnd();
	}

	/// <summary>Renders a release name for a message; mirrors the CLI listener's wording.</summary>
	private static string Describe( string release )
		=> release.Length == 0 ? "the unreleased-plans pseudo-release" : $"release '{release}'";

	/// <summary>
	/// Releases in manifest order, each with its plans in manifest order; unreleased plans
	/// (empty release name) form a trailing pseudo-release named "" which depends on the last
	/// real release in the bundle, so it is a link in the chain like any other and a completed
	/// release hands over to it. The real releases must form a contiguous chain: each names the
	/// one preceding it in the bundle.
	/// </summary>
	private static List<ReleaseGroup> Group( BundleManifest manifest )
	{
		List<ReleaseGroup> groups = manifest.Releases
											.Select( release => new ReleaseGroup(
														release.Name, release.Description, release.Dependency, [] ) )
											.ToList();

		Dictionary<string, ReleaseGroup> byName = new ( StringComparer.Ordinal );

		foreach( ReleaseGroup group in groups )
			if( !byName.TryAdd( group.Name, group ) )
				throw new InvalidBundleException(
					$"Bundle manifest lists release '{group.Name}' more than once." );

		for( int index = 1; index < groups.Count; index++ )
			if( !string.Equals( groups[index].Dependency, groups[index - 1].Name, StringComparison.Ordinal ) )
				throw new InvalidBundleException(
					$"Release '{groups[index].Name}' does not depend on '{groups[index - 1].Name}', "
					+ "the release preceding it in the bundle." );

		ReleaseGroup? unreleased = null;

		// empty when the bundle lists no release: then the unreleased plans start the lineage
		string lastRelease = groups.Count > 0 ? groups[^1].Name : "";

		foreach( BundleManifestPlan plan in manifest.Plans )
			if( plan.Release.Length == 0 )
				( unreleased ??= new ReleaseGroup( "", "", lastRelease, [] ) ).Plans.Add( plan );
			else if( byName.TryGetValue( plan.Release, out ReleaseGroup? group ) )
				group.Plans.Add( plan );
			else
				throw new InvalidBundleException(
					$"Plan '{plan.Name}' references release '{plan.Release}' which the manifest does not list." );

		if( unreleased is not null )
			groups.Add( unreleased );

		return groups;
	}

	private sealed record ReleaseGroup(
		string Name, string Description, string Dependency, List<BundleManifestPlan> Plans );

	/// <summary>
	/// The group deployment starts at, how many of its plans are already deployed, and whether
	/// it is being resumed — a resumed release does not have its start journaled again.
	/// </summary>
	private sealed record StartPoint( int Group, int SkippedPlans, bool Resumed );
}
