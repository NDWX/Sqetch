namespace Pug.Sqetch;

public record ReleaseDefinition( string Name, string Description, string Dependency ) 
	: ObjectDefinition( Name, Description );