using Pug.Sqetch.Bundling;
using Pug.Sqetch.Deployment;
using Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;
using static Pug.Sqetch.Tests.Deployment.Manifests;

namespace Pug.Sqetch.Tests.Deployment;

public class BundleValidatorTests
{
	private readonly DefaultBundleLayout _layout = new ();

	[Fact]
	public void CompleteBundlesPassAndReturnTheManifestAndJournaling()
	{
		BundleManifest manifest = Manifest( [Release( "r1" )], [Plan( "a", "r1", "s1" )] );
		InMemoryBundleReader reader = ReaderFor( manifest, _layout );

		reader.Add( "manifest.json", ManifestJson( manifest ) );

		ValidatedBundle read = BundleValidator.Validate( reader, _layout );

		Assert.Equal( "a", Assert.Single( read.Manifest.Plans ).Name );
		Assert.Equal(
			[FakeJournal.Marker( JournalingSlot.PrepareJournal )],
			read.Journaling.Statements( JournalingSlot.PrepareJournal ) );
		Assert.Equal(
			FakeJournal.Marker( JournalingSlot.GetLatestRelease ),
			read.Journaling.Text( JournalingSlot.GetLatestRelease ) );
	}

	/// <summary>
	/// Journaling is read before any driver exists, so a bundle that could never be journaled is
	/// refused without the database being touched.
	/// </summary>
	[Fact]
	public void EveryMissingJournalingStatementIsListed()
	{
		BundleManifest manifest = Manifest( [Release( "r1" )], [Plan( "a", "r1", "s1" )] );
		InMemoryBundleReader reader = ReaderFor( manifest, _layout );

		reader.Add( "manifest.json", ManifestJson( manifest ) );
		reader.Remove( "journaling/PrepareJournal.sql" );
		reader.Remove( "journaling/OnDeployingStep.sql" );

		BundlingException error = Assert.Throws<BundlingException>(
			() => BundleValidator.Validate( reader, _layout ) );

		Assert.Contains( "journaling/PrepareJournal.sql", error.Message );
		Assert.Contains( "journaling/OnDeployingStep.sql", error.Message );
	}

	[Fact]
	public void AQueryStatementBundledWithTwoStatementsIsRefused()
	{
		BundleManifest manifest = Manifest( [Release( "r1" )], [Plan( "a", "r1", "s1" )] );
		InMemoryBundleReader reader = ReaderFor( manifest, _layout );

		reader.Add( "manifest.json", ManifestJson( manifest ) );
		reader.Add( "journaling/GetDeployedPlans.sql", "select 1\n;;\nselect 2" );

		BundlingException error = Assert.Throws<BundlingException>(
			() => BundleValidator.Validate( reader, _layout ) );

		Assert.Contains( "GetDeployedPlans is a query", error.Message );
	}

	[Fact]
	public void EveryMissingScriptIsListed()
	{
		BundleManifest manifest = Manifest( [Release( "r1" )], [Plan( "a", "r1", "s1" ), Plan( "b", "r1", "s1" )] );
		InMemoryBundleReader reader = ReaderFor( manifest, _layout );

		reader.Add( "manifest.json", ManifestJson( manifest ) );
		reader.Remove( _layout.ScriptPath( "a", "s1", StepScriptKind.Rollback ) );
		reader.Remove( _layout.ScriptPath( "b", "s1", StepScriptKind.Deploy ) );

		InvalidBundleException error = Assert.Throws<InvalidBundleException>(
			() => BundleValidator.Validate( reader, _layout ) );

		Assert.Contains( "plans/a/steps/s1/rollback.sql", error.Message );
		Assert.Contains( "plans/b/steps/s1/deploy.sql", error.Message );
	}

	[Fact]
	public void MissingManifestSurfacesAsAnInvalidManifest()
	{
		Assert.Throws<InvalidBundleManifestException>(
			() => BundleValidator.Validate( new InMemoryBundleReader(), _layout ) );
	}

	private string ManifestJson( BundleManifest manifest )
	{
		// serialize through the layout by writing a scriptless bundle
		InMemoryBundleWriter writer = new ();

		_layout.Write(
			new Bundle(
				new ProjectDefinition( manifest.Project.Name, manifest.Project.Description, manifest.Project.Engine ),
				manifest.Selection,
				Journaling(),
				manifest.Releases
						.Select( release => new BundleRelease(
									release.Name, release.Description, release.Dependency, release.Finalized ) )
						.ToList(),
				manifest.Plans
						.Select( plan => new BundlePlan(
									plan.Name, plan.Description, plan.Release, plan.Dependencies,
									plan.Steps
										.Select( step => new BundleStep(
													step.Name, step.Description, step.Dependencies,
													() => new StepScripts( null, null, null ) ) )
										.ToList() ) )
						.ToList() ),
			writer );

		return writer.Entries["manifest.json"];
	}

	private sealed class InMemoryBundleWriter : IBundleWriter
	{
		public Dictionary<string, string> Entries { get; } = new ();

		public void Add( string path, Stream content )
		{
			using StreamReader reader = new ( content );

			Entries[path] = reader.ReadToEnd();
		}

		public void Dispose()
		{
		}
	}
}
