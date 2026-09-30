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

	/// <summary>
	/// Reads the project's journaling SQL back out of a bundle; throws
	/// <see cref="BundlingException"/> when any slot is missing or unreadable.
	/// Where the statements live is this layout's business — a layout is free to keep them in
	/// separate entries or inside its manifest — so consumers read them through here rather than
	/// composing entry paths of their own.
	/// </summary>
	JournalingStatements ReadJournalingStatements( IBundleReader reader );

	/// <summary>Entry path of a step's script within this layout.</summary>
	string ScriptPath( string plan, string step, StepScriptKind kind );
}
