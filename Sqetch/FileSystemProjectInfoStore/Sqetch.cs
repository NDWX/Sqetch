using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace Sqetch;

public class Sqetch
{
	public static UserInfo? CurrentUser;
	public static string UserProfilePath =  Environment.GetFolderPath( Environment.SpecialFolder.UserProfile );

	internal static Stream CreateFile( string path, bool expandEnvironmentVariables = false )
	{
		if( expandEnvironmentVariables )
			path = Path.GetFullPath( Environment.ExpandEnvironmentVariables( path ) );

		DateTime? fileCreationTime = null;

		if( File.Exists( path ) )
			fileCreationTime = File.GetCreationTime( path );

		SafeFileHandle fileHandle = File.OpenHandle( path, FileMode.Create, FileAccess.ReadWrite, FileShare.None );
		
		if( fileCreationTime is not null )
			File.SetCreationTime( fileHandle, fileCreationTime.Value );

		return new FileStream( fileHandle, FileAccess.ReadWrite );
	}

	internal static Stream CreateFile(string fileName, string folder)
	{
		folder = Path.GetFullPath( Environment.ExpandEnvironmentVariables( folder ) );
		
		if( !Path.Exists( folder ) )
			throw new DirectoryNotFoundException();

		string userInfoFilePath = Path.Combine( folder, fileName );

		return CreateFile( userInfoFilePath );
	}

	internal static SafeFileHandle OpenFile( string filePath )
	{
		if( string.IsNullOrWhiteSpace( filePath ) )
			throw new ArgumentException( "Value cannot be null or whitespace.", nameof(filePath) );

		filePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables( filePath ));
		
		if( !File.Exists( filePath ) )
			throw new FileNotFoundException();
		
		SafeFileHandle fileHandle = File.OpenHandle( filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
		
		return fileHandle;
	}

	private static void Store<T>( T info, string storeLocation, string fileName )
	{
		using Stream fileStream = CreateFile( fileName, storeLocation );

		JsonSerializer.Serialize( fileStream, info );
	}
	
	private static async Task StoreAsync<T>( T info, string storeLocation, string fileName )
	{
		await using Stream fileStream = CreateFile( fileName, storeLocation );

		await JsonSerializer.SerializeAsync( fileStream, info );
	}

	private static void Store<T>( T info, string filePath )
	{
		using Stream fileStream = CreateFile( filePath, true );

		JsonSerializer.Serialize( fileStream, info );
	}
	
	private static async Task StoreAsync<T>( T info, string filePath )
	{
		await using Stream fileStream = CreateFile( filePath, true );

		await JsonSerializer.SerializeAsync( fileStream, info );
	}

	public static void Configure( UserInfo userInfo, string storeLocation)
	{
		Store( userInfo, storeLocation, Defaults.UserInfoStoreFileName );
	}

	public static async Task ConfigureAsync( UserInfo userInfo, string storeLocation )
	{
		await StoreAsync( userInfo, storeLocation, Defaults.UserInfoStoreFileName );
	}

	public static UserInfo? ReadUserInfo( string userInfoStoreFilePath )
	{
		userInfoStoreFilePath = Path.GetFullPath( Environment.ExpandEnvironmentVariables( userInfoStoreFilePath ) );

		if( !File.Exists( userInfoStoreFilePath ) ) return null;

		using Stream fileStream = File.OpenRead( userInfoStoreFilePath );

		UserInfo? userInfo = JsonSerializer.Deserialize<UserInfo>(  fileStream, JsonSerializerOptions.Default );
		
		return userInfo;
	}
	
	public static async Task<UserInfo?> ReadUserInfoAsync( string userInfoStoreFilePath )
	{
		userInfoStoreFilePath = Path.GetFullPath( Environment.ExpandEnvironmentVariables( userInfoStoreFilePath ) );

		if( !File.Exists( userInfoStoreFilePath ) ) return null;

		await using Stream fileStream = File.OpenRead( userInfoStoreFilePath );

		UserInfo? userInfo = await JsonSerializer.DeserializeAsync<UserInfo>(  fileStream, JsonSerializerOptions.Default );
		
		return userInfo;
	}

	public static UserInfo? GetUserInfo()
	{
		string userInfoPath = Path.Combine( UserProfilePath, Defaults.UserInfoStoreFileName );
		
		return File.Exists( userInfoPath ) ? ReadUserInfo( userInfoPath ) : null;
	}

	public static async Task<UserInfo?> GetUserInfoAsync()
	{
		string userInfoPath = Path.Combine( UserProfilePath, Defaults.UserInfoStoreFileName );
		
		return File.Exists( userInfoPath ) ? await ReadUserInfoAsync( userInfoPath ) : null;
	}

	public static UserInfo? ReadProjectUserInfo( string projectPath )
	{
		string userInfoStoreFilePath = Path.Combine( projectPath, Defaults.UserInfoStoreFileName );

		UserInfo? userInfo = ReadUserInfo( userInfoStoreFilePath );

		return userInfo;
	}

	public static async Task<UserInfo?> ReadProjectUserInfoAsync( string projectPath )
	{
		string userInfoStoreFilePath = Path.Combine( projectPath, Defaults.UserInfoStoreFileName );

		UserInfo? userInfo = await ReadUserInfoAsync( userInfoStoreFilePath );

		return userInfo;
	}

	public static UserInfo RequireUserInfo(string? projectPath)
	{
		if( !string.IsNullOrWhiteSpace( projectPath ) && Directory.Exists( projectPath ) )
		{
			UserInfo? userInfo = ReadProjectUserInfo( projectPath );
			
			if( userInfo is not  null ) return userInfo;
		}
			
		if( CurrentUser is not null )
			return CurrentUser;

		CurrentUser = GetUserInfo() ?? throw new UnknownUserException();

		return CurrentUser;
	}

	public static async Task<UserInfo> RequireUserInfoAsync(string? projectPath)
	{
		if( !string.IsNullOrWhiteSpace( projectPath ) && Directory.Exists( projectPath ) )
		{
			UserInfo? userInfo = await ReadProjectUserInfoAsync( projectPath );
			
			if( userInfo is not  null ) return userInfo;
		}

		if( CurrentUser is not null )
			return CurrentUser;
		
		CurrentUser = (await GetUserInfoAsync().ConfigureAwait( false )) ?? throw new UnknownUserException();

		return CurrentUser;
	}
	
	public static ActionContext GetActionContext(string projectPath)
	{
		return new ActionContext(
				RequireUserInfo(projectPath),
				DateTime.Now
			);
	}

	public static async Task<ActionContext> GetActionContextAsync(string projectPath)
	{
		return new ActionContext(
				await RequireUserInfoAsync(projectPath),
				DateTime.Now
			);
	}

	public static void Save( ProjectInfo projectInfo, string filePath )
	{
		using Stream fileStream = File.Create( filePath );

		JsonSerializer.Serialize( fileStream, projectInfo, JsonSerializerOptions.Default );
	}
	
	public static async Task SaveAsync( ProjectInfo projectInfo, string filePath )
	{
		await using Stream fileStream = File.Create( filePath );

		await JsonSerializer.SerializeAsync( fileStream, projectInfo, JsonSerializerOptions.Default );
	}

	public static ProjectInfo Initialize(  ProjectDefinition projectDefinition, string? path )
	{
		ArgumentNullException.ThrowIfNull(projectDefinition);
		
		projectDefinition.Validate();
		
		if( string.IsNullOrWhiteSpace( path ) )
			path = Directory.GetCurrentDirectory();

		ProjectInfo projectInfo = new ( projectDefinition,
								GetActionContext(path),
								new Dictionary<string, PlanInfo>(),
								new Dictionary<string, ReleaseInfo>()
			);

		Store( projectInfo, path );
		
		return projectInfo;
	}

	public static async Task<ProjectInfo> InitializeAsync(  ProjectDefinition projectDefinition, string? path )
	{
		ArgumentNullException.ThrowIfNull(projectDefinition);
		
		projectDefinition.Validate();

		if( string.IsNullOrWhiteSpace( path ) )
			path = Directory.GetCurrentDirectory();

		ProjectInfo projectInfo = new ( projectDefinition,
								await GetActionContextAsync(path),
								new Dictionary<string, PlanInfo>(),
								new Dictionary<string, ReleaseInfo>()
			);

		await StoreAsync( projectInfo, path );
		
		return projectInfo;
	}

	
}