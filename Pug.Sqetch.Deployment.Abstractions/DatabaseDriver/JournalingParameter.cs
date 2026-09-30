namespace Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

/// <summary>
/// One named value bound into a journaling statement or query.
/// </summary>
/// <param name="Name">
/// The bare parameter name, with no provider prefix — the maintainer writes '@release' in the SQL,
/// and adding or translating the prefix (or a positional placeholder) is the driver's job. Sqetch
/// never scans SQL for placeholders, so a driver for a provider that rejects unreferenced
/// parameters must do its own scan.
/// </param>
/// <param name="Value">The value bound to <paramref name="Name"/>.</param>
public readonly record struct JournalingParameter( string Name, string Value );
