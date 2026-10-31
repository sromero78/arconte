using System.Windows.Input;

namespace RenombradorDocumental.App.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Func<object?, Task>? _asyncExec;
    private readonly Action<object?>? _exec;
    private readonly Func<object?, bool>? _can;
    private bool _ocupado;

    public RelayCommand(Action<object?> exec, Func<object?, bool>? can = null)
    { _exec = exec; _can = can; }

    public RelayCommand(Func<object?, Task> exec, Func<object?, bool>? can = null)
    { _asyncExec = exec; _can = can; }

    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    public bool CanExecute(object? parameter) => !_ocupado && (_can?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        try
        {
            _ocupado = true;
            RaiseCanExecuteChanged();
            if (_asyncExec != null) await _asyncExec(parameter);
            else _exec?.Invoke(parameter);
        }
        finally
        {
            _ocupado = false;
            RaiseCanExecuteChanged();
        }
    }
}
