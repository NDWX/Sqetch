namespace Pug.Sqetch.Bundling;

/// <summary>
/// Maps assembled bundle content onto bundle entries (manifest and script files). Custom
/// layouts plug in through <see cref="BundleLayoutRegistry"/>.
/// </summary>
public interface IBundleLayout
{
	string Name { get; }

	void Write( Bundle bundle, IBundleWriter writer );
}
