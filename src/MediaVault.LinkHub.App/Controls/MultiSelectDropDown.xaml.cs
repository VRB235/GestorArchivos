using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace MediaVault.LinkHub.App.Controls;

/// <summary>
/// Dropdown compacto de selección múltiple con búsqueda (apto para listas grandes).
/// Espera ítems con propiedades <c>Name</c> e <c>IsSelected</c>.
/// </summary>
public partial class MultiSelectDropDown : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(MultiSelectDropDown),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(
            nameof(Placeholder),
            typeof(string),
            typeof(MultiSelectDropDown),
            new PropertyMetadata("Seleccionar…", (_, _) => { /* summary via code */ }));

    public static readonly DependencyProperty SummaryProperty =
        DependencyProperty.Register(
            nameof(Summary),
            typeof(string),
            typeof(MultiSelectDropDown),
            new PropertyMetadata("Ninguno"));

    public MultiSelectDropDown()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshSummary();
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string Summary
    {
        get => (string)GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MultiSelectDropDown control)
            control.RefreshSummary();
    }

    private void DropToggle_OnChecked(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        ApplySearchFilter();
        SearchBox.Focus();
        RefreshSummary();
    }

    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e) =>
        ApplySearchFilter();

    private void ApplySearchFilter()
    {
        var query = SearchBox.Text?.Trim() ?? string.Empty;
        OptionsList.Items.Filter = string.IsNullOrWhiteSpace(query)
            ? null
            : item =>
            {
                var name = GetItemName(item);
                return name.Contains(query, StringComparison.OrdinalIgnoreCase);
            };
    }

    private void OptionCheckBox_OnChanged(object sender, RoutedEventArgs e) =>
        RefreshSummary();

    private void SelectAll_OnClick(object sender, RoutedEventArgs e)
    {
        SetAllVisible(selected: true);
        RefreshSummary();
    }

    private void ClearAll_OnClick(object sender, RoutedEventArgs e)
    {
        SetAllVisible(selected: false);
        RefreshSummary();
    }

    private void SetAllVisible(bool selected)
    {
        foreach (var item in OptionsList.Items)
        {
            if (OptionsList.Items.Filter is not null && !OptionsList.Items.Filter(item))
                continue;

            SetItemSelected(item, selected);
        }
    }

    private void RefreshSummary()
    {
        if (ItemsSource is null)
        {
            Summary = Placeholder;
            return;
        }

        var selectedNames = new List<string>();
        var total = 0;
        foreach (var item in ItemsSource)
        {
            total++;
            if (GetItemSelected(item))
                selectedNames.Add(GetItemName(item));
        }

        Summary = selectedNames.Count switch
        {
            0 => Placeholder,
            1 => selectedNames[0],
            _ when selectedNames.Count == total => $"Todas ({total})",
            _ => $"{selectedNames.Count} seleccionadas"
        };
    }

    private void Root_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Mantener popup abierto al interactuar dentro.
    }

    private static string GetItemName(object? item)
    {
        if (item is null)
            return string.Empty;

        var prop = item.GetType().GetProperty("Name");
        return prop?.GetValue(item)?.ToString() ?? item.ToString() ?? string.Empty;
    }

    private static bool GetItemSelected(object? item)
    {
        if (item is null)
            return false;

        var prop = item.GetType().GetProperty("IsSelected");
        return prop?.GetValue(item) is true;
    }

    private static void SetItemSelected(object? item, bool selected)
    {
        if (item is null)
            return;

        var prop = item.GetType().GetProperty("IsSelected");
        if (prop is null || !prop.CanWrite)
            return;

        prop.SetValue(item, selected);
    }
}
