using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace Sqetch;

public class JsonFileObject<T> : IObject<T>
{
	private readonly string _filePath;
	private bool _fileIsNew = false;
	private DateTime fileCreationTime;
	private SafeFileHandle _fileHandle;

	private readonly SemaphoreSlim _semaphore = new ( 0, 1 );

	public JsonFileObject( string filePath )
	{
		if( string.IsNullOrWhiteSpace( filePath ) )
			throw new ArgumentException( "Value cannot be null or whitespace.", nameof(filePath) );

		_filePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables( filePath ));

		if( File.Exists( filePath ) )
		{
			_fileHandle = File.OpenHandle( _filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
			_fileIsNew = true;
			fileCreationTime = DateTime.Now;
		}
		else
		{
			_fileHandle = File.OpenHandle( _filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
			fileCreationTime = File.GetCreationTime( _fileHandle );
		}
	}

	public string Key => _filePath;

	public T? Read()
	{
		T? info;

		_semaphore.Wait();
		
		using Stream stream = new FileStream( _fileHandle, FileAccess.Read );

		try
		{
			info = JsonSerializer.Deserialize<T>( stream );
		}
		catch( JsonException )
		{
			throw new SqetchDataFormatException();
		}
		finally
		{
			_semaphore.Release();
		}

		if( info is null )
			throw new SqetchDataFormatException();

		return info;
	}
	
	public async Task<T?> ReadAsync()
	{
		T? info;

		await _semaphore.WaitAsync();

		await using Stream stream = new FileStream( _fileHandle, FileAccess.Read );

		try
		{
			info = await JsonSerializer.DeserializeAsync<T>( stream );
		}
		catch( JsonException )
		{
			throw new SqetchDataFormatException();
		}
		finally
		{
			_semaphore.Release();
		}

		if( info is null )
			throw new SqetchDataFormatException();

		return info;
	}
	
	public void Write( T info )
	{
		_semaphore.Wait();

		try
		{
			if( !_fileIsNew )
			{
				_fileHandle = File.OpenHandle( _filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None );

				File.SetCreationTime( _filePath, fileCreationTime );

				_fileIsNew = false;
			}

			using Stream fileStream = new FileStream( _fileHandle, FileAccess.ReadWrite );

			JsonSerializer.Serialize( fileStream, info );
		}
		finally
		{
			_semaphore.Release();
		}
	}
	
	public async Task WriteAsync( T info )
	{
		await _semaphore.WaitAsync();

		try
		{
			if( !_fileIsNew )
			{
				_fileHandle = File.OpenHandle( _filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None );

				File.SetCreationTime( _filePath, fileCreationTime );

				_fileIsNew = false;
			}

			await using Stream fileStream = new FileStream( _fileHandle, FileAccess.ReadWrite );

			await JsonSerializer.SerializeAsync( fileStream, info );
		}
		finally
		{
			_semaphore.Release();
		}
	}

	public void Dispose()
	{
		_fileHandle.Dispose();
		_semaphore.Dispose();
		
		GC.SuppressFinalize( this );
	}
}