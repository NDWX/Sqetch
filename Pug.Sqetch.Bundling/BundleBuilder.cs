namespace Pug.Sqetch.Bundling;

/// <summary>
/// Assembles the deployable content of a project into a <see cref="Bundle"/>, verifying
/// that no included plan is missing a step script.
/// </summary>
public class BundleBuilder
{
	private readonly IReadOnlyProject _project;
	private readonly ProjectDefinition _definition;

	public BundleBuilder( IReadOnlyProject project, ProjectDefinition definition )
	{
		ArgumentNullException.ThrowIfNull( project );
		ArgumentNullException.ThrowIfNull( definition );

		_project = project;
		_definition = definition;
	}

	/// <summary>
	/// Assembles the bundle in deployment-chronological order. Throws
	/// <see cref="EmptyBundleException"/> when no plans match <paramref name="selection"/>
	/// and <see cref="MissingStepScriptsException"/> when an included plan is missing a
	/// step script.
	/// </summary>
	public Bundle Assemble( BundleSelection selection )
	{
		List<ProjectRelease> releases = SelectReleases( selection );
		List<BundlePlan> plans = new ();

		foreach( ProjectRelease release in releases )
			plans.AddRange(
				_project.GetPlans( new PlanSearchCriteria( Release: release.Definition.Name, Released: true ) )
					.Select( ToBundlePlan ) );

		// unreleased plans deploy last: released plans can never depend on them
		if( selection == BundleSelection.Test )
			plans.AddRange( _project.GetPlans( new PlanSearchCriteria() ).Select( ToBundlePlan ) );

		if( plans.Count == 0 )
			throw new EmptyBundleException();

		foreach( BundlePlan plan in plans )
			_project.VerifyStepScripts( plan.Name );

		return new Bundle(
			_definition,
			selection,
			releases.Select( x => new BundleRelease(
				x.Definition.Name, x.Definition.Description, x.Finalized is not null ) ).ToList(),
			plans );
	}

	private List<ProjectRelease> SelectReleases( BundleSelection selection )
	{
		List<ProjectRelease> releases =
			_project.GetReleases( new ReleaseSearchCriteria( Finalized: true ) ).ToList();

		// finalized releases are always the prefix of the single release lineage, so open
		// releases follow them in chain order
		if( selection == BundleSelection.Test )
			releases.AddRange( _project.GetReleases( new ReleaseSearchCriteria() ) );

		return releases;
	}

	private BundlePlan ToBundlePlan( ProjectPlan plan )
	{
		string name = plan.Definition.Name;

		return new BundlePlan(
			name,
			plan.Definition.Description,
			plan.Release,
			( ( plan.Definition as PlanDefinition )?.Dependencies ?? [] ).ToList(),
			_project.GetSteps( name ).Select( x => ToBundleStep( name, x ) ).ToList() );
	}

	private BundleStep ToBundleStep( string plan, ProjectElement step )
		=> new (
			step.Definition.Name,
			step.Definition.Description,
			( ( step.Definition as StepDefinition )?.Dependencies ?? [] ).ToList(),
			() => _project.GetStepScripts( plan, step.Definition.Name ) );
}
