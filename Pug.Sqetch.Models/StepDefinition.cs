namespace Pug.Sqetch.Models;

public record StepDefinition( string Plan, string Name, string Description, ICollection<string> Dependencies ) 
	: ObjectDefinition( Name, Description );