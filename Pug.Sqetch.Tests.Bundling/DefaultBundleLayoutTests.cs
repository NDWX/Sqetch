using System.Text;
using System.Text.Json;
using Pug.Sqetch.Bundling;

namespace Pug.Sqetch.Tests.Bundling;

public class DefaultBundleLayoutTests
{
	[Fact]
	public void WritesManifestFirstThenScriptsUnderPlansInBundleOrder()
	{
		TrackingStream deploy = Script( "-- deploy" ), verify = Script( "-- verify" ), rollback = Script( "-- rollback" );

		Bundle bundle = new (
			new ProjectDefinition( "demo", "Demo project", "postgres" ),
			BundleSelection.Finalized,
			[new BundleRelease( "2026.07", "July release", true )],
			[
				new BundlePlan(
					"table", "Create table", "2026.07", [],
					[new BundleStep( "create", "creates it", [], () => new StepScripts( deploy, verify, rollback ) )] ),
				new BundlePlan(
					"index", "Create index", "2026.07", ["table"],
					[new BundleStep( "add", "adds it", ["create"], () => new StepScripts( Script( "-- i" ), Script( "-- v" ), Script( "-- r" ) ) )] )
			] );

		RecordingWriter writer = new ();

		new DefaultBundleLayout().Write( bundle, writer );

		Assert.Equal(
			[
				"manifest.json",
				"plans/table/steps/create/deploy.sql",
				"plans/table/steps/create/verify.sql",
				"plans/table/steps/create/rollback.sql",
				"plans/index/steps/add/deploy.sql",
				"plans/index/steps/add/verify.sql",
				"plans/index/steps/add/rollback.sql"
			],
			writer.Entries.Select( x => x.Path ).ToArray() );

		Assert.Equal( "-- deploy", writer.Entries[1].Content );

		// each step's scripts are disposed right after they are written
		Assert.True( deploy.Disposed );
		Assert.True( verify.Disposed );
		Assert.True( rollback.Disposed );

		using JsonDocument manifest = JsonDocument.Parse( writer.Entries[0].Content );
		JsonElement rootElement = manifest.RootElement;

		Assert.Equal( "demo", rootElement.GetProperty( "project" ).GetProperty( "name" ).GetString() );
		Assert.Equal( "postgres", rootElement.GetProperty( "project" ).GetProperty( "engine" ).GetString() );
		Assert.Equal( "finalized", rootElement.GetProperty( "selection" ).GetString() );

		JsonElement release = rootElement.GetProperty( "releases" )[0];
		Assert.Equal( "2026.07", release.GetProperty( "name" ).GetString() );
		Assert.True( release.GetProperty( "finalized" ).GetBoolean() );

		JsonElement[] plans = rootElement.GetProperty( "plans" ).EnumerateArray().ToArray();
		Assert.Equal( ["table", "index"], plans.Select( x => x.GetProperty( "name" ).GetString() ).ToArray() );
		Assert.Equal( "2026.07", plans[1].GetProperty( "release" ).GetString() );
		Assert.Equal( "table", plans[1].GetProperty( "dependencies" )[0].GetString() );
		Assert.Equal( "add", plans[1].GetProperty( "steps" )[0].GetProperty( "name" ).GetString() );
		Assert.Equal( "create", plans[1].GetProperty( "steps" )[0].GetProperty( "dependencies" )[0].GetString() );
	}

	[Fact]
	public void NullScriptStreamsAreSkippedButTheStepStaysInTheManifest()
	{
		Bundle bundle = new (
			new ProjectDefinition( "demo", "", "postgres" ),
			BundleSelection.Test,
			[],
			[
				new BundlePlan(
					"partial", "", "", [],
					[new BundleStep( "only-deploy", "", [], () => new StepScripts( Script( "-- d" ), null, null ) )] )
			] );

		RecordingWriter writer = new ();

		new DefaultBundleLayout().Write( bundle, writer );

		Assert.Equal(
			["manifest.json", "plans/partial/steps/only-deploy/deploy.sql"],
			writer.Entries.Select( x => x.Path ).ToArray() );

		using JsonDocument manifest = JsonDocument.Parse( writer.Entries[0].Content );

		Assert.Equal( "test", manifest.RootElement.GetProperty( "selection" ).GetString() );
		Assert.Equal(
			"only-deploy",
			manifest.RootElement.GetProperty( "plans" )[0].GetProperty( "steps" )[0].GetProperty( "name" ).GetString() );
	}

	private static TrackingStream Script( string content ) => new ( Encoding.UTF8.GetBytes( content ) );

	private sealed class RecordingWriter : IBundleWriter
	{
		public List<(string Path, string Content)> Entries { get; } = [];

		public void Add( string path, Stream content )
		{
			using MemoryStream buffer = new ();

			content.CopyTo( buffer );

			Entries.Add( (path, Encoding.UTF8.GetString( buffer.ToArray() )) );
		}

		public void Dispose()
		{
		}
	}

	private sealed class TrackingStream( byte[] data ) : MemoryStream( data )
	{
		public bool Disposed { get; private set; }

		protected override void Dispose( bool disposing )
		{
			Disposed = true;

			base.Dispose( disposing );
		}
	}
}
