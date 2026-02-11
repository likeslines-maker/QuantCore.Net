using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PortfolioStressLab.Wpf.Infrastructure
{
    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _run;
        private readonly Func<bool>? _can;
        private bool _running;

        public AsyncRelayCommand(Func<Task> run, Func<bool>? can = null)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _can = can;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => !_running && (_can?.Invoke() ?? true);

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter)) return;
            try
            {
                _running = true;
                RaiseCanExecuteChanged();
                await _run().ConfigureAwait(true);
            }
            finally
            {
                _running = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
