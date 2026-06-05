using AppAmbit;
using AppAmbitTestingApp.Models;

namespace AppAmbitTestingApp;

public partial class CmsPage : ContentPage
{
    private const string Collection = "blog_extended";

    private readonly List<(string Label, Func<ICmsQueryBuilder<CmsExampleModel>> Build)> _filters;

    public CmsPage()
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

    private async void OnFetchListClicked(object sender, EventArgs e)
    {
        await LoadResults(Cms.Content<CmsExampleModel>(Collection));
    }

    private async void OnApplyFilterClicked(object sender, EventArgs e)
    {
        if (FilterPicker.SelectedIndex < 0)
        {
            await DisplayAlert("Info", "Please select a filter first.", "OK");
            return;
        }
        var query = _filters[FilterPicker.SelectedIndex].Build();
        await LoadResults(query);
    }

    private async void OnSearchClicked(object sender, EventArgs e)
    {
        var term = SearchEntry.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            await LoadResults(Cms.Content<CmsExampleModel>(Collection).Search(term));
    }

    private async Task LoadResults(ICmsQueryBuilder<CmsExampleModel> query)
    {
        SetLoading(true);
        try
        {
            var items = await query.GetListAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                ResultsList.ItemsSource = null;
                ResultsList.ItemsSource = items;
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool isLoading)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingBar.IsVisible = isLoading;
        });
    }
}
