namespace Pug.Sqetch.Models;

public record ProjectRelease( ReleaseDefinition Definition, ActionContext? Finalized, ActionContext Registration ) 
	: ProjectElement<ReleaseDefinition>( Definition, Registration );