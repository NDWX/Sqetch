using Spectre.Console;

namespace Pug.Sqetch.Deployment;

/// <summary>
/// Reports deployment progress to the console and, when a log path is given, appends the
/// same lines timestamped to the log file.
/// </summary>
public sealed class ConsoleDeploymentListener : IDeploymentListener, IDisposable
{
	private readonly IAnsiConsole _console;
	private readonly StreamWriter? _log;

	public bool UpToDate { get; private set; }

	public ConsoleDeploymentListener( IAnsiConsole console, string? logPath )
	{
		_console = console;

		if( logPath is null )
			return;

		string? parent = Path.GetDirectoryName( logPath );

		if( !string.IsNullOrEmpty( parent ) )
			Directory.CreateDirectory( parent );

		_log = new StreamWriter( logPath, append: true );
	}

	public void ContinuingFrom( string release ) => Report( $"continuing from {Name( release )}" );

	public void SkippingRelease( string release ) => Report( $"skipping {Name( release )}: already deployed" );

	public void SkippingPlan( string release, string plan ) => Report( $"skipping plan '{plan}' of {Name( release )}: already deployed" );

	public void DeployingRelease( string release ) => Report( $"deploying {Name( release )}" );

	public void DeployingPlan( string release, string plan ) => Report( $"deploying plan '{plan}' of {Name( release )}" );

	public void DeployingStep( string release, string plan, string step ) => Report( $"deploying step '{step}' of plan '{plan}'" );

	public void StepDeployed( string release, string plan, string step ) => Report( $"deployed step '{step}' of plan '{plan}'" );

	public void PlanDeployed( string release, string plan ) => Report( $"deployed plan '{plan}' of {Name( release )}" );

	public void ReleaseDeployed( string release ) => Report( $"deployed {Name( release )}" );

	public void Committed() => Report( "committed" );

	public void NothingToDeploy()
	{
		UpToDate = true;

		Report( "nothing to deploy" );
	}

	/// <summary>Logged only; the console shows the exception handler's error line.</summary>
	public void Failed( string message ) => Log( $"failed: {message}" );

	public void Dispose() => _log?.Dispose();

	private static string Name( string release ) => release.Length == 0 ? "unreleased plans" : $"release '{release}'";

	private void Report( string line )
	{
		_console.WriteLine( line );

		Log( line );
	}

	private void Log( string line ) => _log?.WriteLine( $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {line}" );
}
