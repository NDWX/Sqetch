namespace Pug.Sqetch;

public record PlanDefinition( string Name, string Description, ICollection<string> Dependencies ) 
	: ObjectDefinition( Name, Description );