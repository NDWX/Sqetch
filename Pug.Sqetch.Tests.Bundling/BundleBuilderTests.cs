using Pug.Sqetch.Bundling;
using Pug.Sqetch.Stores.FileSystem;
using Pug.Sqetch.Tests.Stores.FileSystem;

namespace Pug.Sqetch.Tests.Bundling;

/// <summary>
/// Drives <see cref="BundleBuilder"/> against the real engine over a temp file-system
/// project: two chained releases (first finalized), plus an unreleased plan.
/// </summary>
public class BundleBuilderTests
{
	[Fact]
	public void FinalizedSelectionBundlesOnlyFinalizedReleasesInChainOrder()
	{
		using TempProject temp = TempProject.Create();
		using FileSystemProjectStores stores = temp.Open();
		using IProject project = CreateProject( stores );

		PopulateProject( temp, project );

		Bundle bundle = Builder( stores, project ).Assemble( BundleSelection.Finalized );

		Assert.Equal( BundleSelection.Finalized, bundle.Selection );
		Assert.Equal( "test-project", bundle.Project.Name );
		Assert.Equal( ["2026.07"], bundle.Releases.Select( x => x.Name ).ToArray() );
		Assert.True( bundle.Releases[0].Finalized );

		// dependency order within the release: 'index' depends on 'table'
		Assert.Equal( ["table", "index"], bundle.Plans.Select( x => x.Name ).ToArray() );
		Assert.Equal( ["create"], bundle.Plans[0].Steps.Select( x => x.Name ).ToArray() );

		using StepScripts scripts = bundle.Plans[0].Steps[0].OpenScripts();
		Assert.Equal( "-- deploy create", new StreamReader( scripts.DeployScript! ).ReadToEnd() );
	}

	[Fact]
	public void TestSelectionAppendsOpenReleasesThenUnreleasedPlans()
	{
		using TempProject temp = TempProject.Create();
		using FileSystemProjectStores stores = temp.Open();
		using IProject project = CreateProject( stores );

		PopulateProject( temp, project );

		Bundle bundle = Builder( stores, project ).Assemble( BundleSelection.Test );

		Assert.Equal( ["2026.07", "2026.08"], bundle.Releases.Select( x => x.Name ).ToArray() );
		Assert.Equal( [true, false], bundle.Releases.Select( x => x.Finalized ).ToArray() );

		Assert.Equal( ["table", "index", "cleanup", "pending"], bundle.Plans.Select( x => x.Name ).ToArray() );
		Assert.Equal( ["2026.07", "2026.07", "2026.08", ""], bundle.Plans.Select( x => x.Release ).ToArray() );
	}

	[Fact]
	public void AssembleThrowsWhenNothingMatches()
	{
		using TempProject temp = TempProject.Create();
		using FileSystemProjectStores stores = temp.Open();
		using IProject project = CreateProject( stores );

		Assert.Throws<EmptyBundleException>( () => Builder( stores, project ).Assemble( BundleSelection.Finalized ) );
		Assert.Throws<EmptyBundleException>( () => Builder( stores, project ).Assemble( BundleSelection.Test ) );
	}

	[Fact]
	public void AssembleThrowsWhenAStepScriptIsMissing()
	{
		using TempProject temp = TempProject.Create();
		using FileSystemProjectStores stores = temp.Open();
		using IProject project = CreateProject( stores );

		PopulateProject( temp, project );

		File.Delete( Path.Combine(
			temp.Root, "releases", "2026.07", "plans", "table", "steps", "create", "verify.sql" ) );

		MissingStepScriptsException missing = Assert.Throws<MissingStepScriptsException>(
			() => Builder( stores, project ).Assemble( BundleSelection.Finalized ) );

		Assert.Equal( "table", missing.Plan );
		Assert.Equal( ["verify"], missing.Scripts );
	}

	private static IProject CreateProject( FileSystemProjectStores stores )
		=> ProjectFactory.Create(
			stores.InfoStore, stores.ScriptsStore,
			new ReleaseChainDependencyDeterminator( stores.InfoStore ), TestData.User );

	private static BundleBuilder Builder( FileSystemProjectStores stores, IProject project )
		=> new ( project, stores.InfoStore.GetDefinition() );

	/// <summary>
	/// 'table' ← 'index' in finalized release 2026.07, 'cleanup' in open release 2026.08,
	/// 'pending' unreleased; every step gets its three scripts seeded.
	/// </summary>
	private static void PopulateProject( TempProject temp, IProject project )
	{
		project.Add( new PlanDefinition( "table", "Create table", [] ) );
		project.Add( new PlanDefinition( "index", "Create index", ["table"] ) );
		project.Add( new PlanDefinition( "cleanup", "Cleanup", [] ) );
		project.Add( new PlanDefinition( "pending", "Not yet released", [] ) );

		SeedScripts( temp, project.Add( new StepDefinition( "table", "create", "", [] ), "table" ), "create" );
		SeedScripts( temp, project.Add( new StepDefinition( "index", "add", "", [] ), "index" ), "add" );
		SeedScripts( temp, project.Add( new StepDefinition( "cleanup", "drop", "", [] ), "cleanup" ), "drop" );
		SeedScripts( temp, project.Add( new StepDefinition( "pending", "later", "", [] ), "pending" ), "later" );

		project.CreateRelease( new ReleaseDefinition( "2026.07", "July", "" ), ["table", "index"] );
		project.FinalizeRelease( "2026.07" );
		project.CreateRelease( new ReleaseDefinition( "2026.08", "August", "2026.07" ), ["cleanup"] );
	}

	private static void SeedScripts( TempProject temp, StepScriptKeys keys, string step )
	{
		foreach( (string key, string kind) in new[]
				{
					(keys.DeployScript, "deploy"), (keys.VerifyScript, "verify"), (keys.RollbackScript, "rollback")
				} )
			File.WriteAllText(
				Path.Combine( temp.Root, key.Replace( '/', Path.DirectorySeparatorChar ) ),
				$"-- {kind} {step}" );
	}
}
