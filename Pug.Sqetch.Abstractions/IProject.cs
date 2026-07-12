namespace Pug.Sqetch;

public interface IProject
	: IDisposable, IReadOnlyProject
{
	void Add( PlanDefinition plan, string? release = null );
	
	Task AddAsync( PlanDefinition plan, string? release = null );
	
	void DeletePlan( string name );
	
	Task DeletePlanAsync( string name );
	
	StepScriptKeys Add( StepDefinition step, string plan );
	
	void Delete( string plan, string step );
	
	void CreateRelease( ReleaseDefinition definition, IEnumerable<string> plans );
	
	void CreateRelease( ReleaseDefinition definition, bool includeUnreleasedPlans = false );

	void AddPlanToRelease( string release, string plan, bool includeDependencies );
	
	void RemovePlanFromRelease( string release, string plan, bool includeDependants = false );
	
	void DeleteRelease( string release );
	
	void FinalizeRelease( string release );
}