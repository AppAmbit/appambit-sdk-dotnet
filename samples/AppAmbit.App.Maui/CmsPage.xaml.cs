using AppAmbit;
using AppAmbitTestingApp.Models;


namespace AppAmbitTestingApp;

public partial class CmsPage : ContentPage
{
    private const string Collection = "tech_inventory";

    // Filter definitions: label shown in Picker → factory that builds the query
    private readonly List<(string Label, Func<CmsQueryBuilder<CmsExampleModel>> Build)> _filters;

    public CmsPage()
    {
        InitializeComponent();

        _filters = new()
        {
            // Equality
            ("Equals: item_sku = TEC-02",
                () => Cms.For<CmsExampleModel>(Collection).Equals("item_sku", "TEC-02")),
            ("Not Equals: item_sku ≠ TEC-02",
                () => Cms.For<CmsExampleModel>(Collection).NotEquals("item_sku", "TEC-02")),

            // Text matching
            ("Contains: product_name contains 'Pro'",
                () => Cms.For<CmsExampleModel>(Collection).Contains("product_name", "Pro")),
            ("Starts With: item_sku starts with 'TEC'",
                () => Cms.For<CmsExampleModel>(Collection).StartsWith("item_sku", "TEC")),

            // List membership
            ("In List: item_sku in [TEC-01, TEC-02]",
                () => Cms.For<CmsExampleModel>(Collection).InList("item_sku", new[] { "TEC-01", "TEC-02" })),
            ("Not In List: item_sku not in [TEC-01, TEC-02]",
                () => Cms.For<CmsExampleModel>(Collection).NotInList("item_sku", new[] { "TEC-01", "TEC-02" })),

            // Numeric comparisons
            ("Greater Than: price > 500",
                () => Cms.For<CmsExampleModel>(Collection).GreaterThan("price", 500)),
            ("Greater Or Equal: price >= 500",
                () => Cms.For<CmsExampleModel>(Collection).GreaterThanOrEqual("price", 500)),
            ("Less Than: price < 500",
                () => Cms.For<CmsExampleModel>(Collection).LessThan("price", 500)),
            ("Less Or Equal: price <= 500",
                () => Cms.For<CmsExampleModel>(Collection).LessThanOrEqual("price", 500)),

            // Sorting
            ("Order By price ASC",
                () => Cms.For<CmsExampleModel>(Collection).OrderByAscending("price")),
            ("Order By price DESC",
                () => Cms.For<CmsExampleModel>(Collection).OrderByDescending("price")),

            // Pagination
            ("Pagination: Page 1, 2 per page",
                () => Cms.For<CmsExampleModel>(Collection).SetPage(1).SetPerPage(2)),
            ("Pagination: Page 2, 2 per page",
                () => Cms.For<CmsExampleModel>(Collection).SetPage(2).SetPerPage(2)),
        };

        FilterPicker.ItemsSource = _filters.Select(f => f.Label).ToList();
    }

    // Get all without filters
    private async void OnFetchListClicked(object sender, EventArgs e)
    {
        //await LoadResults(Cms.For<CmsExampleModel>("sistema_de_gestion_de_propiedades_de_una_marinaclub_nautico"));
        Cms.Clear("sistema_de_gestion_de_propiedades_de_una_marinaclub_nautico");
        await LoadResults(Cms.For<CmsExampleModel>(Collection));
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
            await LoadResults(Cms.For<CmsExampleModel>(Collection).Search(term));
    }

    private async Task LoadResults(CmsQueryBuilder<CmsExampleModel> query)
    {
        try
        {
            var items = await query.GetListAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                ResultsList.ItemsSource = null;
                ResultsList.ItemsSource = items;
            });

            if (items.Count == 0)
                await DisplayAlert("Info", "No entries found. Cache may be empty — try again in a few seconds.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
}
