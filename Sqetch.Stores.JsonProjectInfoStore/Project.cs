namespace Sqetch.Stores.JsonProjectInfoStore;

public record Element( string Description, ActionContext Registration );

public record Step( string Description, ICollection<string> Dependencies, ActionContext Registration );

public record Plan(string Description, ICollection<string> Dependencies,  ActionContext Registration, IDictionary<string, Step> Steps, string Release);

public record Release( string Description, string Dependency, ActionContext Registration, ActionContext Finalized );

public record Project( ProjectDefinition Definition, ActionContext Creation, IDictionary<string, Release> Releases, IDictionary<string, Plan> Plans );