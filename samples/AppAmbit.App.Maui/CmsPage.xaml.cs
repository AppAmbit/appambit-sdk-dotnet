using AppAmbit;
using AppAmbitTestingApp.Models;


namespace AppAmbitTestingApp;

public partial class CmsPage : ContentPage
{
    private const string Collection = "tech_inventory";

    // Filter definitions: label shown in Picker → factory that builds the query
    private readonly List<(string Label, Func<ICmsQueryBuilder<CmsExampleModel>> Build)> _filters;

    public CmsPage()
    {
        InitializeComponent();

        _filters = new()
        {
            // Equality
            ("Equals: item_sku = TEC-02",
                () => Cms.Content<CmsExampleModel>(Collection).Equals("item_sku", "TEC-02")),
            ("Not Equals: item_sku ≠ TEC-02",
                () => Cms.Content<CmsExampleModel>(Collection).NotEquals("item_sku", "TEC-02")),
            ("In List: category = Cat 1",
                () => Cms.Content<CmsExampleModel>(Collection).InList("category", new[] { "Cat 1" })),
            ("Boolean: in_stock = true",
                () => Cms.Content<CmsExampleModel>(Collection).Equals("in_stock", "true")),

            // Text matching
            ("Contains: product_name contains 'Pro'",
                () => Cms.Content<CmsExampleModel>(Collection).Contains("product_name", "Pro")),
            ("Starts With: item_sku starts with 'TEC'",
                () => Cms.Content<CmsExampleModel>(Collection).StartsWith("item_sku", "TEC")),

            // List membership
            ("In List: item_sku in [TEC-01, TEC-02]",
                () => Cms.Content<CmsExampleModel>(Collection).InList("item_sku", new[] { "TEC-01", "TEC-02" })),
            ("Not In List: item_sku not in [TEC-01, TEC-02]",
                () => Cms.Content<CmsExampleModel>(Collection).NotInList("item_sku", new[] { "TEC-01", "TEC-02" })),

            // Numeric comparisons
            ("Greater Than: price > 500",
                () => Cms.Content<CmsExampleModel>(Collection).GreaterThan("price", 500)),
            ("Greater Or Equal: price >= 500",
                () => Cms.Content<CmsExampleModel>(Collection).GreaterThanOrEqual("price", 500)),
            ("Less Than: price < 500",
                () => Cms.Content<CmsExampleModel>(Collection).LessThan("price", 500)),
            ("Less Or Equal: price <= 500",
                () => Cms.Content<CmsExampleModel>(Collection).LessThanOrEqual("price", 500)),

            // Sorting
            ("Order By product_name ASC",
                () => Cms.Content<CmsExampleModel>(Collection).OrderByAscending("product_name")),
            ("Order By product_name DESC",
                () => Cms.Content<CmsExampleModel>(Collection).OrderByDescending("product_name")),
            ("Order By price ASC",
                () => Cms.Content<CmsExampleModel>(Collection).OrderByAscending("price")),
            ("Order By price DESC",
                () => Cms.Content<CmsExampleModel>(Collection).OrderByDescending("price")),

            // Pagination
            ("Pagination: Page 1, 2 per page",
                () => Cms.Content<CmsExampleModel>(Collection).GetPage(1).GetPerPage(2)),
            ("Pagination: Page 2, 2 per page",
                () => Cms.Content<CmsExampleModel>(Collection).GetPage(2).GetPerPage(2)),
        };

        FilterPicker.ItemsSource = _filters.Select(f => f.Label).ToList();
    }

    private async void OnFetchListClicked(object sender, EventArgs e)
    {
        await LoadResults(Cms.Content<CmsExampleModel>(Collection));
    }

    // Apply selected filter from Picker
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

    // Full-text search
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
