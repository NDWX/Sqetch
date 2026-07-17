using System.Text.Json;

namespace Pug.Sqetch.Bundling;

/// <summary>
/// Default bundle layout: 'manifest.json' first — so streaming consumers see it before the
/// payload — then 'plans/&lt;plan&gt;/steps/&lt;step&gt;/{deploy,verify,rollback}.sql'.
/// Scripts live under 'plans/' so a plan named like the manifest file cannot collide with
/// it. The order of the manifest arrays is the deployment order and part of the contract.
/// </summary>
public sealed class DefaultBundleLayout : IBundleLayout
{
	public const string LayoutName = "default";

	private static readonly JsonSerializerOptions JsonOptions = new ()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true
	};

	public string Name => LayoutName;

	public void Write( Bundle bundle, IBundleWriter writer )
	{
		using( MemoryStream manifest = new ( JsonSerializer.SerializeToUtf8Bytes( Manifest( bundle ), JsonOptions ) ) )
			writer.Add( "manifest.json", manifest );

		foreach( BundlePlan plan in bundle.Plans )
			foreach( BundleStep step in plan.Steps )
				WriteScripts( plan, step, writer );
	}

	private static object Manifest( Bundle bundle )
		=> new
		{
			Project = new
			{
				bundle.Project.Name,
				bundle.Project.Description,
				bundle.Project.Engine
			},
			Selection = bundle.Selection.ToString().ToLowerInvariant(),
			Generated = DateTime.Now,
			Releases = bundle.Releases.Select( release => new
			{
				release.Name,
				release.Description,
				release.Finalized
			} ),
			Plans = bundle.Plans.Select( plan => new
			{
				plan.Name,
				plan.Description,
				plan.Release,
				plan.Dependencies,
				Steps = plan.Steps.Select( step => new
				{
					step.Name,
					step.Description,
					step.Dependencies
				} )
			} )
		};

	private static void WriteScripts( BundlePlan plan, BundleStep step, IBundleWriter writer )
	{
		string directory = $"plans/{plan.Name}/steps/{step.Name}";

		using StepScripts scripts = step.OpenScripts();

		AddScript( writer, $"{directory}/deploy.sql", scripts.DeployScript );
		AddScript( writer, $"{directory}/verify.sql", scripts.VerifyScript );
		AddScript( writer, $"{directory}/rollback.sql", scripts.RollbackScript );
	}

	private static void AddScript( IBundleWriter writer, string path, Stream? content )
	{
		if( content is not null )
			writer.Add( path, content );
	}
}
