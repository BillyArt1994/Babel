using System;
using System.Collections.Generic;
using Babel.Foundation;

namespace Babel.Gameplay.RunFlow
{
    public enum RunControlCommandKind
    {
        StartRun = 0,
        Pause = 1,
        Resume = 2,
        TogglePause = 3,
        SetSpeed = 4,
        BeginUpgradeChoice = 5,
        SelectUpgrade = 6,
        RequestRestart = 7,
        RequestReturnToMenu = 8,
        ResolveOutcome = 9
    }

    public readonly struct RunControlCommand
    {
        private RunControlCommand(RunControlCommandKind kind, int intValue)
        {
            Kind = kind;
            IntValue = intValue;
        }

        public RunControlCommandKind Kind { get; }
        public int IntValue { get; }

        public static RunControlCommand StartRun() => new RunControlCommand(RunControlCommandKind.StartRun, 0);
        public static RunControlCommand Pause() => new RunControlCommand(RunControlCommandKind.Pause, 0);
        public static RunControlCommand Resume() => new RunControlCommand(RunControlCommandKind.Resume, 0);
        public static RunControlCommand TogglePause() => new RunControlCommand(RunControlCommandKind.TogglePause, 0);

        public static RunControlCommand SetSpeed(RunSpeed speed)
        {
            RunClock.ValidateSpeed(speed);
            return new RunControlCommand(RunControlCommandKind.SetSpeed, (int)speed);
        }

        public static RunControlCommand BeginUpgradeChoice() => new RunControlCommand(RunControlCommandKind.BeginUpgradeChoice, 0);

        public static RunControlCommand SelectUpgrade(int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            return new RunControlCommand(RunControlCommandKind.SelectUpgrade, index);
        }

        public static RunControlCommand RequestRestart() => new RunControlCommand(RunControlCommandKind.RequestRestart, 0);
        public static RunControlCommand RequestReturnToMenu() => new RunControlCommand(RunControlCommandKind.RequestReturnToMenu, 0);

        public static RunControlCommand ResolveOutcome(RunOutcome outcome)
        {
            if (outcome != RunOutcome.Victory && outcome != RunOutcome.Defeat)
                throw new ArgumentOutOfRangeException(nameof(outcome));
            return new RunControlCommand(RunControlCommandKind.ResolveOutcome, (int)outcome);
        }
    }

    internal sealed class RunControlCommandQueue
    {
        private readonly Queue<RunControlCommand> _commands;

        public RunControlCommandQueue(int initialCapacity = 8)
        {
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            _commands = new Queue<RunControlCommand>(initialCapacity);
        }

        public int Count => _commands.Count;
        public void Enqueue(RunControlCommand command) => _commands.Enqueue(command);

        public bool TryDequeue(out RunControlCommand command)
        {
            if (_commands.Count == 0)
            {
                command = default;
                return false;
            }

            command = _commands.Dequeue();
            return true;
        }

        public void Clear() => _commands.Clear();
    }

    public enum GameplayCommandKind
    {
        PointerDown = 0,
        PointerHold = 1,
        PointerUp = 2,
        PointerCancel = 3,
        CastAbility = 4
    }

    public readonly struct GameplayCommand
    {
        private GameplayCommand(
            GameplayCommandKind kind,
            Float2 screenPosition,
            Float2 worldPosition,
            float holdDuration,
            float chargeRatio,
            int intValue)
        {
            Kind = kind;
            ScreenPosition = screenPosition;
            WorldPosition = worldPosition;
            HoldDuration = holdDuration;
            ChargeRatio = chargeRatio;
            IntValue = intValue;
        }

        public GameplayCommandKind Kind { get; }
        public Float2 ScreenPosition { get; }
        public Float2 WorldPosition { get; }
        public float HoldDuration { get; }
        public float ChargeRatio { get; }
        public int IntValue { get; }

        public static GameplayCommand Pointer(
            GameplayCommandKind kind,
            Float2 screenPosition,
            Float2 worldPosition,
            float holdDuration,
            float chargeRatio)
        {
            if (kind != GameplayCommandKind.PointerDown &&
                kind != GameplayCommandKind.PointerHold &&
                kind != GameplayCommandKind.PointerUp &&
                kind != GameplayCommandKind.PointerCancel)
                throw new ArgumentOutOfRangeException(nameof(kind));

            ValidateFinite(screenPosition, nameof(screenPosition));
            ValidateFinite(worldPosition, nameof(worldPosition));
            if (!IsFinite(holdDuration) || holdDuration < 0f) throw new ArgumentOutOfRangeException(nameof(holdDuration));
            if (!IsFinite(chargeRatio) || chargeRatio < 0f || chargeRatio > 1f) throw new ArgumentOutOfRangeException(nameof(chargeRatio));
            return new GameplayCommand(kind, screenPosition, worldPosition, holdDuration, chargeRatio, 0);
        }

        public static GameplayCommand CastAbility(int abilityId, Float2 worldPosition)
        {
            if (abilityId < 0) throw new ArgumentOutOfRangeException(nameof(abilityId));
            ValidateFinite(worldPosition, nameof(worldPosition));
            return new GameplayCommand(GameplayCommandKind.CastAbility, Float2.Zero, worldPosition, 0f, 0f, abilityId);
        }

        private static void ValidateFinite(Float2 value, string parameterName)
        {
            if (!IsFinite(value.X) || !IsFinite(value.Y)) throw new ArgumentOutOfRangeException(parameterName);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public interface IUpgradeSelectionHandler
    {
        bool TrySelectUpgrade(int optionIndex, RunContext context);
    }
}
