using Pug.Sqetch.ProjectInfoStores.FileSystem;

namespace Pug.Sqetch.Cli;

/// <summary>
/// One opened Sqetch project for the duration of a command: the file-system stores, the
/// on-disk layout (deliberate CLI-level coupling to the file-system store, for printing
/// created locations), and the lazily wired business-logic engine.
/// </summary>
internal sealed class ProjectSession : IDisposable
{
	private IProject? _project;

	private ProjectSession( string root, FileSystemProjectStores stores )
	{
		Root = root;
		Stores = stores;
	}

	public string Root { get; }

	public FileSystemProjectStores Stores { get; }

	public ProjectPaths Paths => Stores.Paths;

	public IProject Project
		=> _project ??= ProjectFactory.Create(
			Stores.InfoStore, Stores.ScriptsStore,
			new ReleaseChainDependencyDeterminator( Stores.InfoStore ),
			UserIdentity.Require( Root ) );

	public static ProjectSession Open()
	{
		string root = Directory.GetCurrentDirectory();

		return new ProjectSession( root, FileSystemProjectStores.Open( root ) );
	}

	public void Dispose()
	{
		_project?.Dispose();
		Stores.Dispose();
	}
}
