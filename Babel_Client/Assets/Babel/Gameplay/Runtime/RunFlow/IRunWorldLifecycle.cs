using System;

namespace Babel.Gameplay.RunFlow
{
    /// <summary>
    /// Optional simulation-owned state that follows the same transaction boundary as the
    /// run command and event buffers. RunContext owns the attached instance.
    /// </summary>
    internal interface IRunWorldLifecycle : IDisposable
    {
        void Reset();
        void BeginTick();
        void EndTick();
        void AbortTick();
    }
}
