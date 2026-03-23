namespace Sqetch;

public record PlanInfo( ObjectDefinition Definition, ActionContext AdditionContext, Dictionary<string, StepInfo> Steps );