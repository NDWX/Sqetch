namespace Sqetch;

public record ReleaseInfo( ReleaseDefinition Definition, ActionContext AdditionContext, ICollection<string> Plans );