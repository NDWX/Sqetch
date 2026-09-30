using System.Formats.Tar;
using System.IO.Compression;
using System.Text.Json;
using Spectre.Console.Cli.Testing;

namespace Pug.Sqetch.Tests.Cli;

/// <summary>
/// Drives 'sqetch bundle' through <see cref="CommandAppTester"/> against a temp project
/// directory, same fixture pattern as <see cref="CliTests"/>. Shares the "cli" collection
/// with it because both switch the process-wide current directory.
/// </summary>
[Collection( "cli" )]
public class BundleCliTests : IDisposable
{
	private readonly string _root;
	private readonly string _originalDirectory;

	public BundleCliTests()
	{
		_root = Path.Combine( Path.GetTempPath(), "sqetch-cli-tests", Guid.NewGuid().ToString( "N" ) );
		Directory.CreateDirectory( _root );

		_originalDirectory = Directory.GetCurrentDirectory();
		Directory.SetCurrentDirectory( _root );
	}

	public void Dispose()
	{
		Directory.SetCurrentDirectory( _originalDirectory );

		try
		{
			Directory.Delete( _root, recursive: true );
		}
		catch( IOException )
		{
		}
	}

	private static CommandAppResult Run( params string[] args )
	{
		CommandAppTester tester = new ();

		tester.Configure( SqetchApp.Configure );

		return tester.Run( args );
	}

	private void InitializeProject( bool withJournaling = true )
	{
		Assert.Equal( 0, Run( "project", "user", "tester", "tester@example.com" ).ExitCode );
		Assert.Equal( 0, Run( "project", "init", "demo", "--shard-by-prefix" ).ExitCode );

		if( withJournaling )
			SetJournaling();
	}

	/// <summary>
	/// Every journaling slot, which 'bundle' requires. The statements deliberately do not start with
	/// '--': a positional argument beginning with two dashes is parsed as an option, so SQL opening
	/// with a comment has to be supplied through --file or --stdin.
	/// </summary>
	private static void SetJournaling()
	{
		foreach( JournalingSlot slot in JournalingSlots.All )
			Assert.Equal( 0, Run( "journaling", "set", slot.ToString(), $"select '{slot}'" ).ExitCode );
	}

	/// <summary>Plan 'table' with step 'create' in finalized release 2026.07.</summary>
	private static void CreateFinalizedRelease()
	{
		Assert.Equal( 0, Run( "plan", "create", "--name", "table" ).ExitCode );
		Assert.Equal( 0, Run( "plan", "add-step", "--plan", "table", "--name", "create" ).ExitCode );
		Assert.Equal( 0, Run( "release", "create", "--name", "2026.07", "--plans", "table" ).ExitCode );
		Assert.Equal( 0, Run( "release", "finalize", "--name", "2026.07" ).ExitCode );
	}

	[Fact]
	public void BundleDefaultsToFinalizedZipAndPrintsThePath()
	{
		InitializeProject();
		CreateFinalizedRelease();

		CommandAppResult result = Run( "bundle" );

		Assert.Equal( 0, result.ExitCode );
		Assert.Matches( @"^demo-\d{14}\.zip$", result.Output );

		using ZipArchive archive = ZipFile.OpenRead( Path.Combine( _root, result.Output ) );

		Assert.Equal(
			[
				"manifest.json",
				"plans/table/steps/create/deploy.sql",
				"plans/table/steps/create/verify.sql",
				"plans/table/steps/create/rollback.sql"
			],
			archive.Entries.Select( x => x.FullName ).Where( x => !x.StartsWith( "journaling/" ) ).ToArray() );

		// the project's journaling SQL travels with the bundle, between the manifest and the plans
		Assert.Equal( 15, archive.Entries.Count( x => x.FullName.StartsWith( "journaling/" ) ) );
		Assert.Equal(
			"select 'PrepareJournal'",
			new StreamReader( archive.GetEntry( "journaling/PrepareJournal.sql" )!.Open() ).ReadToEnd().Trim() );

		using JsonDocument manifest = JsonDocument.Parse(
			new StreamReader( archive.GetEntry( "manifest.json" )!.Open() ).ReadToEnd() );

		Assert.Equal( "finalized", manifest.RootElement.GetProperty( "selection" ).GetString() );
		Assert.Equal( "demo", manifest.RootElement.GetProperty( "project" ).GetProperty( "name" ).GetString() );
		Assert.Equal( "2026.07", manifest.RootElement.GetProperty( "plans" )[0].GetProperty( "release" ).GetString() );
	}

	[Fact]
	public void TestSelectionIncludesUnreleasedPlansAfterReleasedOnes()
	{
		InitializeProject();
		CreateFinalizedRelease();

		Run( "plan", "create", "--name", "pending" );
		Run( "plan", "add-step", "--plan", "pending", "--name", "later" );

		CommandAppResult result = Run( "bundle", "--test", "--bundle-output-path", "test.zip" );

		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( "test.zip", result.Output );

		using ZipArchive archive = ZipFile.OpenRead( Path.Combine( _root, "test.zip" ) );
		using JsonDocument manifest = JsonDocument.Parse(
			new StreamReader( archive.GetEntry( "manifest.json" )!.Open() ).ReadToEnd() );

		JsonElement[] plans = manifest.RootElement.GetProperty( "plans" ).EnumerateArray().ToArray();

		Assert.Equal( ["table", "pending"], plans.Select( x => x.GetProperty( "name" ).GetString() ).ToArray() );
		Assert.Equal( "", plans[1].GetProperty( "release" ).GetString() );
		Assert.Equal( "test", manifest.RootElement.GetProperty( "selection" ).GetString() );
	}

	[Fact]
	public void SinceBundlesOnlyTheReleasesAfterTheNamedOne()
	{
		InitializeProject();
		CreateFinalizedRelease();

		Assert.Equal( 0, Run( "plan", "create", "--name", "index" ).ExitCode );
		Assert.Equal( 0, Run( "plan", "add-step", "--plan", "index", "--name", "create" ).ExitCode );
		Assert.Equal(
			0,
			Run(
					"release", "create", "--name", "2026.08", "--plans", "index",
					"--depends-on", "2026.07" )
				.ExitCode );
		Assert.Equal( 0, Run( "release", "finalize", "--name", "2026.08" ).ExitCode );

		CommandAppResult result = Run( "bundle", "--since", "2026.07", "--bundle-output-path", "since.zip" );

		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( "since.zip", result.Output );

		using ZipArchive archive = ZipFile.OpenRead( Path.Combine( _root, "since.zip" ) );
		using JsonDocument manifest = JsonDocument.Parse(
			new StreamReader( archive.GetEntry( "manifest.json" )!.Open() ).ReadToEnd() );

		JsonElement[] releases = manifest.RootElement.GetProperty( "releases" ).EnumerateArray().ToArray();

		Assert.Equal( ["2026.08"], releases.Select( x => x.GetProperty( "name" ).GetString() ).ToArray() );
		Assert.Equal( "2026.07", releases[0].GetProperty( "dependency" ).GetString() );

		JsonElement[] plans = manifest.RootElement.GetProperty( "plans" ).EnumerateArray().ToArray();

		Assert.Equal( ["index"], plans.Select( x => x.GetProperty( "name" ).GetString() ).ToArray() );
	}

	[Fact]
	public void SinceWithAnUnknownReleaseFailsWithFriendlyError()
	{
		InitializeProject();
		CreateFinalizedRelease();

		CommandAppResult result = Run( "bundle", "--since", "nope" );

		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "does not exist", result.Output );
	}

	[Fact]
	public void DirectoryBundleMaterializesTheTree()
	{
		InitializeProject();
		CreateFinalizedRelease();

		CommandAppResult result = Run( "bundle", "--bundle-type", "directory", "--bundle-output-path", "deploy" );

		Assert.Equal( 0, result.ExitCode );
		Assert.Equal( "deploy", result.Output );

		Assert.True( File.Exists( Path.Combine( _root, "deploy", "manifest.json" ) ) );
		Assert.True( File.Exists(
			Path.Combine( _root, "deploy", "plans", "table", "steps", "create", "deploy.sql" ) ) );
	}

	[Fact]
	public void TarGzBundleIsReadable()
	{
		InitializeProject();
		CreateFinalizedRelease();

		Assert.Equal( 0, Run( "bundle", "--bundle-type", "tar.gz", "--bundle-output-path", "b.tar.gz" ).ExitCode );

		using FileStream file = File.OpenRead( Path.Combine( _root, "b.tar.gz" ) );
		using GZipStream gzip = new ( file, CompressionMode.Decompress );
		using TarReader reader = new ( gzip );

		// the manifest leads so streaming consumers see it before the payload
		Assert.Equal( "manifest.json", reader.GetNextEntry()!.Name );
	}

	[Fact]
	public void MissingStepScriptFailsWithoutCreatingOutput()
	{
		InitializeProject();
		CreateFinalizedRelease();

		File.Delete( Directory.GetFiles( _root, "verify.sql", SearchOption.AllDirectories ).Single() );

		CommandAppResult result = Run( "bundle", "--bundle-output-path", "broken.zip" );

		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "missing script(s): verify", result.Output );
		Assert.False( File.Exists( Path.Combine( _root, "broken.zip" ) ) );
	}

	[Fact]
	public void NothingToBundleFailsWithFriendlyError()
	{
		InitializeProject();

		Run( "plan", "create", "--name", "unreleased-only" );

		CommandAppResult result = Run( "bundle" );

		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "nothing to bundle", result.Output );
	}

	/// <summary>
	/// A bundle journals through the project's own SQL, so bundling without it is refused — and the
	/// error names every unset slot, since a maintainer setting them up wants the whole list.
	/// </summary>
	[Fact]
	public void BundlingWithoutJournalingStatementsIsRefusedNamingThem()
	{
		InitializeProject( withJournaling: false );
		CreateFinalizedRelease();

		CommandAppResult result = Run( "bundle" );

		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "DeployingRelease", result.Output );
		Assert.Contains( "GetDeployedPlans", result.Output );
		Assert.Empty( Directory.GetFiles( _root, "*.zip" ) );
	}

	[Fact]
	public void JournalingIsListedAsUnsetUntilItIsSet()
	{
		InitializeProject( withJournaling: false );

		CommandAppResult before = Run( "journaling", "list" );

		Assert.Equal( 0, before.ExitCode );
		Assert.Equal( 15, before.Output.Split( '\n', StringSplitOptions.RemoveEmptyEntries ).Length );

		SetJournaling();

		Assert.DoesNotContain( "no", Run( "journaling", "list" ).Output );
	}

	[Fact]
	public void InvalidOptionsAreRejected()
	{
		InitializeProject();

		CommandAppResult both = Run( "bundle", "--test", "--finalized" );

		Assert.NotEqual( 0, both.ExitCode );
		Assert.Contains( "either --test or --finalized", both.Output );

		CommandAppResult unknownType = Run( "bundle", "--bundle-type", "rar" );

		Assert.NotEqual( 0, unknownType.ExitCode );
		Assert.Contains( "--bundle-type must be one of", unknownType.Output );
		Assert.Contains( "zip", unknownType.Output );
	}

	[Fact]
	public void ExistingOutputTargetIsRefusedIntact()
	{
		InitializeProject();
		CreateFinalizedRelease();

		File.WriteAllText( Path.Combine( _root, "taken.zip" ), "keep" );

		CommandAppResult result = Run( "bundle", "--bundle-output-path", "taken.zip" );

		Assert.Equal( 1, result.ExitCode );
		Assert.Contains( "already exists", result.Output );
		Assert.Equal( "keep", File.ReadAllText( Path.Combine( _root, "taken.zip" ) ) );
	}
}
