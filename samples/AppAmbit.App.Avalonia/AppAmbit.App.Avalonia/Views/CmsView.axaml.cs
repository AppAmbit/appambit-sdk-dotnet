using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AppAmbitAvalonia;
using AppAmbitTestingAppAvalonia.Models;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
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
            ("Title = T20",                  () => Cms.Content<CmsExampleModel>(Collection).Equals("title", "T20")),
            ("Title ≠ T20",                  () => Cms.Content<CmsExampleModel>(Collection).NotEquals("title", "T20")),
            ("Is Published = true",          () => Cms.Content<CmsExampleModel>(Collection).Equals("is_published", "true")),
            ("Is Published = false",         () => Cms.Content<CmsExampleModel>(Collection).Equals("is_published", "false")),
            ("Title contains 't1'",          () => Cms.Content<CmsExampleModel>(Collection).Contains("title", "t1")),
            ("Title starts with 't'",        () => Cms.Content<CmsExampleModel>(Collection).StartsWith("title", "t")),
            ("Category IN [science, tech]",  () => Cms.Content<CmsExampleModel>(Collection).InList("category", new[] { "science", "tech" })),
            ("Category NOT IN [tech, news]", () => Cms.Content<CmsExampleModel>(Collection).NotInList("category", new[] { "tech", "news" })),
            ("Views > 1000",                 () => Cms.Content<CmsExampleModel>(Collection).GreaterThan("views_count", 1000)),
            ("Views ≥ 555",                  () => Cms.Content<CmsExampleModel>(Collection).GreaterThanOrEqual("views_count", 555)),
            ("Views < 15000",                () => Cms.Content<CmsExampleModel>(Collection).LessThan("views_count", 15000)),
            ("Views ≤ 15000",                () => Cms.Content<CmsExampleModel>(Collection).LessThanOrEqual("views_count", 15000)),
            ("Sort Title ↑",                 () => Cms.Content<CmsExampleModel>(Collection).OrderByAscending("title")),
            ("Sort Title ↓",                 () => Cms.Content<CmsExampleModel>(Collection).OrderByDescending("title")),
            ("Sort Views ↑",                 () => Cms.Content<CmsExampleModel>(Collection).OrderByAscending("views_count")),
            ("Sort Views ↓",                 () => Cms.Content<CmsExampleModel>(Collection).OrderByDescending("views_count")),
            ("Page 1 (2 per page)",          () => Cms.Content<CmsExampleModel>(Collection).GetPage(1).GetPerPage(2)),
            ("Page 2 (2 per page)",          () => Cms.Content<CmsExampleModel>(Collection).GetPage(2).GetPerPage(2)),
        };

        FilterPicker.ItemsSource = _filters.Select(f => f.Label).ToList();
    }

    private async void OnFetchListClicked(object? sender, RoutedEventArgs e)
    {
        await LoadResults(Cms.Content<CmsExampleModel>(Collection));
    }

    private async void OnApplyFilterClicked(object? sender, RoutedEventArgs e)
    {
        if (FilterPicker.SelectedIndex < 0)
        {
            await AlertWindow.ShowAlert("Please select a filter first.");
            return;
        }
        var query = _filters[FilterPicker.SelectedIndex].Build();
        await LoadResults(query);
    }

    private async void OnSearchClicked(object? sender, RoutedEventArgs e)
    {
        var term = SearchEntry.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            await LoadResults(Cms.Content<CmsExampleModel>(Collection).Search(term));
    }

    private async Task LoadResults(AppAmbit.ICmsQueryBuilder<CmsExampleModel> query)
    {
        SetLoading(true);
        try
        {
            var items = await query.GetListAsync();

            Dispatcher.UIThread.Post(() =>
            {
                ResultsList.ItemsSource = null;
                ResultsList.ItemsSource = items;
            });
        }
        catch (Exception ex)
        {
            await AlertWindow.ShowAlert($"Error: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool isLoading)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LoadingBar.IsVisible = isLoading;
        });
    }
}

public sealed class ListToStringConverter : IValueConverter
{
    public static readonly ListToStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is IEnumerable<string> list ? string.Join(", ", list) : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class UrlToBitmapConverter : IValueConverter
{
    private static readonly HttpClient _http = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Bitmap?> _cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string url || string.IsNullOrWhiteSpace(url))
            return null;

        if (_cache.TryGetValue(url, out var cached))
            return cached;

        _ = LoadAndCacheAsync(url);
        return null;
    }

    private static async Task LoadAndCacheAsync(string url)
    {
        try
        {
            var bytes = await _http.GetByteArrayAsync(url);
            using var ms = new MemoryStream(bytes);
            _cache[url] = new Bitmap(ms);
        }
        catch
        {
            _cache[url] = null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
