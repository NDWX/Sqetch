namespace Pug.Sqetch;

public class UnknownPlanException
	: Exception
{
	public string Name { get; }

	public UnknownPlanException( string name ) 
		: base($"Plan '{name}' does not exist.")
	{
		Name = name;
	}
}