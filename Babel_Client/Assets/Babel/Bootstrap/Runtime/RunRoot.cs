using System;
using Babel.Gameplay.RunFlow;
using Babel.Unity.Infrastructure.Time;
using UnityEngine;

namespace Babel.Bootstrap
{
    [DefaultExecutionOrder(-800)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RunDriver))]
    public sealed class RunRoot : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float _durationSeconds = 900f;
        [SerializeField] private int _seed = 1;
        [SerializeField] private RunDriver _driver;

        private GameCompositionRoot _composition;

        public RunContext Context => _composition == null ? null : _composition.Context;
        public RunDriver Driver => _driver;
        public int Seed => _seed;

        private void Awake()
        {
            if (_driver == null) _driver = GetComponent<RunDriver>();

            var settings = new RunSettings(_durationSeconds);
            IUpgradeSelectionHandler upgradeHandler = FindUpgradeSelectionHandler();
            _composition = new GameCompositionRoot(settings, _seed, null, upgradeHandler);
            _driver.Initialize(_composition.Loop, _composition.Context, _composition.PresentationTime);
            _driver.Enqueue(RunControlCommand.StartRun());
        }

        private void OnDestroy()
        {
            if (_driver != null) _driver.Detach();
            if (_composition == null) return;

            try { _composition.Dispose(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
            _composition = null;
        }

        private IUpgradeSelectionHandler FindUpgradeSelectionHandler()
        {
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IUpgradeSelectionHandler handler) return handler;
            return null;
        }

        private void OnValidate()
        {
            if (_durationSeconds < 1f) _durationSeconds = 1f;
            if (_driver == null) _driver = GetComponent<RunDriver>();
        }
    }
}
