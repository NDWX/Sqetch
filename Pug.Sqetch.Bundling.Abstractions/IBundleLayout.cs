namespace Pug.Sqetch.Bundling;

/// <summary>
/// Maps assembled bundle content onto bundle entries (manifest and script files). Custom
/// layouts plug in through <see cref="BundleLayoutRegistry"/>.
/// </summary>
public interface IBundleLayout
{
	string Name { get; }

	void Write( Bundle bundle, IBundleWriter writer );

	/// <summary>
	/// Reads and validates the manifest of a bundle; throws
	/// <see cref="InvalidBundleManifestException"/> when it is missing or invalid.
	/// </summary>
	BundleManifest ReadManifest( IBundleReader reader );

	/// <summary>Entry path of a step's script within this layout.</summary>
	string ScriptPath( string plan, string step, StepScriptKind kind );
}
