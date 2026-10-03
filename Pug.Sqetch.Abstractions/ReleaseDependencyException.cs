using Pug.Sqetch.Models;

namespace Pug.Sqetch;

public class ReleaseDependencyException
	: Exception
{
	public IEnumerable<ProjectRelease> ReleaseDependencies { get; }

	public ReleaseDependencyException( string message, IEnumerable<ProjectRelease> releaseDependencies )
		: base(message)
	{
		ReleaseDependencies = releaseDependencies;
	}
}