using Pug.Sqetch.Stores.FileSystem;

namespace Pug.Sqetch.Tests;

/// <summary>
/// Drives the real <see cref="IProject"/> engine over the file-system stores, wired the
/// way the CLI wires them.
/// </summary>
public class EngineIntegrationTests
{
	[Fact]
	public void FullReleaseLifecycle()
	{
		using TempProject temp = TempProject.Create( ShardingCases.Configuration( NamePrefixShardingStrategy.StrategyName ) );
		using FileSystemProjectStores stores = temp.Open();
		using IProject project = ProjectFactory.Create(
			stores.InfoStore, stores.ScriptsStore,
			new ReleaseChainDependencyDeterminator( stores.InfoStore ), TestData.User );

		project.Add( new PlanDefinition( "customer-email", "Add e-mail column", [] ) );
		project.Add( new PlanDefinition( "customer-index", "Index the e-mail column", ["customer-email"] ) );

		project.Add( new StepDefinition( "customer-email", "add-column", "adds the column", [] ), "customer-email" );

		Assert.Single( project.GetSteps( "customer-email" ) );

		project.CreateRelease( new ReleaseDefinition( "2026.07", "July release", "" ), ["customer-email", "customer-index"] );

		Assert.Equal( 2, project.GetPlans( "2026.07" ).Count() );

		// removing a plan that another release member depends on must be refused
		Assert.Throws<AbandonedPlanDependantsException>(
			() => project.RemovePlanFromRelease( "2026.07", "customer-email" ) );

		project.FinalizeRelease( "2026.07" );

		Assert.NotNull( stores.InfoStore.GetRelease( "2026.07" )!.Finalized );
		Assert.Throws<PlanFinalizedException>( () => project.DeletePlan( "customer-email" ) );

		project.Add( new PlanDefinition( "cleanup", "Remove legacy column", [] ) );

		Assert.Throws<ReleaseFinalizedException>(
			() => project.AddPlanToRelease( "2026.07", "cleanup", includeDependencies: false ) );

		// a follow-up release can depend on the finalized one
		project.CreateRelease( new ReleaseDefinition( "2026.08", "August release", "2026.07" ), ["cleanup"] );

		Assert.Equal(
			["2026.08"],
			project.GetReleaseDependants( "2026.07" ).Select( x => x.Definition.Name ).ToArray() );

		// registration context carries the wired user identity
		Assert.Equal( TestData.User, stores.InfoStore.GetPlan( "cleanup" )!.Registration.Subject );
	}
}
