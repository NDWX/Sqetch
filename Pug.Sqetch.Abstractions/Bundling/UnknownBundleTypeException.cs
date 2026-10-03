namespace Pug.Sqetch.Bundling;

public class UnknownBundleTypeException
	: BundlingException
{
	public string Name { get; }

	public UnknownBundleTypeException( string name, IEnumerable<string> known )
		: base( $"Unknown bundle type '{name}'; known types: {string.Join( ", ", known )}." )
	{
		Name = name;
	}
}
