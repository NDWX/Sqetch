using Pug.Sqetch.Bundling;
using Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

namespace Pug.Sqetch.Deployment;

public static class BundleValidator
{
	/// <summary>
	/// Reads the bundle's manifest and confirms every step listed in it has all three
	/// scripts in the bundle; throws <see cref="InvalidBundleException"/> naming every
	/// missing entry, or <see cref="InvalidBundleManifestException"/> for a missing or
	/// invalid manifest.
	/// </summary>
	public static BundleManifest Validate( IBundleReader reader, IBundleLayout layout )
	{
		BundleManifest manifest = layout.ReadManifest( reader );

		List<string> missing = new ();

		foreach( BundleManifestPlan plan in manifest.Plans )
			foreach( BundleManifestStep step in plan.Steps )
				foreach( StepScriptKind kind in Enum.GetValues<StepScriptKind>() )
				{
					string path = layout.ScriptPath( plan.Name, step.Name, kind );

					if( !reader.Contains( path ) )
						missing.Add( path );
				}

		if( missing.Count > 0 )
			throw new InvalidBundleException(
				$"Bundle is missing entries listed in its manifest: {string.Join( ", ", missing )}." );

		return manifest;
	}
}
