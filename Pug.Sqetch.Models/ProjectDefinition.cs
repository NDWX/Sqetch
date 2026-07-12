namespace Pug.Sqetch;

public record ProjectDefinition( string Name, string Description, string Engine ) 
	: ObjectDefinition( Name, Description );