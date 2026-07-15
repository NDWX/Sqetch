using Pug.Sqetch;
using Spectre.Console.Cli;

CommandApp app = new ();

app.Configure( SqetchApp.Configure );

return app.Run( args );
