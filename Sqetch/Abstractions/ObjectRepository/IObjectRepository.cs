namespace Sqetch;

public interface IObjectRepository
{
	IObject<T> Get<T>( string key );
	
	IObject<T> GetOrCreate<T>(string key );
}