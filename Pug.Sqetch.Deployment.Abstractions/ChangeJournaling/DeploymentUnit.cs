namespace Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

/// <summary>
/// Represent a deployment unit, which can be a release, plan, or step
/// </summary>
/// <param name="Name">Name of the release, plan, or step</param>
/// <param name="Description">Description of the release, plan, or step</param>
public record DeploymentUnit(string Name, string Description);