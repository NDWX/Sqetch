using System.Diagnostics.CodeAnalysis;
using Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

namespace Pug.Sqetch.Deployment;

/// <summary>
/// Resolves change journal writers by name. Ships empty; the host registers the writers it
/// wants to offer and may designate one as the default used when no name is specified.
/// </summary>
public class ChangeJournalWriterRegistry : IChangeJournalWriterRegistry
{
	private readonly Dictionary<string, Func<IChangeJournalWriter>> _factories = new ( StringComparer.OrdinalIgnoreCase );

	public IEnumerable<string> Names => _factories.Keys;

	public string? DefaultName { get; private set; }

	public void Register( string name, Func<IChangeJournalWriter> factory, bool asDefault = false )
	{
		ArgumentException.ThrowIfNullOrWhiteSpace( name );
		ArgumentNullException.ThrowIfNull( factory );

		_factories[name] = factory;

		if( asDefault )
			DefaultName = name;
	}

	public bool TryCreate( string? name, [NotNullWhen( true )] out IChangeJournalWriter? writer )
	{
		name ??= DefaultName ?? ( _factories.Count == 1 ? _factories.Keys.First() : null );

		if( name is not null && _factories.TryGetValue( name, out Func<IChangeJournalWriter>? create ) )
		{
			writer = create();
			return true;
		}

		writer = null;
		return false;
	}

	public IChangeJournalWriter Create( string? name )
		=> TryCreate( name, out IChangeJournalWriter? writer )
			? writer
			: name is null
				? throw new UnknownChangeJournalWriterException( Names )
				: throw new UnknownChangeJournalWriterException( name, Names );
}
