using System.Text.Json;
using Pug.Sqetch;
using Pug.Sqetch.Stores.FileSystem;

internal static class SqetchCli
{
    // options that never take a value
    private static readonly HashSet<string> Flags = new (
        ["global", "finalized", "open", "unreleased", "all-unreleased", "with-dependencies", "with-dependants"],
        StringComparer.OrdinalIgnoreCase );

    private static readonly JsonSerializerOptions UserFileOptions = new ()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static int Run( string[] args )
    {
        try
        {
            return Execute( args );
        }
        catch( Exception exception )
        {
            Console.Error.WriteLine( $"error: {Describe( exception )}" );
            return 1;
        }
    }

    private static int Execute( string[] args )
    {
        if( args.Length == 0 || args[0] is "help" or "--help" or "-h" )
            return Help();

        Arguments arguments = Arguments.Parse( args.AsSpan( 1 ).ToArray(), Flags );

        string projectPath = Directory.GetCurrentDirectory();

        switch( args[0] )
        {
            case "init":
                Initialize( projectPath, arguments );
                break;

            case "user":
                SaveUser( projectPath, arguments );
                break;

            case "add-plan":
                using( ProjectHandle handle = OpenProject( projectPath ) )
                    handle.Project.Add( new PlanDefinition(
                        arguments.Positional( 0, "name" ),
                        arguments.Option( "description" ) ?? string.Empty,
                        arguments.List( "depends-on" ) ) );
                break;

            case "add-step":
                AddStep( projectPath, arguments );
                break;

            case "create-release":
                using( ProjectHandle handle = OpenProject( projectPath ) )
                {
                    ReleaseDefinition definition = new (
                        arguments.Positional( 0, "name" ),
                        arguments.Option( "description" ) ?? string.Empty,
                        arguments.Option( "depends-on" ) ?? string.Empty );

                    if( arguments.Has( "plans" ) )
                        handle.Project.CreateRelease( definition, arguments.List( "plans" ) );
                    else
                        handle.Project.CreateRelease( definition, arguments.Has( "all-unreleased" ) );
                }
                break;

            case "release-plan":
                using( ProjectHandle handle = OpenProject( projectPath ) )
                    handle.Project.AddPlanToRelease(
                        arguments.Positional( 0, "release" ), arguments.Positional( 1, "plan" ),
                        arguments.Has( "with-dependencies" ) );
                break;

            case "unrelease-plan":
                using( ProjectHandle handle = OpenProject( projectPath ) )
                    handle.Project.RemovePlanFromRelease(
                        arguments.Positional( 0, "release" ), arguments.Positional( 1, "plan" ),
                        arguments.Has( "with-dependants" ) );
                break;

            case "delete-plan":
                using( ProjectHandle handle = OpenProject( projectPath ) )
                    handle.Project.DeletePlan( arguments.Positional( 0, "plan" ) );
                break;

            case "delete-step":
                using( ProjectHandle handle = OpenProject( projectPath ) )
                    handle.Project.Delete( arguments.Positional( 0, "plan" ), arguments.Positional( 1, "step" ) );
                break;

            case "finalize":
                using( ProjectHandle handle = OpenProject( projectPath ) )
                    handle.Project.FinalizeRelease( arguments.Positional( 0, "release" ) );
                break;

            case "plans":
                ListPlans( projectPath, arguments );
                break;

            case "releases":
                ListReleases( projectPath, arguments );
                break;

            case "steps":
                ListSteps( projectPath, arguments );
                break;

            case "reindex":
                using( FileSystemProjectStores stores = FileSystemProjectStores.Open( projectPath ) )
                    stores.Reindex();

                Console.WriteLine( "plan index rebuilt" );
                break;

            default:
                Console.Error.WriteLine( $"unknown command '{args[0]}'" );
                return Help();
        }

        return 0;
    }

    private static void Initialize( string projectPath, Arguments arguments )
    {
        ShardingConfiguration? sharding = null;

        if( arguments.Has( "shard-by-prefix" ) )
        {
            string delimiter = arguments.Option( "shard-by-prefix" ) is { Length: > 0 } value ? value : ".";

            sharding = new ShardingConfiguration(
                NamePrefixShardingStrategy.StrategyName,
                new Dictionary<string, string> { [NamePrefixShardingStrategy.DelimiterOption] = delimiter } );
        }

        FileSystemProjectStores.Initialize(
                new ProjectDefinition(
                    arguments.Positional( 0, "name" ),
                    arguments.Option( "description" ) ?? string.Empty,
                    arguments.Option( "engine" ) ?? string.Empty ),
                new ActionContext( RequireUser( projectPath ), DateTime.Now ),
                projectPath, sharding )
            .Dispose();

        Console.WriteLine( $"initialized Sqetch project in {projectPath}" );
    }

    private static void SaveUser( string projectPath, Arguments arguments )
    {
        UserInfo user = new ( arguments.Positional( 0, "name" ), arguments.Positional( 1, "email" ) );

        string directory = arguments.Has( "global" )
            ? Environment.GetFolderPath( Environment.SpecialFolder.UserProfile )
            : projectPath;

        File.WriteAllText(
            Path.Combine( directory, Defaults.UserInfoStoreFileName ),
            JsonSerializer.Serialize( user, UserFileOptions ) + "\n" );

        Console.WriteLine( $"user identity saved to {directory}" );
    }

    private static void AddStep( string projectPath, Arguments arguments )
    {
        using ProjectHandle handle = OpenProject( projectPath );

        string plan = arguments.Positional( 0, "plan" );
        string name = arguments.Positional( 1, "name" );

        StepScriptKeys keys = handle.Project.Add(
            new StepDefinition( plan, name, arguments.Option( "description" ) ?? string.Empty, arguments.List( "depends-on" ) ),
            plan );

        // seed empty script files so they are ready to edit and visible to git
        foreach( (string key, string kind) in new[]
                {
                    (keys.DeployScript, "deploy"), (keys.VerifyScript, "verify"), (keys.RollbackScript, "rollback")
                } )
        {
            string path = Path.Combine( projectPath, key.Replace( '/', Path.DirectorySeparatorChar ) );

            if( !File.Exists( path ) )
                File.WriteAllText( path, $"-- {kind} script for step '{name}' of plan '{plan}'\n" );

            Console.WriteLine( key );
        }
    }

    private static void ListPlans( string projectPath, Arguments arguments )
    {
        using FileSystemProjectStores stores = FileSystemProjectStores.Open( projectPath );

        string? release = arguments.Option( "release" ) ?? ( arguments.Has( "unreleased" ) ? string.Empty : null );

        foreach( ProjectPlan plan in stores.InfoStore.GetPlans( release ).Values.OrderBy( x => x.Definition.Name ) )
        {
            string membership = plan.Release.Length == 0 ? "(unreleased)" : plan.Release;

            Console.WriteLine( $"{plan.Definition.Name}  {membership}  {plan.Definition.Description}" );
        }
    }

    private static void ListReleases( string projectPath, Arguments arguments )
    {
        using FileSystemProjectStores stores = FileSystemProjectStores.Open( projectPath );

        string prefix = arguments.Option( "prefix" ) ?? string.Empty;

        IEnumerable<ProjectRelease> releases = [];

        if( !arguments.Has( "finalized" ) )
            releases = releases.Concat( stores.InfoStore.GetReleases( prefix ) );

        if( !arguments.Has( "open" ) )
            releases = releases.Concat( stores.InfoStore.GetReleases( prefix, released: true ) );

        foreach( ProjectRelease release in releases.OrderBy( x => x.Definition.Name ) )
        {
            string state = release.Finalized is null ? "open" : $"finalized {release.Finalized.Timestamp:yyyy-MM-dd}";
            string dependency = release.Definition.Dependency.Length == 0 ? "" : $"  after {release.Definition.Dependency}";

            Console.WriteLine( $"{release.Definition.Name}  [{state}]{dependency}  {release.Definition.Description}" );
        }
    }

    private static void ListSteps( string projectPath, Arguments arguments )
    {
        using FileSystemProjectStores stores = FileSystemProjectStores.Open( projectPath );

        string plan = arguments.Positional( 0, "plan" );

        foreach( ProjectElement step in stores.InfoStore.GetSteps( plan ).Values.OrderBy( x => x.Definition.Name ) )
            Console.WriteLine( $"{step.Definition.Name}  {step.Definition.Description}" );
    }

    private static ProjectHandle OpenProject( string projectPath )
    {
        FileSystemProjectStores stores = FileSystemProjectStores.Open( projectPath );

        IProject project = ProjectFactory.Create(
            stores.InfoStore, stores.ScriptsStore,
            new ReleaseChainDependencyDeterminator( stores.InfoStore ),
            RequireUser( projectPath ) );

        return new ProjectHandle( stores, project );
    }

    private static UserInfo RequireUser( string projectPath )
    {
        string[] candidates =
        [
            Path.Combine( projectPath, Defaults.UserInfoStoreFileName ),
            Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.UserProfile ), Defaults.UserInfoStoreFileName )
        ];

        foreach( string candidate in candidates )
        {
            if( !File.Exists( candidate ) )
                continue;

            UserInfo? user = JsonSerializer.Deserialize<UserInfo>( File.ReadAllText( candidate ), UserFileOptions );

            if( user is not null )
                return user;
        }

        throw new UnknownUserException();
    }

    private static string Describe( Exception exception )
        => exception switch
        {
            UnknownUserException => "user identity not configured; run 'sqetch user <name> <email> [--global]'",
            DuplicatePlanNameException => "a plan with this name already exists",
            DuplicateStepNameException => "a step with this name already exists in the plan",
            DuplicateReleaseNameException => "a release with this name already exists",
            UnknownPlanException unknownPlan => $"plan '{unknownPlan.Name}' does not exist",
            UnknownReleaseException => "release does not exist",
            ReleaseFinalizedException => "the release is finalized and can no longer be changed",
            PlanFinalizedException => "the plan belongs to a finalized release and can no longer be changed",
            EmptyReleaseException => "a release without plans cannot be finalized",
            AbandonedPlanDependantsException => "other plans in the release depend on this plan (use --with-dependants to remove them too)",
            IncompleteDefinitionException => "the definition is missing a name",
            _ => exception.Message
        };

    private static int Help()
    {
        Console.WriteLine(
            """
            usage: sqetch <command> [arguments] [options]

            project
              init <name> [--description <text>] [--engine <engine>] [--shard-by-prefix [<delimiter>]]
              user <name> <email> [--global]
              reindex

            plans
              add-plan <name> [--description <text>] [--depends-on <plan,plan>]
              delete-plan <plan>
              add-step <plan> <name> [--description <text>] [--depends-on <step,step>]
              delete-step <plan> <step>
              plans [--release <release> | --unreleased]
              steps <plan>

            releases
              create-release <name> [--description <text>] [--depends-on <release>] [--plans <plan,plan> | --all-unreleased]
              release-plan <release> <plan> [--with-dependencies]
              unrelease-plan <release> <plan> [--with-dependants]
              finalize <release>
              releases [--prefix <prefix>] [--open | --finalized]
            """ );

        return 0;
    }

    private sealed class ProjectHandle( FileSystemProjectStores stores, IProject project ) : IDisposable
    {
        public IProject Project { get; } = project;

        public void Dispose()
        {
            Project.Dispose();
            stores.Dispose();
        }
    }
}