using System;
using System.Windows.Input;

namespace WPFCAD.Helper
{
  public abstract class CommandBase : ICommand
  {
    public event EventHandler CanExecuteChanged
    {
      add { CommandManager.RequerySuggested += value; }
      remove { CommandManager.RequerySuggested -= value; }
    }

    public void Execute(object parameter)
    {
      if (CanExecute(parameter))
        OnExecute(parameter);
    }
    public virtual bool CanExecute(object parameter)
    {
      return true;
    }

    protected abstract void OnExecute(object parameter);

    protected void RaiseCanExecuteChanged()
    {
      CommandManager.InvalidateRequerySuggested();
    }
  }
  public class RelayCommand<T> : CommandBase
  {
    public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
    {
      if (execute == null)
        throw new ArgumentNullException(nameof(execute));
      if (canExecute == null)
        canExecute = _ => true;

      _execute = execute;
      _canExecute = canExecute;
    }

    protected override void OnExecute(object parameter)
    {
      _execute((T)parameter);
    }
    public override bool CanExecute(object parameter)
    {
      return _canExecute != null ? _canExecute((T)parameter) : true;
    }

    private readonly Action<T> _execute;
    private readonly Func<T, bool> _canExecute;
  }
  public class RelayCommand : RelayCommand<object>
  {
    public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null) : base(execute, canExecute) { }
  }
}
