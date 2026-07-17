namespace Pug.Sqetch.Bundling;

/// <summary>
/// A pluggable kind of bundle output (an archive file or a directory tree). Custom types
/// plug in through <see cref="BundleTypeRegistry"/>.
/// </summary>
public interface IBundleType
{
	string Name { get; }

	/// <summary>Default output name for a bundle, e.g. the base name plus an extension.</summary>
	string DefaultOutputName( string baseName );

	/// <summary>
	/// Creates the bundle output at <paramref name="outputPath"/>; refuses an existing
	/// target with a <see cref="BundlingException"/>.
	/// </summary>
	IBundleWriter Create( string outputPath );
}
