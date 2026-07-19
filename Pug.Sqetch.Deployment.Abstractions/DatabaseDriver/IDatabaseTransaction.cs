using System.Data;

namespace Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

/// <summary>
/// Provides an abstraction for managing database transactions.
/// This interface defines methods for executing upgrade step scripts and
/// change journaling related statements and queries within the context of a transaction,
/// as well as committing or rolling back the transaction.
/// </summary>
public interface IDatabaseTransaction
{
    /// <summary>
    /// Executes the provided upgrade step script within the context of the transaction.
    /// </summary>
    /// <param name="script">The upgrade step script to be executed.</param>
    void ExecuteStepScript(string script);

    /// <summary>
    /// Executes the specified journaling statement within the context of the transaction.
    /// </summary>
    /// <param name="statement">The journaling statement to be executed.</param>
    void ExecuteJournalingStatement(string statement);

    /// <summary>
    /// Executes the specified journaling query within the context of the transaction and returns the result set.
    /// </summary>
    /// <param name="query">The journaling query to be executed.</param>
    /// <returns>An <see cref="IDataReader"/> containing the result set of the executed query.</returns>
    IDataReader ExecuteJournalingQuery(string query);

    /// <summary>
    /// Rolls back transaction
    /// </summary>
    void Rollback();

    /// <summary>
    /// Commits transaction
    /// </summary>
    void Commit();
}