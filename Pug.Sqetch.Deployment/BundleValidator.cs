using Pug.Sqetch.Bundling;
using Pug.Sqetch.Models;

namespace Pug.Sqetch.Deployment;

public static class BundleValidator
{
	/// <summary>
	/// Reads the bundle's manifest and the project's journaling SQL, and confirms every step listed
	/// in the manifest has all three scripts in the bundle; throws
	/// <see cref="InvalidBundleException"/> naming every missing entry,
	/// <see cref="InvalidBundleManifestException"/> for a missing or invalid manifest, or
	/// <see cref="BundlingException"/> when the journaling statements are missing or invalid.
	/// Journaling is read here, before any driver exists, so a bundle that could never be journaled
	/// is refused without touching the database.
	/// </summary>
	public static ValidatedBundle Validate( IBundleReader reader, IBundleLayout layout )
	{
		BundleManifest manifest = layout.ReadManifest( reader );
		JournalingStatements journaling = layout.ReadJournalingStatements( reader );

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

		return new ValidatedBundle( manifest, journaling );
	}
}
