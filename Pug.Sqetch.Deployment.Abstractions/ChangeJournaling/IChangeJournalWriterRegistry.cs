using System.Diagnostics.CodeAnalysis;

namespace Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

/// <summary>
/// Resolves change journal writers by name. Ships empty; the host registers the writers it
/// wants to offer and may designate one as the default used when no name is specified.
/// </summary>
public interface IChangeJournalWriterRegistry
{
	IEnumerable<string> Names { get; }

	/// <summary>Name of the designated default writer, when one has been registered as such.</summary>
	string? DefaultName { get; }

	void Register( string name, Func<IChangeJournalWriter> factory, bool asDefault = false );

	/// <summary>
	/// A null <paramref name="name"/> resolves to the designated default, or to the only
	/// registered writer when there is exactly one.
	/// </summary>
	bool TryCreate( string? name, [NotNullWhen( true )] out IChangeJournalWriter? writer );

	/// <summary>Throws <see cref="UnknownChangeJournalWriterException"/> when resolution fails.</summary>
	IChangeJournalWriter Create( string? name );
}
