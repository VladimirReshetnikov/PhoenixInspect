using System.Collections;
using System.ComponentModel;
using System.Windows.Data;
using PhoenixInspect.Explorer.Wpf.Models;

namespace PhoenixInspect.Explorer.Wpf.ViewModels;

/// <summary>Creates the grouped collection views that property lists bind to.</summary>
/// <remarks>
/// A <see cref="CollectionViewSource"/> declared in XAML resources cannot inherit the panel's data context, so the
/// grouped views are created here and exposed as ordinary view-model properties instead.
/// </remarks>
public static class PropertyGrouping
{
    /// <summary>Wraps a property-row collection in a view grouped by <see cref="PropertyRow.Group"/>.</summary>
    /// <param name="source">The live collection to wrap; group changes follow its notifications.</param>
    /// <returns>A grouped view that preserves insertion order inside each group.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static ICollectionView Create(IEnumerable source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var view = new CollectionViewSource { Source = source };
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PropertyRow.Group)));
        return view.View;
    }
}
