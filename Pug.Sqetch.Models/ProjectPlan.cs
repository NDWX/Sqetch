 namespace Pug.Sqetch.Models;

public record ProjectPlan( ObjectDefinition Definition, string Release, ActionContext Registration ) 
	: ProjectElement( Definition, Registration );