namespace Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

/// <summary>
/// Represents the core interface for database driver implementations. It is purely a transaction
/// factory: journaling is always transactional, so there is no non-transactional counterpart to
/// <see cref="BeginTransaction"/> — running the journal's queries inside one transaction keeps a
/// deployment's resume decision read-consistent instead of two independent auto-commit round trips.
/// </summary>
public interface IDatabaseDriver
{
    /// <summary>
    /// Return an instance of IDatabaseTransaction that can be used to execute change and journaling scripts within a single transaction.
    /// </summary>
    /// <returns>Instance of IDatabaseTransaction</returns>
    public IDatabaseTransaction BeginTransaction();
}