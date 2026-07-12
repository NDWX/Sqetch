namespace Pug.Sqetch;

public interface IScriptsStore : IDisposable
{
	StepScriptKeys PutStepScripts(string plan, string step, StepScripts scripts);

	void DeleteStepScripts( string plan, string step );

	StepScripts? GetStepScripts( string plan, string name );
}