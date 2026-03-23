namespace Sqetch;

public interface IReleaseDependencyDeterminator
{
	DependencyRelationship DetermineDependencyRelationship(string first,  string second);
}