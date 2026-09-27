using Pug.Sqetch.Bundling;
using Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

namespace Pug.Sqetch.Deployment;

/// <summary>
/// Executes a bundle's deploy scripts in manifest order against a database, journaling every
/// event through the change journal writer and starting after whatever the journal says is
/// already deployed. Where that start lies follows from the journal alone: nothing journaled
/// deploys the bundle whole, an <em>incompletely</em> deployed release resumes inside itself
/// (so the bundle must contain it), skipping the plans the journal already holds and deploying
/// the rest in bundle order, and a <em>completely</em> deployed release hands over to
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
			Deploy( groups, deployedPlans: null );

			return;
		}

		if( latest.Completed )
		{
			Deploy( groups, latest, deployedPlans: null);

			return;
		}

		// an incompletely deployed release must finish before any later release, so a bundle that
		// does not contain it cannot be deployed at all; checked before its plans are queried,
		// since a bundle that cannot be used is not worth the round trip
		if( !groups.Any( group => string.Equals( group.Name, latest.Name, StringComparison.Ordinal ) ) )
			throw new IncompatibleBundleException(
				latest,
				$"{Describe( latest.Name )} is only partially deployed; deploy a bundle that contains it "
				+ $"so its remaining plans deploy{( groups.Count > 0 ? $" before {Describe( groups[0].Name )}" : "" )}." );

		// the journal's plans are a set, not a position: resuming by membership is unaffected by
		// the order the bundle lists plans in, which is only partly determined — plans with no
		// dependency between them are ordered by registration, which the journal never records.
		// An empty set resumes at the release's first plan, without journaling its start again.
		Deploy( groups, latest, journal.GetDeployedPlans( latest.Name, driver ).ToList());
	}

	/// <summary>
	/// Deploys the groups the database has not seen yet. <paramref name="deployedPlans"/> resumes
	/// inside <paramref name="lastGroup"/> — which must then be one of <paramref name="groups"/> —
	/// skipping the plans it names; <paramref name="lastGroup"/> alone hands over to the group
	/// depending on it; neither starts at the bundle's first group.
	/// <paramref name="latestJournaledRelease"/> is diagnostic context for refusals only.
	/// </summary>
	private void Deploy(List<ReleaseGroup> groups,
		JournaledRelease? latestJournaledRelease = null, IReadOnlyCollection<string>? deployedPlans = null)
	{
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
		
		bool firstGroupFound = false, 
			pseudoReleaseFound = false, 
			lastGroupDeployed = false;
		
		ReleaseGroup lastGroup = null;

		List<(ReleaseGroup, IEnumerable<string>)> groupsToDeploy = new();

		foreach (ReleaseGroup group in groups)
		{
			if (pseudoReleaseFound || (latestJournaledRelease is not null && latestJournaledRelease.Name.Length == 0 && latestJournaledRelease.Completed))
				throw new IncompatibleBundleException(
					latestJournaledRelease,
					$"A pseudo bundle cannot be followed by other releases");

			lastGroup = group;
			pseudoReleaseFound = group.Name.Length == 0;

			// start by working out the first release from the bundle to be deployed
			if (!firstGroupFound)
			{
				// if database has no previous bundle deployment
				if (latestJournaledRelease is null)
				{
					// Bundle must include very first release, which has no dependency.
					// If first release in the bundle has dependency, and therefore doesn't include very first release,
					// bundle is incomplete because first release in the bundle can't have any dependency
					if (group.HasDependency)
						throw new IncompatibleBundleException(
							null,
							$"this bundle continues from {Describe(groups[0].Dependency)} "
							+ "but the database has no deployed releases.");

					groupsToDeploy.Add((group, Array.Empty<string>()));
					firstGroupFound = true;
					continue;
				}

				// database has a previous deployment;
				
				// If last deployed release is found
				if (string.Equals(latestJournaledRelease.Name, group.Name, StringComparison.Ordinal))
				{
					// if last deployed release was completed, skip to next release in bundle
					if (latestJournaledRelease.Completed)
					{
						listener.SkippingRelease(latestJournaledRelease.Name);
						firstGroupFound = true;
						continue;
					}

					// if last deployed release was not yet completed;
					
					// throw exception if deployed plans from that release are not in the bundle,
					// which indicates the release has been changed and therefore
					// the database should be restored from a clean backup
					EnsureDeployedPlansAreInBundleRelease(latestJournaledRelease, deployedPlans!, group);
					
					// otherwise deploy release
					groupsToDeploy.Add((group, deployedPlans!.AsEnumerable()));

					firstGroupFound = true;
					continue;
				}

				// If last deployed release is dependency for next release in bundle
				if (string.Equals(latestJournaledRelease.Name, group.Dependency, StringComparison.Ordinal))
				{
					// If last deployed release was a pseudo-release,
					// ignore subsequent releases in bundle because
					// pseudo-release cannot be followed by any other releases:
					// pseudo-release contains un-released plans and is only possible for test-releases
					// This scenario usually means the project release structure has changed,
					// and therefore database should be restored from a clean backup
					if (latestJournaledRelease.Name.Length == 0)
					{
						listener.SkippingRelease(group.Name);
						continue;
					}
					
					// Otherwise deploy release
					listener.ContinuingFrom(latestJournaledRelease.Name);
					groupsToDeploy.Add((group, Array.Empty<string>()));
					firstGroupFound = true;
					continue;
				}

				// Releases prior to last deployed release are skipped
				listener.SkippingRelease(group.Name);
				continue;
			}

			// Subsequent releases after first to-be-included release are deployed 
			groupsToDeploy.Add((group, Array.Empty<string>()));
		}
		
		// if no new release is found
		if (groupsToDeploy.Count == 0)
		{ 
			// and the last release inspected is not the last release journaled in previous deployment,
			// throw and error
			if (latestJournaledRelease is not null && latestJournaledRelease.Name.Length > 0 && !string.Equals(lastGroup.Name, latestJournaledRelease.Name))
			{
				throw new IncompatibleBundleException(
					latestJournaledRelease,
					$"database is at {Describe( latestJournaledRelease.Name )}, which this bundle neither contains nor continues from." );
			}

			// otherwise, there's just no new release to deploy
			listener.NothingToDeploy();
			return;
		}

		Stack<(ReleaseGroup release, Stack<string> deployedPlans)> deployedGroups = new();
		void Deploy(ReleaseGroup release, IEnumerable<string> plansToSkip)
		{
			if (latestJournaledRelease is null || !string.Equals(latestJournaledRelease.Name, release.Name))
			{
				listener.DeployingRelease(release.Name);
				journal.DeployingRelease(new DeploymentUnit(release.Name, release.Description),
					Transaction());
			}

			// if deployePlans is not null, then we're continuing incomplete release from previous deployment.
			if (plansToSkip is not null)
			{
				// deploy pending plans for the release
				for (int index = 0; index < release.Plans.Count; index++)
				{
					BundleManifestPlan plan = release.Plans[index];
						
					if (plansToSkip.Contains(plan.Name))
					{
						listener.SkippingPlan(release.Name, plan.Name);
						continue;
					}

					DeployPlan(release.Name, plan, Transaction, listener);

					if (commitLevel == DeploymentCommitLevel.Plan && index < release.Plans.Count - 1)
						CommitBoundary();
				}
			}
			else // deploy pending plans for the next release
			{
				for (int index = 0; index < release.Plans.Count; index++)
				{
					BundleManifestPlan plan = release.Plans[index];
					DeployPlan(release.Name, plan, Transaction, listener);

					if (commitLevel == DeploymentCommitLevel.Plan && index < release.Plans.Count - 1)
						CommitBoundary();
				}
			}
				
			journal.ReleaseDeployed(release.Name, Transaction());
			listener.ReleaseDeployed(release.Name);

			CommitBoundary();
		}
		
		try
		{
			foreach ((ReleaseGroup release, IEnumerable<string> plansToSkip) in groupsToDeploy)
			{
				Deploy(release, plansToSkip);
			}
		}
		catch (Exception e)
		{
			transaction?.Rollback();
			
			throw;
		}
	}

	private static void EnsureDeployedPlansAreInBundleRelease(JournaledRelease? journaledRelease,
		IReadOnlyCollection<string> deployedPlans, ReleaseGroup bundlePlanGroup)
	{
		IEnumerable<string> missingDeployedPlans = deployedPlans.Except(bundlePlanGroup.Plans.Select(plan => plan.Name));

		if( missingDeployedPlans.Any())
		{
			if( deployedPlans is not null )
				throw new IncompatibleBundleException(
					journaledRelease,
					$"the database has deployed plans of {Describe( journaledRelease.Name )} that this bundle does "
					+ $"not contain: {string.Join( ", ", missingDeployedPlans.Select( plan => $"'{plan}'" ) )}; deploy a "
					+ "bundle that contains them, or restore the database." );
		}
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
		string Name,
		string Description,
		string Dependency,
		List<BundleManifestPlan> Plans)
	{
		public bool HasDependency => !string.IsNullOrEmpty( Dependency );
	}

	/// <summary>
	/// The group deployment starts at and, when that group is being resumed, the names of its
	/// plans the journal already holds — null when the group is entered fresh. A resumed release
	/// does not have its start journaled again: the run that began it did that.
	/// </summary>
	private sealed record StartPoint( int Group, IReadOnlySet<string>? DeployedPlans );
}
