using System.ComponentModel.DataAnnotations;

namespace Pug.Sqetch.Models;

public record ReleaseDefinition( [Required] string Name, string Description, [Required] string Dependency )
	: ObjectDefinition( Name, Description );