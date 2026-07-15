namespace Pug.Sqetch.Stores.FileSystem;

public class ProjectStoreException : Exception
{
	public ProjectStoreException( string message, Exception? innerException = null )
		: base( message, innerException )
	{
	}
}
