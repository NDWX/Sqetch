namespace Pug.Sqetch;

public class StepRequiredException( IEnumerable<string> dependants ) : Exception
{
	public IEnumerable<string> Dependants { get; } = dependants;
}