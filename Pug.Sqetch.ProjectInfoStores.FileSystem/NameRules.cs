using System.Text.RegularExpressions;

namespace Pug.Sqetch.Stores.FileSystem;

internal static partial class NameRules
{
	// Names become path segments; the charset must exclude path separators, leading dots
	// (hidden files / traversal) and trailing dots (invalid on Windows).
	[GeneratedRegex( "^[A-Za-z0-9](?:[A-Za-z0-9._-]{0,126}[A-Za-z0-9_-])?$" )]
	private static partial Regex ValidName();

	public static void Ensure( string name, string kind )
	{
		ArgumentNullException.ThrowIfNull( name );

		if( !ValidName().IsMatch( name ) )
			throw new ArgumentException(
				$"{kind} name '{name}' is not a valid name; use letters, digits, '.', '_' or '-', " +
				"starting with a letter or digit and not ending with '.'" );
	}
}
