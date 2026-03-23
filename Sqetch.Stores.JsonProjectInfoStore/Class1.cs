using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace Sqetch.Stores.JsonProjectInfoStore;

public class JsonProjectInfoStore : IProjectInfoStore
{
	private readonly string _filePath;
	private SafeFileHandle _fileHandle;
	private readonly DateTime _fileCreationTime;
	private ProjectDefinition _projectDefinition;
	private IDictionary<string, ProjectPlan> _plans;
	private IDictionary<string, ProjectRelease> _releases;
	
	public JsonProjectInfoStore( string filePath )
	{
		_filePath = filePath;
		_fileHandle = OpenFile( filePath );
		_fileCreationTime = File.GetCreationTime( _fileHandle);
		projectInfo = Read( _fileHandle );
	}

	internal JsonProjectInfoStore( SafeFileHandle fileHandle )
	{
		_fileHandle = fileHandle ?? throw new ArgumentNullException( nameof(fileHandle) );
		_fileCreationTime = File.GetCreationTime( _fileHandle);
		projectInfo = Read( _fileHandle );
	}

	private static SafeFileHandle OpenFile( string filePath )
	{
		if( string.IsNullOrWhiteSpace( filePath ) )
			throw new ArgumentException( "Value cannot be null or whitespace.", nameof(filePath) );

		filePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables( filePath ));
		
		if( !File.Exists( filePath ) )
			throw new FileNotFoundException();
		
		SafeFileHandle fileHandle = File.OpenHandle( filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
		
		return fileHandle;
	}

	private void Read( SafeFileHandle fileHandle )
	{
		using Stream fileStream = new FileStream( fileHandle, FileAccess.ReadWrite );

		Project? info = JsonSerializer.Deserialize<Project>( fileStream );

		if( info is null )
			throw new JsonProjectInfoException();
		
		
	}

	private void WriteToFile()
	{
		_fileHandle.Close();
		_fileHandle = File.OpenHandle( _filePath, FileMode.Create, FileAccess.Write, FileShare.None );
		File.SetCreationTime( _fileHandle, _fileCreationTime );

		using Stream fileStream = new FileStream( _fileHandle, FileAccess.Write );
		JsonSerializer.Serialize( fileStream, projectInfo );
	}
	
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

	public void Dispose()
	{
		// TODO release managed resources here
	}

	public ProjectDefinition GetDefinition()
	{
		throw new NotImplementedException();
	}

	public void AddPlan( ObjectDefinition definition, IEnumerable<string> dependencies, ActionContext context )
	{
		throw new NotImplementedException();
	}

	public void UpdatePlan( ObjectDefinition definition )
	{
		throw new NotImplementedException();
	}

	public bool PlanExists( string name )
	{
		throw new NotImplementedException();
	}

	public IDictionary<string, ProjectPlan> GetPlans( string? release = null )
	{
		throw new NotImplementedException();
	}

	public ProjectPlan? GetPlan( string name )
	{
		throw new NotImplementedException();
	}

	public IEnumerable<string> GetPlanDependencies( string name )
	{
		throw new NotImplementedException();
	}

	public IEnumerable<string> GetPlanDependants( string name )
	{
		throw new NotImplementedException();
	}

	public void SetPlanDependencies( string name, IEnumerable<string> dependencies )
	{
		throw new NotImplementedException();
	}

	public void AddStep( string plan, ObjectDefinition definition, IEnumerable<string> dependencies, ActionContext context )
	{
		throw new NotImplementedException();
	}

	public void UpdateStep( string plan, ObjectDefinition definition )
	{
		throw new NotImplementedException();
	}

	public bool StepExists( string plan, string name )
	{
		throw new NotImplementedException();
	}

	public IDictionary<string, ProjectElement> GetSteps( string plan )
	{
		throw new NotImplementedException();
	}

	public ProjectElement? GetStep( string plan, string name )
	{
		throw new NotImplementedException();
	}

	public IEnumerable<string> GetStepDependencies( string plan, string name )
	{
		throw new NotImplementedException();
	}

	public void SetStepDependencies( string plan, string name, IEnumerable<string> dependencies )
	{
		throw new NotImplementedException();
	}

	public IEnumerable<string> GetStepDependants( string plan, string name )
	{
		throw new NotImplementedException();
	}

	public void DeletePlan( string name )
	{
		throw new NotImplementedException();
	}

	public void DeleteStep( string plan, string name )
	{
		throw new NotImplementedException();
	}

	public bool VersionExists( string name )
	{
		throw new NotImplementedException();
	}

	public IEnumerable<ProjectRelease> GetReleases(
		string prefix = "", bool released = false, Range<DateTime>? createTimestamp = null, Range<DateTime>? finalizeTimestamp = null
	)
	{
		throw new NotImplementedException();
	}

	public ProjectRelease? GetRelease( string name )
	{
		throw new NotImplementedException();
	}

	public void AddRelease( ObjectDefinition definition, ActionContext context )
	{
		throw new NotImplementedException();
	}

	public void SetReleaseContext( string release, ActionContext releaseContext )
	{
		throw new NotImplementedException();
	}

	public IEnumerable<ProjectRelease> GetReleaseDependants( string release )
	{
		throw new NotImplementedException();
	}

	public IEnumerable<ProjectElement> GetReleasePlans( string release )
	{
		throw new NotImplementedException();
	}

	public void AddReleasePlan( string release, string name, ActionContext context )
	{
		throw new NotImplementedException();
	}

	public void DeleteReleasePlan( string release, string name, ActionContext context )
	{
		throw new NotImplementedException();
	}
}