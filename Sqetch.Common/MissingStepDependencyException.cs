namespace Sqetch;

public class MissingStepDependencyException
	: Exception
{
	public string Dependency { get; }

	public MissingStepDependencyException( string dependency )
	{
		Dependency = dependency;
	}
}