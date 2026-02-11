using System;
using System.Windows.Input;

namespace PortfolioStressLab.Wpf.Infrastructure
{
    public sealed class RelayCommand : ICommand
    {
        private readonly Action _run;
        private readonly Func<bool>? _can;

        public RelayCommand(Action run, Func<bool>? can = null)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _can = can;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _can?.Invoke() ?? true;

        public void Execute(object? parameter) => _run();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
