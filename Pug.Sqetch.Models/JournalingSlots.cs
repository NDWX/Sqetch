namespace Pug.Sqetch;

/// <summary>
/// What each <see cref="JournalingSlot"/> is called on disk and which parameters its statements may
/// use. Both the project info store and the bundle layout resolve file names through
/// <see cref="FileName"/>, so the two never drift apart.
/// </summary>
public static class JournalingSlots
{
	/// <summary>Bare parameter names — no provider prefix; the maintainer writes '@release'.</summary>
	public static class Parameters
	{
		public const string Project = "project";

		public const string Release = "release";

		public const string Plan = "plan";

		public const string Step = "step";

		public const string Description = "description";
	}

	/// <summary>Every slot, in declaration order.</summary>
	public static IReadOnlyList<JournalingSlot> All { get; } = Enum.GetValues<JournalingSlot>();

	public static IReadOnlyList<string> Names { get; } = All.Select( slot => slot.ToString() ).ToList();

	/// <summary>
	/// The two slots that return a result set. A query slot holds exactly one statement — a result
	/// set has to come from somewhere unambiguous.
	/// </summary>
	public static bool IsQuery( JournalingSlot slot )
		=> slot is JournalingSlot.GetLatestRelease or JournalingSlot.GetDeployedPlans;

	/// <summary>
	/// The slot's file name in the project's 'journaling' folder and in a bundle. Event slots take an
	/// 'On' prefix so they read as what they respond to; the queries and
	/// <see cref="JournalingSlot.PrepareJournal"/> are not events and take none.
	/// </summary>
	public static string FileName( JournalingSlot slot )
		=> IsQuery( slot ) || slot == JournalingSlot.PrepareJournal ? $"{slot}.sql" : $"On{slot}.sql";

	/// <summary>
	/// The parameters supplied to the slot's statements. Every slot gets
	/// <see cref="Parameters.Project"/>: the SQL is frozen into the bundle, so a journal shared by
	/// two projects could not otherwise tell them apart. There is deliberately no timestamp —
	/// 'now()' evaluates server-side, so a clock-skewed deployment host cannot disorder the journal —
	/// and no actor; the maintainer uses 'current_user'.
	/// </summary>
	public static IReadOnlyList<string> ParameterNames( JournalingSlot slot )
		=> slot switch
		{
			JournalingSlot.PrepareJournal or JournalingSlot.GetLatestRelease =>
				[Parameters.Project],

			JournalingSlot.GetDeployedPlans or JournalingSlot.RollingBackRelease
				or JournalingSlot.RolledBackRelease =>
				[Parameters.Project, Parameters.Release],

			JournalingSlot.RollingBackPlan or JournalingSlot.RolledBackPlan =>
				[Parameters.Project, Parameters.Release, Parameters.Plan],

			JournalingSlot.RollingBackStep or JournalingSlot.RolledBackStep =>
				[Parameters.Project, Parameters.Release, Parameters.Plan, Parameters.Step],

			JournalingSlot.DeployingRelease or JournalingSlot.ReleaseDeployed =>
				[Parameters.Project, Parameters.Release, Parameters.Description],

			JournalingSlot.DeployingPlan or JournalingSlot.PlanDeployed =>
				[Parameters.Project, Parameters.Release, Parameters.Plan, Parameters.Description],

			JournalingSlot.DeployingStep or JournalingSlot.StepDeployed =>
			[
				Parameters.Project, Parameters.Release, Parameters.Plan, Parameters.Step,
				Parameters.Description
			],

			_ => throw new ArgumentOutOfRangeException( nameof(slot), slot, "Unknown journaling slot." )
		};

	/// <summary>
	/// Resolves a slot named on a command line, case-insensitively. Rejects the numeric forms
	/// <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/> would otherwise accept.
	/// </summary>
	public static bool TryParse( string? name, out JournalingSlot slot )
	{
		slot = default;

		if( string.IsNullOrWhiteSpace( name ) || !char.IsLetter( name.Trim()[0] ) )
			return false;

		return Enum.TryParse( name.Trim(), ignoreCase: true, out slot ) && Enum.IsDefined( slot );
	}
}
