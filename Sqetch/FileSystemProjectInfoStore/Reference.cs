namespace Sqetch;

public record Reference(string Type, string Identifier) 
	: Reference<string>(Type, Identifier);

public record Reference<TIdentifier>( string Type, TIdentifier Identifier );