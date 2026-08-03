using System;
using Babel.Gameplay.RunFlow;
using UnityEngine;

namespace Babel.Unity.Infrastructure.Time
{
    /// <summary>
    /// The only runtime writer of Unity Time.timeScale.
    /// Simulation time remains authoritative in RunClock; this value only drives legacy presentation.
    /// </summary>
    public sealed class PresentationTimeScaleAdapter : IDisposable
    {
        private static float _legacyResumeScale = 1f;
        private static bool _simulationPresentationRunning = true;
        private bool _isDisposed;

        public static bool IsSimulationPresentationRunning => _simulationPresentationRunning;

        public void Sync(RunPhase phase, RunSpeed speed)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(PresentationTimeScaleAdapter));
            float target = phase == RunPhase.Playing ? (int)speed : 0f;
            if (phase == RunPhase.Playing) _legacyResumeScale = target;
            _simulationPresentationRunning = phase == RunPhase.Playing;
            Write(target);
        }

        public static void ApplyLegacySpeed(float speed)
        {
            if (!Mathf.Approximately(speed, 1f) &&
                !Mathf.Approximately(speed, 2f) &&
                !Mathf.Approximately(speed, 4f))
                throw new ArgumentOutOfRangeException(nameof(speed));

            _legacyResumeScale = speed;
            _simulationPresentationRunning = true;
            Write(speed);
        }

        public static void PauseLegacy()
        {
            if (UnityEngine.Time.timeScale > 0f)
                _legacyResumeScale = UnityEngine.Time.timeScale;
            _simulationPresentationRunning = false;
            Write(0f);
        }

        public static void ResumeLegacy()
        {
            _simulationPresentationRunning = true;
            Write(_legacyResumeScale);
        }

        public static void FreezeLegacy()
        {
            _simulationPresentationRunning = false;
            Write(0f);
        }

        public static void ResetLegacy()
        {
            _legacyResumeScale = 1f;
            _simulationPresentationRunning = true;
            Write(1f);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _legacyResumeScale = 1f;
            _simulationPresentationRunning = true;
            Write(1f);
            _isDisposed = true;
        }

        private static void Write(float value)
        {
            if (!Mathf.Approximately(UnityEngine.Time.timeScale, value))
                UnityEngine.Time.timeScale = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _legacyResumeScale = 1f;
            _simulationPresentationRunning = true;
        }
    }
}
