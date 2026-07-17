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

	/// <summary>
	/// Opens the scripts of <paramref name="step"/>; the caller owns and must dispose the
	/// returned streams. Throws <see cref="MissingStepScriptsException"/> when any of the
	/// step's script files is missing.
	/// </summary>
	StepScripts GetStepScripts( string plan, string step );

	/// <summary>
	/// Confirms every step of <paramref name="plan"/> has all of its script files; throws
	/// <see cref="MissingStepScriptsException"/> for the first step with a missing script.
	/// </summary>
	void VerifyStepScripts( string plan );

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
