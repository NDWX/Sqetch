using System.Formats.Tar;
using System.IO.Compression;

namespace Pug.Sqetch.Bundling;

public sealed class TarGzBundleType : IBundleType
{
	public const string TypeName = "tar.gz";

	public string Name => TypeName;

	public string DefaultOutputName( string baseName ) => baseName + ".tar.gz";

	public IBundleWriter Create( string outputPath )
		=> Create( BundleOutput.CreateFile( outputPath ), leaveOutputOpen: false );

	/// <summary>Writes the archive to <paramref name="output"/>, which stays open.</summary>
	public IBundleWriter Create( Stream output )
		=> Create( output, leaveOutputOpen: true );

	private static IBundleWriter Create( Stream output, bool leaveOutputOpen )
	{
		GZipStream gzip = new ( output, CompressionLevel.Optimal, leaveOpen: leaveOutputOpen );

		return new TarBundleType.Writer( new TarWriter( gzip, leaveOpen: true ), gzip );
	}
}
