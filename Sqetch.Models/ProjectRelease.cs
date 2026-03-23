namespace Sqetch;

public record ProjectRelease( ReleaseDefinition Definition, ActionContext? Finalized, ActionContext Registration ) 
	: ProjectElement<ReleaseDefinition>( Definition, Registration );