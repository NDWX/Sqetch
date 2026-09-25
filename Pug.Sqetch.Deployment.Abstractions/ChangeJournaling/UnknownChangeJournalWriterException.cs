namespace Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

public class UnknownChangeJournalWriterException
	: DeploymentException
{
	public string? Name { get; }

	public IReadOnlyList<string> Known { get; }

	public UnknownChangeJournalWriterException( string name, IEnumerable<string> known )
		: base( $"Unknown change journal writer '{name}'; known writers: {string.Join( ", ", known )}." )
	{
		Name = name;
		Known = known.ToList();
	}

	/// <summary>No name was specified and the registry has no resolvable default.</summary>
	public UnknownChangeJournalWriterException( IEnumerable<string> known )
		: base( "No change journal writer specified and no default is registered; use --journal <name>." )
	{
		Name = null;
		Known = known.ToList();
	}
}
