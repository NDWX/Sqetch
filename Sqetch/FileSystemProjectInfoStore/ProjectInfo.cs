namespace Sqetch;

public record ProjectInfo( ProjectDefinition Definition, ActionContext Creation, ICollection<string> Plans, Dictionary<string, ReleaseInfo> Releases );