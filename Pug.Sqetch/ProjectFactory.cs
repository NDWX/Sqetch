using Pug.Sqetch.Models;
using Pug.Sqetch.ProjectInfoStore;

namespace Pug.Sqetch;

public static class ProjectFactory
{
	public static IProject Create(
		IProjectInfoStore infoStore, IScriptsStore scriptsStore,
		IReleaseDependencyDeterminator releaseDependencyDeterminator, UserInfo userInfo )
	{
		return new Project( infoStore, scriptsStore, releaseDependencyDeterminator, userInfo );
	}
}
