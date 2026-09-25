namespace Pug.Sqetch.Bundling;

internal static class BundleInput
{
	/// <summary>
	/// Extracts an archive bundle into a fresh temporary directory using
	/// <paramref name="extract"/> and returns a reader over it that deletes the directory
	/// on dispose. Missing or unreadable archives surface as friendly
	/// <see cref="BundlingException"/>s.
	/// </summary>
	public static IBundleReader ExtractToTemporary( string path, Action<string, string> extract )
	{
		if( !File.Exists( path ) )
			throw new BundlingException( $"Bundle '{path}' does not exist." );

		string temporary = Path.Combine( Path.GetTempPath(), "sqetch-bundle-" + Guid.NewGuid().ToString( "N" ) );

		Directory.CreateDirectory( temporary );

		try
		{
			extract( path, temporary );
		}
		catch( Exception exception ) when( exception is IOException or InvalidDataException )
		{
			try
			{
				Directory.Delete( temporary, recursive: true );
			}
			catch( IOException )
			{
			}

			throw new BundlingException( $"Cannot read bundle '{path}': {exception.Message}" );
		}

		return new DirectoryBundleReader( temporary, deleteOnDispose: true );
	}
}
