namespace Pug.Sqetch.Deployment;

/// <summary>
/// When a deployment should undo itself. Rollback is a <em>compensating</em> pass — it runs the
/// rollback script of everything this run committed, in reverse — so it is independent of
/// <see cref="DeploymentCommitLevel"/>: at plan level the earlier plans are already durable, and
/// no database transaction could take them back.
/// </summary>
public enum DeploymentRollbackMode
{
	/// <summary>Leave whatever committed in place; a failure rolls back only its open transaction.</summary>
	None,

	/// <summary>On failure, undo everything this run had already committed, then report the failure.</summary>
	OnError,

	/// <summary>
	/// Undo a deployment that succeeded. For proving a test bundle applies cleanly and leaving the
	/// database as it was; refused for a bundle of finalized releases, which is not something to
	/// undo on purpose.
	/// </summary>
	OnSuccess
}
