using Pug.Sqetch.Models;

namespace Pug.Sqetch;

public interface IReleaseDependencyDeterminator
{
	DependencyRelationship DetermineDependencyRelationship(string first,  string second);
}