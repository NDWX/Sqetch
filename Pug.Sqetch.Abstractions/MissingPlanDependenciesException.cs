using Pug.Sqetch.Models;

namespace Pug.Sqetch;

public class MissingPlanDependenciesException(
	IDictionary<string, (ProjectPlan, ICollection<string>)> missingDependencies
)
	: Exception
{
	public IDictionary<string, (ProjectPlan, ICollection<string>)> MissingDependencies { get; } = missingDependencies;
}