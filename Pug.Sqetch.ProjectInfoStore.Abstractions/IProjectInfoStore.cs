namespace Pug.Sqetch;

public interface IProjectInfoStore : IDisposable
{
	ProjectDefinition GetDefinition();

	void AddPlan( ObjectDefinition definition, IEnumerable<string> dependencies, ActionContext context );

	void UpdatePlan( ObjectDefinition definition );

	bool PlanExists( string name );

	IDictionary<string, ProjectPlan> GetPlans( string? release = null );

	ProjectPlan? GetPlan( string name );

	IEnumerable<string> GetPlanDependencies( string name );

	IEnumerable<string> GetPlanDependants( string name );

	void SetPlanDependencies( string name, IEnumerable<string> dependencies );

	void AddStep(string plan, ObjectDefinition definition, IEnumerable<string> dependencies, ActionContext context );

	void UpdateStep(string plan, ObjectDefinition definition );
	
	bool StepExists( string plan, string name );

	IDictionary<string, ProjectElement> GetSteps( string plan );

	ProjectElement? GetStep(string plan, string name );

	IEnumerable<string> GetStepDependencies( string plan, string name );

	void SetStepDependencies( string plan, string name, IEnumerable<string> dependencies );

	IEnumerable<string> GetStepDependants( string plan, string name );

	void DeletePlan( string name );

	void DeleteStep( string plan, string name );

	bool VersionExists( string name );

	IEnumerable<ProjectRelease> GetReleases( 
		string prefix = "", bool released = false, 
		Range<DateTime>? createTimestamp = null, Range<DateTime>? finalizeTimestamp = null );

	ProjectRelease? GetRelease(string name);

	void AddRelease( ObjectDefinition definition, ActionContext context );
	
	void SetReleaseContext( string release, ActionContext releaseContext );

	IEnumerable<ProjectRelease> GetReleaseDependants( string release );

	IEnumerable<ProjectElement> GetReleasePlans( string release );

	void AddReleasePlan( string release, string name, ActionContext context );

	void DeleteReleasePlan( string release, string name, ActionContext context );
}