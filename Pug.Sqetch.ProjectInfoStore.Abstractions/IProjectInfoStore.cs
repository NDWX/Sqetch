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

	/// <summary>
	/// Whether the release named <paramref name="name"/> exists; with a null or whitespace
	/// <paramref name="name"/>, whether any release exists at all.
	/// </summary>
	bool ReleaseExists( string? name = null );

	/// <summary>
	/// Plans matching <paramref name="criteria"/>. Released plans are returned grouped by
	/// release, with the groups in release-chronological (dependency-chain) order; the order
	/// of plans within a group, and of unreleased plans, is unspecified — chronological
	/// ordering of individual plans is the business layer's responsibility.
	/// </summary>
	IEnumerable<ProjectPlan> ListPlans( PlanSearchCriteria criteria );

	/// <summary>
	/// Releases matching <paramref name="criteria"/>, in no particular order — chronological
	/// ordering is the business layer's responsibility.
	/// </summary>
	IEnumerable<ProjectRelease> ListReleases( ReleaseSearchCriteria criteria );

	ProjectRelease? GetRelease(string name);

	void AddRelease( ObjectDefinition definition, ActionContext context );

	/// <summary>
	/// Removes an open, empty release. The release must not be finalized and must no longer
	/// contain plans.
	/// </summary>
	void DeleteRelease( string name );

	void SetReleaseContext( string release, ActionContext releaseContext );

	IEnumerable<ProjectRelease> GetReleaseDependants( string release );

	IEnumerable<ProjectElement> GetReleasePlans( string release );

	void AddReleasePlan( string release, string name, ActionContext context );

	void DeleteReleasePlan( string release, string name, ActionContext context );
}