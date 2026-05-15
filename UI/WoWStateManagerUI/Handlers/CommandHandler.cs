using System;
using System.Windows.Input;

namespace WoWStateManagerUI.Handlers
{
    /// <summary>
    /// Synchronous ICommand. Supports both a constant <c>canExecute</c> flag
    /// (the original shape) and a <c>Func&lt;bool&gt;</c> predicate so
    /// view models can drive CanExecute off mutable state.
    /// </summary>
    internal class CommandHandler : ICommand
    {
        private readonly Action _action;
        private readonly Func<bool>? _canExecuteFunc;
        private readonly bool _canExecuteConst;

        public CommandHandler(Action action, bool canExecute)
        {
            _action = action;
            _canExecuteConst = canExecute;
        }

        public CommandHandler(Action action, Func<bool> canExecute)
        {
            _action = action;
            _canExecuteFunc = canExecute;
            _canExecuteConst = true;
        }

        public void Execute(object? parameter) => _action();

        public bool CanExecute(object? parameter)
            => _canExecuteFunc?.Invoke() ?? _canExecuteConst;

        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
