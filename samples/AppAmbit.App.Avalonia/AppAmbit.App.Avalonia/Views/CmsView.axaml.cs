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
    private const string Collection = "blog_extended";

    private readonly List<(string Label, Func<AppAmbit.ICmsQueryBuilder<CmsExampleModel>> Build)> _filters;

    public CmsView()
    {
        InitializeComponent();

        _filters = new()
        {
            ("Title = T20",                  () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).Equals("title", "T20")),
            ("Title ≠ T20",                  () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).NotEquals("title", "T20")),
            ("Is Published = true",          () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).Equals("is_published", "true")),
            ("Is Published = false",         () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).Equals("is_published", "false")),
            ("Title contains 't1'",          () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).Contains("title", "t1")),
            ("Title starts with 't'",        () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).StartsWith("title", "t")),
            ("Category IN [science, tech]",  () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).InList("category", new[] { "science", "tech" })),
            ("Category NOT IN [tech, news]", () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).NotInList("category", new[] { "tech", "news" })),
            ("Views > 1000",                 () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).GreaterThan("views_count", 1000)),
            ("Views ≥ 555",                  () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).GreaterThanOrEqual("views_count", 555)),
            ("Views < 15000",                () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).LessThan("views_count", 15000)),
            ("Views ≤ 15000",                () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).LessThanOrEqual("views_count", 15000)),
            ("Sort Title ↑",                 () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).OrderByAscending("title")),
            ("Sort Title ↓",                 () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).OrderByDescending("title")),
            ("Sort Views ↑",                 () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).OrderByAscending("views_count")),
            ("Sort Views ↓",                 () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).OrderByDescending("views_count")),
            ("Page 1 (2 per page)",          () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).GetPage(1).GetPerPage(2)),
            ("Page 2 (2 per page)",          () => AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).GetPage(2).GetPerPage(2)),
        };

        FilterPicker.ItemsSource = _filters.Select(f => f.Label).ToList();
    }

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
            await LoadResults(AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection).Search(term));
        else
            await LoadResults(AppAmbitAvalonia.Cms.Content<CmsExampleModel>(Collection));
    }

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

public sealed class IsPublishedColorConverter : Avalonia.Data.Converters.IValueConverter
{
    public static readonly IsPublishedColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is true
            ? new SolidColorBrush(Color.Parse("#16A34A"))
            : new SolidColorBrush(Color.Parse("#DC2626"));

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class IsPublishedLabelConverter : Avalonia.Data.Converters.IValueConverter
{
    public static readonly IsPublishedLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is true ? "✓ Published" : "✕ Draft";

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class ListToStringConverter : Avalonia.Data.Converters.IValueConverter
{
    public static readonly ListToStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is IEnumerable<string> list ? string.Join(", ", list) : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}
