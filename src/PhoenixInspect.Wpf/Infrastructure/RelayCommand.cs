using System.Windows.Input;

namespace PhoenixInspect.Wpf;

/// <summary>Routes a parameterless UI gesture to a delegate with an explicit enabled predicate.</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action execute;
    private readonly Func<bool>? canExecute;

    /// <summary>Creates a command over the supplied delegates.</summary>
    /// <param name="execute">The action invoked when the gesture is accepted.</param>
    /// <param name="canExecute">An optional predicate; a null predicate means always enabled.</param>
    /// <exception cref="ArgumentNullException"><paramref name="execute"/> is null.</exception>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.canExecute = canExecute;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => canExecute is null || canExecute();

    /// <inheritdoc />
    public void Execute(object? parameter) => execute();

    /// <summary>Re-evaluates <see cref="CanExecute"/> for every bound control.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
