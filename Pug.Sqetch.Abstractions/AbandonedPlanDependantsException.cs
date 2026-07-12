namespace Pug.Sqetch;

public class AbandonedPlanDependantsException
	: Exception
{
	public List<PlanDependant> Dependants { get; }

	public AbandonedPlanDependantsException( List<PlanDependant> dependants )
	{
		Dependants = dependants;
	}
}