using System;

namespace Babel.Gameplay.RunFlow
{
    public sealed class RunController
    {
        private readonly RunContext _context;
        private readonly IUpgradeSelectionHandler _upgradeSelectionHandler;

        public RunController(RunContext context, IUpgradeSelectionHandler upgradeSelectionHandler = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _upgradeSelectionHandler = upgradeSelectionHandler;
        }

        public int ProcessControlCommands()
        {
            int availableAtFrameStart = _context.ControlCommands.Count;
            int processed = 0;
            for (int i = 0; i < availableAtFrameStart; i++)
            {
                if (!_context.ControlCommands.TryDequeue(out RunControlCommand command)) break;
                Process(command);
                processed++;
            }

            return processed;
        }

        private void Process(RunControlCommand command)
        {
            if (_context.Phase == RunPhase.Disposed || _context.Phase == RunPhase.Transitioning) return;

            if (_context.Phase == RunPhase.Faulted)
            {
                if (command.Kind == RunControlCommandKind.RequestRestart)
                    _context.TryRequestExit(RunExitRequest.Restart);
                else if (command.Kind == RunControlCommandKind.RequestReturnToMenu)
                    _context.TryRequestExit(RunExitRequest.ReturnToMenu);
                return;
            }

            switch (command.Kind)
            {
                case RunControlCommandKind.StartRun:
                    if (_context.Phase == RunPhase.Booting)
                    {
                        _context.ResetForStart();
                        _context.SetPhase(RunPhase.Playing);
                    }
                    break;

                case RunControlCommandKind.Pause:
                    if (_context.Phase == RunPhase.Playing) _context.SetPhase(RunPhase.Paused);
                    break;

                case RunControlCommandKind.Resume:
                    if (_context.Phase == RunPhase.Paused) _context.SetPhase(RunPhase.Playing);
                    break;

                case RunControlCommandKind.TogglePause:
                    if (_context.Phase == RunPhase.Playing) _context.SetPhase(RunPhase.Paused);
                    else if (_context.Phase == RunPhase.Paused) _context.SetPhase(RunPhase.Playing);
                    break;

                case RunControlCommandKind.SetSpeed:
                    if (_context.Phase == RunPhase.Playing ||
                        _context.Phase == RunPhase.Paused ||
                        _context.Phase == RunPhase.ChoosingUpgrade)
                        _context.SetSpeed((RunSpeed)command.IntValue);
                    break;

                case RunControlCommandKind.BeginUpgradeChoice:
                    if (_context.Phase == RunPhase.Playing && _upgradeSelectionHandler != null)
                    {
                        _context.SetPhase(RunPhase.ChoosingUpgrade);
                        _context.PresentationEvents.Add(new RunEvent(RunEventKind.UpgradeChoiceOpened, _context.Clock.Tick));
                    }
                    break;

                case RunControlCommandKind.SelectUpgrade:
                    HandleUpgradeSelection(command.IntValue);
                    break;

                case RunControlCommandKind.RequestRestart:
                    _context.TryRequestExit(RunExitRequest.Restart);
                    break;

                case RunControlCommandKind.RequestReturnToMenu:
                    _context.TryRequestExit(RunExitRequest.ReturnToMenu);
                    break;

                case RunControlCommandKind.ResolveOutcome:
                    _context.TryEndRun((RunOutcome)command.IntValue == RunOutcome.Victory ? RunPhase.Won : RunPhase.Lost);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void HandleUpgradeSelection(int optionIndex)
        {
            if (_context.Phase != RunPhase.ChoosingUpgrade || _upgradeSelectionHandler == null) return;
            if (!_upgradeSelectionHandler.TrySelectUpgrade(optionIndex, _context)) return;

            _context.PresentationEvents.Add(new RunEvent(RunEventKind.UpgradeSelected, _context.Clock.Tick, optionIndex));
            _context.SetPhase(RunPhase.Playing);
        }
    }
}
