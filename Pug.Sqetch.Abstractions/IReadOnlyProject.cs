namespace Pug.Sqetch;

public interface IReadOnlyProject : IDisposable
{
	IEnumerable<ProjectPlan> GetDependants( string plan );
	IEnumerable<ProjectPlan> GetDependencies( string plan );

	/// <summary>
	/// Steps of <paramref name="plan"/> in dependency-chronological order: a step always
	/// follows the steps it depends on, ties broken by registration time.
	/// </summary>
	IEnumerable<ProjectElement> GetSteps( string plan );

	StepScripts GetStepScripts( string plan, string step );

	/// <summary>
	/// Releases matching <paramref name="criteria"/> in dependency-chronological order: a
	/// release always follows the release it depends on, ties broken by registration time.
	/// </summary>
	IEnumerable<ProjectRelease> GetReleases( ReleaseSearchCriteria criteria );

	IEnumerable<ProjectRelease> GetReleaseDependants( string release );

	/// <summary>
	/// Plans matching <paramref name="criteria"/> in dependency-chronological order:
	/// released plans are ordered by release chronology first (a plan in a later release
	/// always follows every plan of an earlier release), then by plan dependencies within
	/// each release, ties broken by registration time.
	/// </summary>
	IEnumerable<ProjectPlan> GetPlans( PlanSearchCriteria criteria );
}
