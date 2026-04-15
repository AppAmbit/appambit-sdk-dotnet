using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppAmbit;
using AppAmbitTestingiOS.Models;
using AppAmbitTestingiOS.Utils;
using Foundation;
using UIKit;

namespace AppAmbitTestingiOS;

public class CmsViewController : UIViewController
{
    private UITableView _tableView = null!;
    private UISearchBar _searchBar = null!;
    private UIButton _btnFilter = null!;
    private UIButton _btnGetAll = null!;
    private UILabel _emptyLabel = null!;

    private CmsTableViewSource _source = null!;
    private const string CollectionName = "tech_inventory";

    private List<(string Label, Func<ICmsQueryBuilder<CmsExampleModel>> Build)> _cmsFilters = new();
    private int _selectedFilterIndex = -1;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Title = "CMS Showcase";
        View!.BackgroundColor = UIColor.SystemBackground;

        SetupFilters();
        SetupUI();
    }

    private void SetupFilters()
    {
        _cmsFilters = new()
        {
            // Equality
            ("Equals: item_sku = TEC-02", () => Cms.Content<CmsExampleModel>(CollectionName).Equals("item_sku", "TEC-02")),
            ("Not Equals: item_sku ≠ TEC-02", () => Cms.Content<CmsExampleModel>(CollectionName).NotEquals("item_sku", "TEC-02")),
            ("In List: category = Cat 1", () => Cms.Content<CmsExampleModel>(CollectionName).InList("category", new[] { "Cat 1" })),
            ("Boolean: in_stock = true", () => Cms.Content<CmsExampleModel>(CollectionName).Equals("in_stock", "true")),

            // Text matching
            ("Contains: product_name contains 'Pro'", () => Cms.Content<CmsExampleModel>(CollectionName).Contains("product_name", "Pro")),
            ("Starts With: item_sku starts with 'TEC'", () => Cms.Content<CmsExampleModel>(CollectionName).StartsWith("item_sku", "TEC")),

            // List membership
            ("In List: item_sku in [TEC-01, TEC-02]", () => Cms.Content<CmsExampleModel>(CollectionName).InList("item_sku", new[] { "TEC-01", "TEC-02" })),
            ("Not In List: item_sku not in [TEC-01, TEC-02]", () => Cms.Content<CmsExampleModel>(CollectionName).NotInList("item_sku", new[] { "TEC-01", "TEC-02" })),

            // Numeric comparisons
            ("Greater Than: price > 500", () => Cms.Content<CmsExampleModel>(CollectionName).GreaterThan("price", 500)),
            ("Greater Or Equal: price >= 500", () => Cms.Content<CmsExampleModel>(CollectionName).GreaterThanOrEqual("price", 500)),
            ("Less Than: price < 500", () => Cms.Content<CmsExampleModel>(CollectionName).LessThan("price", 500)),
            ("Less Or Equal: price <= 500", () => Cms.Content<CmsExampleModel>(CollectionName).LessThanOrEqual("price", 500)),

            // Sorting
            ("Order By product_name ASC", () => Cms.Content<CmsExampleModel>(CollectionName).OrderByAscending("product_name")),
            ("Order By product_name DESC", () => Cms.Content<CmsExampleModel>(CollectionName).OrderByDescending("product_name")),
            ("Order By price ASC", () => Cms.Content<CmsExampleModel>(CollectionName).OrderByAscending("price")),
            ("Order By price DESC", () => Cms.Content<CmsExampleModel>(CollectionName).OrderByDescending("price")),

            // Pagination
            ("Pagination: Page 1, 2 per page", () => Cms.Content<CmsExampleModel>(CollectionName).GetPage(1).GetPerPage(2)),
            ("Pagination: Page 2, 2 per page", () => Cms.Content<CmsExampleModel>(CollectionName).GetPage(2).GetPerPage(2)),
        };
    }

    private UILabel _titleLabel = null!;
    private UIButton _btnApply = null!;
    private UIButton _btnSearch = null!;
    private UIProgressView _progressBar = null!;

    private void SetupUI()
    {
        // Title
        _titleLabel = new UILabel
        {
            Text = "CMS Query Builder",
            Font = UIFont.BoldSystemFontOfSize(20),
            TextAlignment = UITextAlignment.Center,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        // Filter picker (button with menu) + Apply
        _btnFilter = UIButton.FromType(UIButtonType.System);
        _btnFilter.SetTitle("Select a filter...", UIControlState.Normal);
        _btnFilter.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
        _btnFilter.TranslatesAutoresizingMaskIntoConstraints = false;
        _btnFilter.Layer.BorderColor = UIColor.SystemGray4.CGColor;
        _btnFilter.Layer.BorderWidth = 1;
        _btnFilter.Layer.CornerRadius = 8;
        _btnFilter.ContentEdgeInsets = new UIEdgeInsets(0, 12, 0, 12);

        if (OperatingSystem.IsIOSVersionAtLeast(14))
        {
            var actions = _cmsFilters.Select((f, idx) => UIAction.Create(f.Label, null, null, action =>
            {
                _selectedFilterIndex = idx;
                _btnFilter.SetTitle(f.Label, UIControlState.Normal);
            })).ToArray();
            _btnFilter.Menu = UIMenu.Create(string.Empty, actions);
            _btnFilter.ShowsMenuAsPrimaryAction = true;
        }

        _btnApply = UIButton.FromType(UIButtonType.System);
        _btnApply.SetTitle("Apply", UIControlState.Normal);
        _btnApply.BackgroundColor = UIColor.SystemBlue;
        _btnApply.SetTitleColor(UIColor.White, UIControlState.Normal);
        _btnApply.Layer.CornerRadius = 8;
        _btnApply.TranslatesAutoresizingMaskIntoConstraints = false;
        _btnApply.TouchUpInside += async (s, e) =>
        {
            if (_selectedFilterIndex < 0 || _selectedFilterIndex >= _cmsFilters.Count)
            {
                ShowAlert("Info", "Please select a filter first.");
                return;
            }
            await LoadResults(_cmsFilters[_selectedFilterIndex].Build());
        };

        // Search bar + Search button
        _searchBar = new UISearchBar
        {
            Placeholder = "Search term...",
            SearchBarStyle = UISearchBarStyle.Minimal,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        _btnSearch = UIButton.FromType(UIButtonType.System);
        _btnSearch.SetTitle("Search", UIControlState.Normal);
        _btnSearch.BackgroundColor = UIColor.SystemBlue;
        _btnSearch.SetTitleColor(UIColor.White, UIControlState.Normal);
        _btnSearch.Layer.CornerRadius = 8;
        _btnSearch.TranslatesAutoresizingMaskIntoConstraints = false;
        _btnSearch.TouchUpInside += async (s, e) =>
        {
            _searchBar.ResignFirstResponder();
            var term = _searchBar.Text?.Trim();
            if (!string.IsNullOrEmpty(term))
                await LoadResults(Cms.Content<CmsExampleModel>(CollectionName).Search(term));
        };

        // Get All List button
        _btnGetAll = UIButton.FromType(UIButtonType.System);
        _btnGetAll.SetTitle("Get All List", UIControlState.Normal);
        _btnGetAll.BackgroundColor = UIColor.SystemIndigo;
        _btnGetAll.SetTitleColor(UIColor.White, UIControlState.Normal);
        _btnGetAll.Layer.CornerRadius = 8;
        _btnGetAll.TranslatesAutoresizingMaskIntoConstraints = false;
        _btnGetAll.TouchUpInside += async (s, e) =>
        {
            await LoadResults(Cms.Content<CmsExampleModel>(CollectionName));
        };

        // Progress bar
        _progressBar = new UIProgressView(UIProgressViewStyle.Bar)
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            Hidden = true
        };

        // Table View
        _tableView = new UITableView
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            RowHeight = UITableView.AutomaticDimension,
            EstimatedRowHeight = 140f,
            SeparatorStyle = UITableViewCellSeparatorStyle.None
        };
        _tableView.RegisterClassForCellReuse(typeof(CmsCell), CmsCell.Key);
        _source = new CmsTableViewSource();
        _tableView.Source = _source;

        // Empty label
        _emptyLabel = new UILabel
        {
            Text = "No entries found.",
            TextColor = UIColor.SecondaryLabel,
            Font = UIFont.SystemFontOfSize(14),
            TextAlignment = UITextAlignment.Center,
            Hidden = true,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        // Filter row container
        var filterRow = new UIView { TranslatesAutoresizingMaskIntoConstraints = false };
        filterRow.AddSubviews(_btnFilter, _btnApply);

        // Search row container
        var searchRow = new UIView { TranslatesAutoresizingMaskIntoConstraints = false };
        searchRow.AddSubviews(_searchBar, _btnSearch);

        View!.AddSubviews(_titleLabel, filterRow, searchRow, _btnGetAll, _progressBar, _tableView, _emptyLabel);

        var g = View.SafeAreaLayoutGuide;
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            // Title
            _titleLabel.TopAnchor.ConstraintEqualTo(g.TopAnchor, 8),
            _titleLabel.LeadingAnchor.ConstraintEqualTo(g.LeadingAnchor, 16),
            _titleLabel.TrailingAnchor.ConstraintEqualTo(g.TrailingAnchor, -16),

            // Filter row
            filterRow.TopAnchor.ConstraintEqualTo(_titleLabel.BottomAnchor, 12),
            filterRow.LeadingAnchor.ConstraintEqualTo(g.LeadingAnchor, 16),
            filterRow.TrailingAnchor.ConstraintEqualTo(g.TrailingAnchor, -16),
            filterRow.HeightAnchor.ConstraintEqualTo(44),

            _btnFilter.TopAnchor.ConstraintEqualTo(filterRow.TopAnchor),
            _btnFilter.LeadingAnchor.ConstraintEqualTo(filterRow.LeadingAnchor),
            _btnFilter.TrailingAnchor.ConstraintEqualTo(_btnApply.LeadingAnchor, -8),
            _btnFilter.BottomAnchor.ConstraintEqualTo(filterRow.BottomAnchor),

            _btnApply.TopAnchor.ConstraintEqualTo(filterRow.TopAnchor),
            _btnApply.TrailingAnchor.ConstraintEqualTo(filterRow.TrailingAnchor),
            _btnApply.WidthAnchor.ConstraintEqualTo(80),
            _btnApply.BottomAnchor.ConstraintEqualTo(filterRow.BottomAnchor),

            // Search row
            searchRow.TopAnchor.ConstraintEqualTo(filterRow.BottomAnchor, 8),
            searchRow.LeadingAnchor.ConstraintEqualTo(g.LeadingAnchor, 8),
            searchRow.TrailingAnchor.ConstraintEqualTo(g.TrailingAnchor, -16),
            searchRow.HeightAnchor.ConstraintEqualTo(44),

            _searchBar.TopAnchor.ConstraintEqualTo(searchRow.TopAnchor),
            _searchBar.LeadingAnchor.ConstraintEqualTo(searchRow.LeadingAnchor),
            _searchBar.TrailingAnchor.ConstraintEqualTo(_btnSearch.LeadingAnchor, -8),
            _searchBar.BottomAnchor.ConstraintEqualTo(searchRow.BottomAnchor),

            _btnSearch.TopAnchor.ConstraintEqualTo(searchRow.TopAnchor),
            _btnSearch.TrailingAnchor.ConstraintEqualTo(searchRow.TrailingAnchor),
            _btnSearch.WidthAnchor.ConstraintEqualTo(80),
            _btnSearch.BottomAnchor.ConstraintEqualTo(searchRow.BottomAnchor),

            // Get All List
            _btnGetAll.TopAnchor.ConstraintEqualTo(searchRow.BottomAnchor, 10),
            _btnGetAll.LeadingAnchor.ConstraintEqualTo(g.LeadingAnchor, 16),
            _btnGetAll.TrailingAnchor.ConstraintEqualTo(g.TrailingAnchor, -16),
            _btnGetAll.HeightAnchor.ConstraintEqualTo(44),

            // Progress
            _progressBar.TopAnchor.ConstraintEqualTo(_btnGetAll.BottomAnchor, 8),
            _progressBar.LeadingAnchor.ConstraintEqualTo(g.LeadingAnchor, 16),
            _progressBar.TrailingAnchor.ConstraintEqualTo(g.TrailingAnchor, -16),

            // Table
            _tableView.TopAnchor.ConstraintEqualTo(_progressBar.BottomAnchor, 4),
            _tableView.LeadingAnchor.ConstraintEqualTo(g.LeadingAnchor),
            _tableView.TrailingAnchor.ConstraintEqualTo(g.TrailingAnchor),
            _tableView.BottomAnchor.ConstraintEqualTo(g.BottomAnchor),

            _emptyLabel.CenterXAnchor.ConstraintEqualTo(_tableView.CenterXAnchor),
            _emptyLabel.CenterYAnchor.ConstraintEqualTo(_tableView.CenterYAnchor),
        });
    }

    private void ShowAlert(string title, string message)
    {
        var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
        PresentViewController(alert, true, null);
    }

    private async Task LoadResults(ICmsQueryBuilder<CmsExampleModel> query)
    {
        InvokeOnMainThread(() => { _progressBar.Hidden = false; _progressBar.Progress = 0.5f; });
        try
        {
            var items = await query.GetListAsync();
            InvokeOnMainThread(() =>
            {
                _source.UpdateData(items);
                _tableView.ReloadData();
                _emptyLabel.Hidden = items.Count != 0;
                _progressBar.Hidden = true;
            });
        }
        catch (Exception ex)
        {
            InvokeOnMainThread(() =>
            {
                _progressBar.Hidden = true;
                ShowAlert("Error", ex.Message);
            });
        }
    }
}

public class CmsTableViewSource : UITableViewSource
{
    private List<CmsExampleModel> _items = new();

    public void UpdateData(List<CmsExampleModel> items)
    {
        _items = items;
    }

    public override nint RowsInSection(UITableView tableview, nint section) => _items.Count;

    public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
    {
        var cell = (CmsCell)tableView.DequeueReusableCell(CmsCell.Key, indexPath);
        cell.Bind(_items[indexPath.Row]);
        return cell;
    }
}

public class CmsCell : UITableViewCell
{
    public static readonly NSString Key = new NSString(nameof(CmsCell));

    private UIImageView _imgProduct = null!;
    private UIView _card = null!;
    private UILabel _lblProduct = null!;
    private UILabel _lblCategory = null!;
    private UILabel _lblDesc = null!;
    private UILabel _lblPrice = null!;
    private UILabel _lblSkuLine = null!;
    private UILabel _lblSupport = null!;
    private UILabel _lblIdAndDates = null!;

    [Export("initWithStyle:reuseIdentifier:")]
    public CmsCell(UITableViewCellStyle style, NSString reuseIdentifier) : base(style, reuseIdentifier)
    {
        SelectionStyle = UITableViewCellSelectionStyle.None;

        // Card container
        var card = new UIView
        {
            BackgroundColor = UIColor.SecondarySystemGroupedBackground,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        card.Layer.CornerRadius = 12;
        card.Layer.ShadowColor = UIColor.Black.CGColor;
        card.Layer.ShadowOpacity = 0.1f;
        card.Layer.ShadowOffset = new CoreGraphics.CGSize(0, 2);
        card.Layer.ShadowRadius = 4;
        _card = card;

        _imgProduct = new UIImageView
        {
            ContentMode = UIViewContentMode.ScaleAspectFill,
            ClipsToBounds = true,
            BackgroundColor = UIColor.SystemGray5,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        _imgProduct.Layer.CornerRadius = 8;

        _lblProduct = new UILabel { Font = UIFont.BoldSystemFontOfSize(16), TranslatesAutoresizingMaskIntoConstraints = false };
        _lblCategory = new UILabel { Font = UIFont.BoldSystemFontOfSize(11), TextColor = UIColor.SystemBlue, TranslatesAutoresizingMaskIntoConstraints = false, TextAlignment = UITextAlignment.Right };
        _lblDesc = new UILabel { Font = UIFont.SystemFontOfSize(12), TextColor = UIColor.DarkGray, Lines = 2, TranslatesAutoresizingMaskIntoConstraints = false };

        _lblPrice = new UILabel { Font = UIFont.BoldSystemFontOfSize(13), TextColor = UIColor.SystemGreen, TranslatesAutoresizingMaskIntoConstraints = false };
        _lblSkuLine = new UILabel { Font = UIFont.SystemFontOfSize(12), TextColor = UIColor.Gray, TranslatesAutoresizingMaskIntoConstraints = false };

        _lblSupport = new UILabel { Font = UIFont.SystemFontOfSize(11), TextColor = UIColor.LightGray, TranslatesAutoresizingMaskIntoConstraints = false };
        _lblIdAndDates = new UILabel { Font = UIFont.SystemFontOfSize(10), TextColor = UIColor.LightGray, Lines = 2, TranslatesAutoresizingMaskIntoConstraints = false, LineBreakMode = UILineBreakMode.MiddleTruncation };

        ContentView.AddSubview(card);
        card.AddSubviews(_imgProduct, _lblProduct, _lblCategory, _lblDesc, _lblPrice, _lblSkuLine, _lblSupport, _lblIdAndDates);

        NSLayoutConstraint.ActivateConstraints(new[]
        {
            // Card margins
            card.TopAnchor.ConstraintEqualTo(ContentView.TopAnchor, 6),
            card.LeadingAnchor.ConstraintEqualTo(ContentView.LeadingAnchor, 16),
            card.TrailingAnchor.ConstraintEqualTo(ContentView.TrailingAnchor, -16),
            card.BottomAnchor.ConstraintEqualTo(ContentView.BottomAnchor, -6),

            // Thumbnail 80x80 left-aligned
            _imgProduct.TopAnchor.ConstraintEqualTo(card.TopAnchor, 12),
            _imgProduct.LeadingAnchor.ConstraintEqualTo(card.LeadingAnchor, 12),
            _imgProduct.WidthAnchor.ConstraintEqualTo(80),
            _imgProduct.HeightAnchor.ConstraintEqualTo(80),

            // Product name + category right of image
            _lblProduct.TopAnchor.ConstraintEqualTo(card.TopAnchor, 12),
            _lblProduct.LeadingAnchor.ConstraintEqualTo(_imgProduct.TrailingAnchor, 16),
            _lblProduct.TrailingAnchor.ConstraintLessThanOrEqualTo(_lblCategory.LeadingAnchor, -8),

            _lblCategory.CenterYAnchor.ConstraintEqualTo(_lblProduct.CenterYAnchor),
            _lblCategory.TrailingAnchor.ConstraintEqualTo(card.TrailingAnchor, -12),

            _lblDesc.TopAnchor.ConstraintEqualTo(_lblProduct.BottomAnchor, 4),
            _lblDesc.LeadingAnchor.ConstraintEqualTo(_imgProduct.TrailingAnchor, 16),
            _lblDesc.TrailingAnchor.ConstraintEqualTo(card.TrailingAnchor, -12),

            // SKU + Price below image row
            _lblSkuLine.TopAnchor.ConstraintEqualTo(_imgProduct.BottomAnchor, 8),
            _lblSkuLine.LeadingAnchor.ConstraintEqualTo(card.LeadingAnchor, 12),

            _lblPrice.CenterYAnchor.ConstraintEqualTo(_lblSkuLine.CenterYAnchor),
            _lblPrice.LeadingAnchor.ConstraintEqualTo(_lblSkuLine.TrailingAnchor, 16),

            _lblSupport.TopAnchor.ConstraintEqualTo(_lblSkuLine.BottomAnchor, 4),
            _lblSupport.LeadingAnchor.ConstraintEqualTo(card.LeadingAnchor, 12),

            _lblIdAndDates.TopAnchor.ConstraintEqualTo(_lblSupport.BottomAnchor, 4),
            _lblIdAndDates.LeadingAnchor.ConstraintEqualTo(card.LeadingAnchor, 12),
            _lblIdAndDates.TrailingAnchor.ConstraintEqualTo(card.TrailingAnchor, -12),
            _lblIdAndDates.BottomAnchor.ConstraintEqualTo(card.BottomAnchor, -12),
        });
    }

    public void Bind(CmsExampleModel item)
    {
        _lblProduct.Text = item.ProductName;
        _lblCategory.Text = item.Category?.Count > 0 ? $"🏷️ {string.Join(", ", item.Category)}" : "";
        _lblDesc.Text = item.Description;
        _lblSkuLine.Text = $"{item.ItemSku}    Stock: {item.InStock}";
        _lblPrice.Text = $"${item.Price:F2}";
        _lblSupport.Text = $"📧 {item.SupportEmail}";
        _lblIdAndDates.Text = $"ID: {item.Id}\nCr: {item.CreatedAt:dd/MM/yyyy}    Pub: {item.PublishedAt:dd/MM/yyyy}    Upd: {item.UpdatedAt:dd/MM/yyyy}";

        if (!string.IsNullOrWhiteSpace(item.ProductImageUrl))
            ImageUtils.LoadAsync(item.ProductImageUrl!, _imgProduct, item.Id ?? item.ProductImageUrl!);
        else
            _imgProduct.Image = null;
    }
}
