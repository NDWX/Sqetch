namespace Sqetch;

public class Plan : IDisposable
{
	private readonly IProjectInfoStore _infoStore;
	private readonly IObjectRepository _scriptsRepository;
	public PlanInfo Info { get; }

	internal Plan(IProjectInfoStore infoStore, IObjectRepository scriptsRepository )
	{
		_infoStore = infoStore ?? throw new ArgumentNullException( nameof(infoStore) );
		_scriptsRepository = scriptsRepository ?? throw new ArgumentNullException( nameof(scriptsRepository) );
		
		
	}
	
	public ObjectDefinition Definition => Info.Definition;
	public ActionContext AdditionContext => Info.AdditionContext;
	public IDictionary<string, StepInfo> Steps => Info.Steps;

	public StepInfo Add( StepDefinition step )
	{
		ArgumentNullException.ThrowIfNull(step);

		step.Validate();
		throw new NotImplementedException();
	}

	public void Modify( PlanDefinition definition )
	{
		ArgumentNullException.ThrowIfNull(definition);

		definition.Validate();
		throw new NotImplementedException();
	}

	public async Task ModifyAsync( PlanDefinition definition )
	{
		ArgumentNullException.ThrowIfNull(definition);

		definition.Validate();
		throw new NotImplementedException();
	}

	public StepInfo Add( StepDefinition step )
	{
		ArgumentNullException.ThrowIfNull(step);

		step.Validate();
		throw new NotImplementedException();
	}

	public async Task<StepInfo> AddAsync( StepDefinition step )
	{
		throw new NotImplementedException();
	}
	
	public void RemoveStep(string identifier)
	{
		throw new NotImplementedException();
	}
	
	public async Task RemoveStepAsync(string identifier)
	{
		throw new NotImplementedException();
	}

	public void Dispose()
	{
		_object.Dispose();
		
		GC.SuppressFinalize( this );
	}
}