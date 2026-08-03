namespace Babel.Gameplay.RunFlow
{
    public enum RunPhase
    {
        Booting = 0,
        Playing = 1,
        Paused = 2,
        ChoosingUpgrade = 3,
        Won = 4,
        Lost = 5,
        Transitioning = 6,
        Faulted = 7,
        Disposed = 8
    }

    public enum RunSpeed
    {
        One = 1,
        Two = 2,
        Four = 4
    }

    public enum RunExitRequest
    {
        None = 0,
        Restart = 1,
        ReturnToMenu = 2
    }

    public enum RunOutcome
    {
        Victory = 0,
        Defeat = 1
    }
}
