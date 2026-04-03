using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppAmbitAvalonia;
using AppAmbitTestingAppAvalonia.Models;
using AppAmbitTestingAppAvalonia.Utils;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace AppAmbitTestingAppAvalonia.Views;

public partial class CmsView : UserControl
{
    private const string Collection = "tech_inventory";

    private readonly List<(string Label, Func<AppAmbit.ICmsQueryBuilder<CmsExampleModel>> Build)> _filters;

    public CmsView()
    {
        InitializeComponent();

        _filters = new()
        {
            ("Equals: item_sku = TEC-02",
                () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).Equals("item_sku", "TEC-02")),
            ("Not Equals: item_sku ≠ TEC-02",
                () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).NotEquals("item_sku", "TEC-02")),
            ("Boolean: in_stock = true",
                () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).Equals("in_stock", "true")),
            ("Contains: product_name contains 'Pro'",
                () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).Contains("product_name", "Pro")),
            ("Starts With: item_sku starts with 'TEC'",
                () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).StartsWith("item_sku", "TEC")),
            ("In List: [TEC-01, TEC-02]",
                () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).InList("item_sku", new[] { "TEC-01", "TEC-02" })),
            ("Not In List: [TEC-01, TEC-02]",
                () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).NotInList("item_sku", new[] { "TEC-01", "TEC-02" })),
            ("Greater Than: price > 500",
                () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).GreaterThan("price", 500)),
            ("Greater Or Equal: price >= 222",
                () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).GreaterThanOrEqual("price", 222)),
            ("Less Than: price < 500",
                () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).LessThan("price", 500)),
            ("Order By price ASC",
                () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).OrderByAscending("price")),
            ("Order By price DESC",
                () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).OrderByDescending("price")),
            ("Pagination: Page 1 — 1 per page",
                () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).GetPage(1).GetPerPage(1)),
            ("Pagination: Page 2 — 1 per page",
                () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).GetPage(2).GetPerPage(1)),
        };

        FilterPicker.ItemsSource = _filters.Select(f => f.Label).ToList();
    }

    // ── Event handlers ──────────────────────────────────────────────────────────

    private async void OnFetchAllClicked(object? sender, RoutedEventArgs e)
    {
        await LoadResults(AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection));
    }

    private async void OnApplyFilterClicked(object? sender, RoutedEventArgs e)
    {
        if (FilterPicker.SelectedIndex < 0)
        {
            await AlertWindow.ShowAlert("Please select a filter first.");
            return;
        }
        await LoadResults(_filters[FilterPicker.SelectedIndex].Build());
    }

    private async void OnSearchClicked(object? sender, RoutedEventArgs e)
    {
        var term = SearchBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            await LoadResults(AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).Search(term));
        }
        else
        {
            await LoadResults(AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection));
        }
    }

    // ── Core logic ───────────────────────────────────────────────────────────────

    private async Task LoadResults(AppAmbit.ICmsQueryBuilder<CmsExampleModel> query)
    {
        SetLoading(true);

        try
        {
            var items = await query.GetListAsync();
            await ImageUtils.LoadAsync(items);

            Dispatcher.UIThread.Post(() =>
            {
                ResultsList.ItemsSource = items;
                EmptyLabel.IsVisible = items.Count == 0;
            });
        }
        catch (Exception ex)
        {
            await AlertWindow.ShowAlert($"Error loading CMS data: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool loading)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LoadingBar.IsVisible = loading;
            BtnFetchAll.IsEnabled = !loading;
            BtnApplyFilter.IsEnabled = !loading;
            BtnSearch.IsEnabled = !loading;
        });
    }
}

// ── Value converters ─────────────────────────────────────────────────────────────

/// <summary>Converts bool InStock → badge background color.</summary>
public sealed class BooleanToStockColorConverter : Avalonia.Data.Converters.IValueConverter
{
    public static readonly BooleanToStockColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is true
            ? new SolidColorBrush(Color.Parse("#16A34A"))  // green-600
            : new SolidColorBrush(Color.Parse("#DC2626")); // red-600

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Converts bool InStock → "In Stock" / "Out of Stock" label.</summary>
public sealed class BooleanToStockLabelConverter : Avalonia.Data.Converters.IValueConverter
{
    public static readonly BooleanToStockLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is true ? "In Stock" : "Out of Stock";

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}
