using Pug.Sqetch.Deployment;
using Spectre.Console.Cli;

CommandApp app = new ( SqetchDeployApp.CreateDefaultRegistrar() );

app.Configure( SqetchDeployApp.Configure );

return app.Run( args );
