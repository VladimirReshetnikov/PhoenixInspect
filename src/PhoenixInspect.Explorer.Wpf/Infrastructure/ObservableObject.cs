using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PhoenixInspect.Explorer.Wpf.Infrastructure;

/// <summary>Minimal change-notification base class shared by every demonstration view model.</summary>
/// <remarks>
/// The demo host deliberately avoids an MVVM framework dependency so the only non-test consumer of the product
/// libraries stays small and auditable.
/// </remarks>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raises <see cref="PropertyChanged"/> for the calling property.</summary>
    /// <param name="propertyName">The property name supplied automatically by the compiler.</param>
    protected void Raise([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>Assigns a backing field and raises a change notification only when the value actually changed.</summary>
    /// <typeparam name="T">The stored property type.</typeparam>
    /// <param name="field">The backing field to update.</param>
    /// <param name="value">The candidate value.</param>
    /// <param name="propertyName">The property name supplied automatically by the compiler.</param>
    /// <returns><see langword="true"/> when the field was updated; otherwise, <see langword="false"/>.</returns>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(propertyName);
        return true;
    }
}
