namespace Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

/// <summary>
/// Interface for database change journaling
/// </summary>
public interface IChangeJournalWriter
{
    /// <summary>
    /// Called when deployment of a release is beginning, before deployment of plans and steps.
    /// </summary>
    /// <param name="unit">Deployment unit representing the release</param>
    /// <param name="transaction">Database transaction for the deployment</param>
    void DeployingRelease(DeploymentUnit unit, IDatabaseTransaction transaction);

    /// <summary>
    /// Called when deployment of a plan is beginning, before deployment of steps.
    /// </summary>
    /// <param name="unit">Deployment unit representing the plan being deployed</param>
    /// <param name="transaction">Database transaction for the deployment</param>
    void DeployingPlan(Plan unit, IDatabaseTransaction transaction);

    /// <summary>
    /// Called just as a step script is about to be executed.
    /// </summary>
    /// <param name="unit">Deployment unit representing the step being deployed</param>
    /// <param name="transaction">Database transaction for the deployment</param>
    void DeployingStep(Step unit, IDatabaseTransaction transaction);

    /// <summary>
    /// Called after a step script has been successfully executed.
    /// </summary>
    /// <param name="release">The name of the release to which the step belongs.</param>
    /// <param name="plan">The name of the plan that contains the deployed step.</param>
    /// <param name="name">The name of the deployed step.</param>
    /// <param name="transaction">Database transaction used during the step's deployment.</param>
    void StepDeployed(string release, string plan, string name, IDatabaseTransaction transaction);

    /// <summary>
    /// Called when deployment of a plan has completed successfully.
    /// </summary>
    /// <param name="release">The name of the release to which the plan belongs.</param>
    /// <param name="name">The name of the deployed plan.</param>
    /// <param name="transaction">Database transaction associated with the deployment.</param>
    void PlanDeployed(string release, string name, IDatabaseTransaction transaction);

    /// <summary>
    /// Called when deployment of a release is completed successfully.
    /// </summary>
    /// <param name="name">The name of the deployed release</param>
    /// <param name="transaction">Database transaction used during the deployment</param>
    void ReleaseDeployed(string name, IDatabaseTransaction transaction);

    /// <summary>
    /// Called when a release rollback is initiated, before rolling back associated plans and steps.
    /// </summary>
    /// <param name="name">The name of the release to be rolled back</param>
    /// <param name="transaction">Database transaction for the rollback process</param>
    void RollingBackRelease(string name, IDatabaseTransaction transaction);

    /// <summary>
    /// Called when a rollback of a plan is initiated.
    /// </summary>
    /// <param name="release">The name of the release that the plan belongs to</param>
    /// <param name="planname">The name of the plan being rolled back</param>
    /// <param name="transaction">Database transaction for managing the rollback process</param>
    void RollingBackPlan(string release, string planname, IDatabaseTransaction transaction);

    /// <summary>
    /// Called when rolling back a specific step during the deployment process.
    /// </summary>
    /// <param name="release">The name of the release containing the plan and step being rolled back.</param>
    /// <param name="plan">The name of the plan containing the step being rolled back.</param>
    /// <param name="name">The name of the step being rolled back.</param>
    /// <param name="transaction">The database transaction associated with the rollback operation.</param>
    void RollingBackStep(string release, string plan, string name, IDatabaseTransaction transaction);

    /// <summary>
    /// Called when a step within a plan has been rolled back during the rollback process of a deployment.
    /// </summary>
    /// <param name="release">The name of the release containing the plan and step being rolled back.</param>
    /// <param name="plan">The name of the plan containing the step being rolled back.</param>
    /// <param name="name">The name of the step that was rolled back.</param>
    /// <param name="transaction">Database transaction used for the rollback operation.</param>
    void RolledBackStep(string release, string plan, string name, IDatabaseTransaction transaction);

    /// <summary>
    /// Called after rolling back a specific deployment plan within a release.
    /// </summary>
    /// <param name="release">The name of the release to which the plan belongs.</param>
    /// <param name="name">The name of the plan that was rolled back.</param>
    /// <param name="transaction">Database transaction used during the rollback operation.</param>
    void RolledBackPlan(string release, string name, IDatabaseTransaction transaction);

    /// <summary>
    /// Called after a release has been fully rolled back.
    /// </summary>
    /// <param name="name">Name of the release that has been rolled back</param>
    /// <param name="transaction">Database transaction used for the rollback operation</param>
    void RolledBackRelease(string name, IDatabaseTransaction transaction);

    /// <summary>
    /// Retrieves the latest journaled release regardless of completion.
    /// </summary>
    /// <param name="driver">The database driver used to query the release information.</param>
    /// <returns>The latest journaled release; null when nothing has been journaled yet. An empty name is valid: the pseudo-release of unreleased plans. Completed is true only when the release's completion was journaled; false means plans of that release remain to deploy.</returns>

    JournaledRelease? GetLatestRelease(IDatabaseDriver driver);

    /// <summary>
    /// Retrieves the list of plans that have been successfully deployed for a specific release from the database.
    /// </summary>
    /// <param name="release">The name of the release for which to retrieve the deployed plans.</param>
    /// <param name="driver">The database driver used to query the deployment data.</param>
    /// <returns>The names of the plans of the specified release that have been deployed, in no particular order and without duplicates. Deployment of an incompletely deployed release treats them as a set, skipping the plans they name and deploying the rest of the release in bundle order, so no ordering guarantee is required of the implementation.</returns>
    IEnumerable<string> GetDeployedPlans(string release, IDatabaseDriver driver);
}