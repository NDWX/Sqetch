 namespace Pug.Sqetch;

public record ProjectPlan( ObjectDefinition Definition, string Release, ActionContext Registration ) 
	: ProjectElement( Definition, Registration );