using System;
using Babel.Gameplay.RunFlow;
using UnityEngine;

namespace Babel.Unity.Infrastructure.Time
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class RunDriver : MonoBehaviour
    {
        private RunLoop _loop;
        private RunContext _context;
        private PresentationTimeScaleAdapter _presentationTime;
        private int _lastDropWarningFrame = -600;

        public event Action<RunFrameResult> FrameAdvanced;

        public bool IsInitialized => _loop != null;
        public RunContext Context => _context;

        public void Initialize(RunLoop loop, RunContext context, PresentationTimeScaleAdapter presentationTime)
        {
            if (IsInitialized) throw new InvalidOperationException("RunDriver is already initialized.");
            _loop = loop ?? throw new ArgumentNullException(nameof(loop));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _presentationTime = presentationTime ?? throw new ArgumentNullException(nameof(presentationTime));
            enabled = true;
            _presentationTime.Sync(_context.Phase, _context.Clock.Speed);
        }

        public void Enqueue(RunControlCommand command)
        {
            if (!IsInitialized) throw new InvalidOperationException("RunDriver is not initialized.");
            _context.EnqueueControlCommand(command);
        }

        public bool Enqueue(GameplayCommand command)
        {
            if (!IsInitialized) throw new InvalidOperationException("RunDriver is not initialized.");
            return _context.EnqueueGameplayCommand(command);
        }

        private void LateUpdate()
        {
            if (!IsInitialized) return;

            RunFrameResult result = _loop.AdvanceFrame(UnityEngine.Time.unscaledDeltaTime);
            _presentationTime.Sync(_context.Phase, _context.Clock.Speed);

            if (result.DroppedTicks > 0 && UnityEngine.Time.frameCount - _lastDropWarningFrame >= 600)
            {
                _lastDropWarningFrame = UnityEngine.Time.frameCount;
                Debug.LogWarning("[Babel][RunDriver] Dropped simulation ticks to avoid a catch-up spiral: " + result.DroppedTicks);
            }

            FrameAdvanced?.Invoke(result);

            if (result.FaultedThisFrame)
            {
                RunFaultInfo fault = _context.Fault;
                Debug.LogException(fault == null ? new InvalidOperationException("Run faulted without diagnostics.") : fault.Exception, this);
            }
        }

        public void Detach()
        {
            if (!IsInitialized) return;
            FrameAdvanced = null;
            _presentationTime = null;
            _context = null;
            _loop = null;
            enabled = false;
        }

        private void OnDestroy() => Detach();
    }
}
