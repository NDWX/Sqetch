namespace Pug.Sqetch.Bundling;

public enum BundleSelection
{
	/// <summary>Plans of finalized releases only.</summary>
	Finalized,

	/// <summary>Every plan, regardless of release status.</summary>
	Test
}

/// <summary>
/// Assembled bundle content. All lists are in deployment-chronological order; that order is
/// part of the bundle contract.
/// </summary>
public sealed record Bundle(
	ProjectDefinition Project,
	BundleSelection Selection,
	IReadOnlyList<BundleRelease> Releases,
	IReadOnlyList<BundlePlan> Plans );

public sealed record BundleRelease( string Name, string Description, bool Finalized );

/// <summary><paramref name="Release"/> is empty for unreleased plans (test selection).</summary>
public sealed record BundlePlan(
	string Name,
	string Description,
	string Release,
	IReadOnlyList<string> Dependencies,
	IReadOnlyList<BundleStep> Steps );

/// <summary>
/// <paramref name="OpenScripts"/> opens the step's scripts on demand — lazy so that at most
/// one step's streams are open at a time — and is valid only while the project the bundle
/// was assembled from is alive. The caller disposes the result; a script deleted after
/// assembly surfaces as <see cref="MissingStepScriptsException"/>.
/// </summary>
public sealed record BundleStep(
	string Name,
	string Description,
	IReadOnlyList<string> Dependencies,
	Func<StepScripts> OpenScripts );
