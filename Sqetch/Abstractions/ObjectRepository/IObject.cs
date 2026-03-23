namespace Sqetch;

public interface IObject<T>
	: IDisposable
{
	string Key { get; }
	
	T? Read();
	
	Task<T?> ReadAsync();
	
	void Write( T info );
	
	Task WriteAsync( T info );
}