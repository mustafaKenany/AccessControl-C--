using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Turns a ComboBox into a type-to-filter search box.
/// Usage in XAML (with the SearchableComboBox style which provides PART_EditableTextBox):
///   Style="{StaticResource SearchableComboBox}" h:ComboBoxSearch.SearchPath="Name,NameAr"
/// The user types and the dropdown filters to items whose any listed property CONTAINS the
/// text (case-insensitive). Picking an item (or closing) clears the filter so the full list
/// is available next time. Essential when a customer has hundreds/thousands of products.
/// </summary>
public static class ComboBoxSearch
{
    public static readonly DependencyProperty SearchPathProperty =
        DependencyProperty.RegisterAttached(
            "SearchPath", typeof(string), typeof(ComboBoxSearch),
            new PropertyMetadata(null, OnSearchPathChanged));

    public static string? GetSearchPath(DependencyObject d) => (string?)d.GetValue(SearchPathProperty);
    public static void SetSearchPath(DependencyObject d, string? value) => d.SetValue(SearchPathProperty, value);

    private static readonly DependencyProperty AttachedProperty =
        DependencyProperty.RegisterAttached(
            "__cbsAttached", typeof(bool), typeof(ComboBoxSearch), new PropertyMetadata(false));

    private static void OnSearchPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ComboBox combo) return;
        var path = e.NewValue as string;
        if (string.IsNullOrWhiteSpace(path)) return;

        combo.IsEditable = true;
        combo.IsTextSearchEnabled = false;
        combo.StaysOpenOnEdit = true;
        // First listed path is what the editable box shows for the selected item.
        var first = path.Split(',')[0].Trim();
        if (!string.IsNullOrEmpty(first)) TextSearch.SetTextPath(combo, first);

        combo.Loaded += (_, _) => Attach(combo, path);
    }

    private static void Attach(ComboBox combo, string path)
    {
        if ((bool)combo.GetValue(AttachedProperty)) return;
        combo.SetValue(AttachedProperty, true);

        if (combo.Template?.FindName("PART_EditableTextBox", combo) is not TextBox editBox)
            return;

        var paths = path.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        ICollectionView? View() =>
            combo.ItemsSource != null ? CollectionViewSource.GetDefaultView(combo.ItemsSource) : null;

        void ClearFilter()
        {
            var v = View();
            if (v != null && v.Filter != null) v.Filter = null;
        }

        editBox.TextChanged += (_, _) =>
        {
            var text = editBox.Text ?? "";

            // If the text equals the currently-selected item's display, this change came from
            // selecting an item (not the user typing) — don't filter or re-open the dropdown.
            var selText = combo.SelectedItem != null ? GetValue(combo.SelectedItem, paths[0]) : null;
            if (text == selText) { ClearFilter(); return; }

            var view = View();
            if (view == null) return;

            view.Filter = string.IsNullOrWhiteSpace(text)
                ? null
                : obj => paths.Any(p =>
                  {
                      var val = GetValue(obj, p);
                      return !string.IsNullOrEmpty(val)
                             && val.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
                  });

            if (!combo.IsDropDownOpen) combo.IsDropDownOpen = true;
        };

        // Clear the filter when the dropdown closes so reopening shows everything.
        combo.DropDownClosed += (_, _) => ClearFilter();
    }

    private static string? GetValue(object? obj, string propPath)
    {
        if (obj == null) return null;
        var prop = obj.GetType().GetProperty(propPath);
        return prop?.GetValue(obj)?.ToString();
    }
}
