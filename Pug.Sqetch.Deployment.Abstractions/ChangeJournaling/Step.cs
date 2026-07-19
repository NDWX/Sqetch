namespace Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

/// <summary>
/// Represents a specific deployment step within a plan and release.
/// </summary>
/// <param name="Release">The release to which this deployment step belongs.</param>
/// <param name="Plan">The plan under which this deployment step is categorized.</param>
/// <param name="Name">The name of the deployment step.</param>
/// <param name="Description">A description of the deployment step.</param>
public record Step(string Release, string Plan, string Name, string Description) : DeploymentUnit(Name, Description);