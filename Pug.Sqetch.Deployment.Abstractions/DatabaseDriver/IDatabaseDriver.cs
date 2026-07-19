using System.Data;

namespace Pug.Sqetch.Deployment.DatabaseDriver.Abstractions;

/// <summary>
/// Represents the core interface for database driver implementations, providing methods for initiating
/// transactions and executing statements or queries related to journaling where transaction is not required.
/// </summary>
public interface IDatabaseDriver
{
    /// <summary>
    /// Return an instance of IDatabaseTransaction that can be used to execute change and journaling scripts within a single transaction.
    /// </summary>
    /// <returns>Instance of IDatabaseTransaction</returns>
    public IDatabaseTransaction BeginTransaction();

    /// <summary>
    /// Execute change journaling related statement in its own transaction
    /// </summary>
    /// <param name="statement">Change journaling related statement</param>
    void ExecuteJournalingStatement(string statement);

    /// <summary>
    /// Execute change journaling related query in its own transaction
    /// </summary>
    /// <param name="query">Change journaling related query</param>
    /// <returns>DataReader containing results of the query</returns>
    IDataReader ExecuteJournalingQuery(string query);

}