namespace Babel.Gameplay.RunFlow
{
    public enum RunSystemStage
    {
        GameplayCommands = 100,
        TimersAndStatus = 200,
        HumanBrain = 300,
        NavigationAndWorkIntent = 400,
        Abilities = 500,
        Combat = 600,
        Death = 700,
        BabelWork = 800,
        Progression = 900,
        Encounter = 1000,
        RunRules = 1100,
        Presentation = 1200
    }

    public interface IRunSystem
    {
        RunSystemStage Stage { get; }
        int Order { get; }
        int TickInterval { get; }
        void Step(RunContext context, double fixedDeltaSeconds);
    }
}
