namespace Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

/// <summary>
/// Represents a deployment plan.
/// </summary>
/// <param name="Release">The release to which this plan belongs.</param>
/// <param name="Name">The name of the plan for identification purposes.</param>
/// <param name="Description">A description of the plan, detailing its purpose and scope.</param>
public record Plan(string Release, string Name, string Description) : DeploymentUnit(Name, Description);