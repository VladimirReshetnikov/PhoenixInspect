using System.Windows.Input;

namespace PhoenixInspect.Desktop;

/// <summary>A minimal delegating <see cref="ICommand"/> with explicit requery notification.</summary>
/// <remarks>
/// Avalonia has no WPF-style <c>CommandManager</c>, so consumers raise <see cref="RaiseCanExecuteChanged"/>
/// exactly when a dependency of <see cref="CanExecute"/> changes. That explicitness keeps command state
/// deterministic instead of relying on ambient focus heuristics.
/// </remarks>
public sealed class RelayCommand : ICommand
{
    private readonly Action execute;
    private readonly Func<bool>? canExecute;

    /// <summary>Creates a command.</summary>
    /// <param name="execute">The action invoked by <see cref="Execute"/>.</param>
    /// <param name="canExecute">The optional gate consulted by <see cref="CanExecute"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="execute"/> is null.</exception>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.canExecute = canExecute;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            execute();
        }
    }

    /// <summary>Notifies bound controls that <see cref="CanExecute"/> may now answer differently.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
