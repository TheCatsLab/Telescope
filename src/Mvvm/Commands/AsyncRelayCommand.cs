using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Cats.Telescope.VsExtension.Mvvm.Commands;

public class AsyncRelayCommand : ICommand
{
    private bool _isExecuting;
    private Func<object, Task> _asyncExecute { get; }
    public Predicate<object> _canExecutePredicate { get; }

    public AsyncRelayCommand(Func<object, Task> execute)
        : this(execute, null)
    {
    }

    public AsyncRelayCommand(Func<object, Task> asyncExecute,
                   Predicate<object> canExecutePredicate)
    {
        _asyncExecute = asyncExecute;
        _canExecutePredicate = canExecutePredicate;
    }

    protected virtual async Task ExecuteAsync(object parameter)
    {
        await _asyncExecute(parameter);
    }


    public event EventHandler CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }

    public bool CanExecute(object parameter)
    {
        if (_canExecutePredicate == null)
        {
            return !_isExecuting;
        }

        return !_isExecuting && _canExecutePredicate(parameter);
    }

    public async void Execute(object parameter)
    {
        _isExecuting = true;

        // Not necessary if Execute is not called locally
        RaiseCanExecuteChanged(); 

        try
        {
            await ExecuteAsync(parameter);
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }
}
