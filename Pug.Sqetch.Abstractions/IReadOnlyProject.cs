namespace Pug.Sqetch;

public interface IReadOnlyProject : IDisposable
{
	IEnumerable<ProjectPlan> GetDependants( string plan );
	IEnumerable<ProjectPlan> GetDependencies( string plan );
	IEnumerable<ProjectElement> GetSteps( string plan );
	StepScripts GetStepScripts( string plan, string step );

	IEnumerable<ProjectRelease> GetReleases(
		string prefix = "", bool released = false, Range<DateTime> createTimestamp = null,
		Range<DateTime> finalizeTimestamp = null
	);

	IEnumerable<ProjectRelease> GetReleaseDependants( string release );

	IEnumerable<ProjectElement> GetPlans( string release );
}