using System;
using System.Windows.Input;

namespace ArcadeStick.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        // =========================================================================
        // 🏁 START: LIFECYCLE AND DELEGATE REGISTRATION CONSTRUCTOR
        // =========================================================================
        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        // =========================================================================
        // 🛑 END: LIFECYCLE AND DELEGATE REGISTRATION CONSTRUCTOR
        // =========================================================================

        // =========================================================================
        // 🏁 START: ICOMMAND INTERFACE DECLARATION METHODS
        // =========================================================================
        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);

        public void Execute(object? parameter) => _execute(parameter);
        // =========================================================================
        // 🛑 END: ICOMMAND INTERFACE DECLARATION METHODS
        // =========================================================================

        // =========================================================================
        // 🏁 START: COMMAND MANAGER REQUERY EVENT HOOKS
        // =========================================================================
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
        // =========================================================================
        // 🛑 END: COMMAND MANAGER REQUERY EVENT HOOKS
        // =========================================================================
    }
}