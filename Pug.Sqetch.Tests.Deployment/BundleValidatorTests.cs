using Pug.Sqetch.Bundling;
using Pug.Sqetch.Deployment;
using Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;
using static Pug.Sqetch.Tests.Deployment.Manifests;

namespace Pug.Sqetch.Tests.Deployment;

public class BundleValidatorTests
{
	private readonly DefaultBundleLayout _layout = new ();

	[Fact]
	public void CompleteBundlesPassAndReturnTheManifest()
	{
		BundleManifest manifest = Manifest( [Release( "r1" )], [Plan( "a", "r1", "s1" )] );
		InMemoryBundleReader reader = ReaderFor( manifest, _layout );

		reader.Add( "manifest.json", ManifestJson( manifest ) );

		BundleManifest read = BundleValidator.Validate( reader, _layout );

		Assert.Equal( "a", Assert.Single( read.Plans ).Name );
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
