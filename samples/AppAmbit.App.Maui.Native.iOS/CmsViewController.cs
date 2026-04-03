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

public class CmsViewController : UIViewController, IUISearchBarDelegate
{
    private UITableView _tableView = null!;
    private UISearchBar _searchBar = null!;
    private UIButton _btnFilter = null!;
    private UIButton _btnGetAll = null!;
    private UILabel _emptyLabel = null!;

    private CmsTableViewSource _source = null!;
    private const string CollectionName = "tech_inventory";

    private List<(string Label, Func<ICmsQueryBuilder<CmsExampleModel>> Build)> _cmsFilters = new();

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Title = "CMS Query Builder";
        View!.BackgroundColor = UIColor.SystemBackground;

        SetupFilters();
        SetupUI();

        _ = LoadResults(Cms.Content<CmsExampleModel>(CollectionName));
    }

    private void SetupFilters()
    {
        _cmsFilters = new()
        {
            ("Equals: item_sku = TEC-02", () => Cms.Content<CmsExampleModel>(CollectionName).Equals("item_sku", "TEC-02")),
            ("Not Equals: item_sku ≠ TEC-02", () => Cms.Content<CmsExampleModel>(CollectionName).NotEquals("item_sku", "TEC-02")),
            ("Contains: product_name contains 'Pro'", () => Cms.Content<CmsExampleModel>(CollectionName).Contains("product_name", "Pro")),
            ("Starts With: item_sku starts with 'TEC'", () => Cms.Content<CmsExampleModel>(CollectionName).StartsWith("item_sku", "TEC")),
            ("In List: [TEC-01, TEC-02]", () => Cms.Content<CmsExampleModel>(CollectionName).InList("item_sku", new[] { "TEC-01", "TEC-02" })),
            ("Greater Than: price > 500", () => Cms.Content<CmsExampleModel>(CollectionName).GreaterThan("price", 500)),
            ("Order By price DESC", () => Cms.Content<CmsExampleModel>(CollectionName).OrderByDescending("price")),
            ("Pagination: Page 1, 2 items", () => Cms.Content<CmsExampleModel>(CollectionName).GetPage(1).GetPerPage(2)),
            ("Pagination: Page 2, 2 items", () => Cms.Content<CmsExampleModel>(CollectionName).GetPage(2).GetPerPage(2)),
        };
    }

    private void SetupUI()
    {
        // Search Bar
        _searchBar = new UISearchBar
        {
            Placeholder = "Search term...",
            Delegate = this,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        // Get All Button
        _btnGetAll = UIButton.FromType(UIButtonType.System);
        _btnGetAll.SetTitle("Get All List", UIControlState.Normal);
        _btnGetAll.TranslatesAutoresizingMaskIntoConstraints = false;
        _btnGetAll.TouchUpInside += async (s, e) =>
        {
            await Cms.ClearCache(CollectionName);
            await LoadResults(Cms.Content<CmsExampleModel>(CollectionName));
        };

        // Filter Menu Button
        _btnFilter = UIButton.FromType(UIButtonType.System);
        _btnFilter.SetTitle("Filters 🔽", UIControlState.Normal);
        _btnFilter.TranslatesAutoresizingMaskIntoConstraints = false;
        if (OperatingSystem.IsIOSVersionAtLeast(14))
        {
            var actions = _cmsFilters.Select(f => UIAction.Create(f.Label, null, null, action =>
            {
                _ = LoadResults(f.Build());
            })).ToArray();
            _btnFilter.Menu = UIMenu.Create(string.Empty, actions);
            _btnFilter.ShowsMenuAsPrimaryAction = true;
        }

        // Table View
        _tableView = new UITableView
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            RowHeight = UITableView.AutomaticDimension,
            EstimatedRowHeight = 330f
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

        View!.AddSubviews(_searchBar, _btnFilter, _btnGetAll, _tableView, _emptyLabel);

        // Constraints
        var g = View.SafeAreaLayoutGuide;
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            _searchBar.TopAnchor.ConstraintEqualTo(g.TopAnchor),
            _searchBar.LeadingAnchor.ConstraintEqualTo(g.LeadingAnchor),
            _searchBar.TrailingAnchor.ConstraintEqualTo(g.TrailingAnchor),

            _btnFilter.TopAnchor.ConstraintEqualTo(_searchBar.BottomAnchor, 8),
            _btnFilter.LeadingAnchor.ConstraintEqualTo(g.LeadingAnchor, 16),
            _btnFilter.HeightAnchor.ConstraintEqualTo(44),

            _btnGetAll.TopAnchor.ConstraintEqualTo(_searchBar.BottomAnchor, 8),
            _btnGetAll.TrailingAnchor.ConstraintEqualTo(g.TrailingAnchor, -16),
            _btnGetAll.HeightAnchor.ConstraintEqualTo(44),

            _tableView.TopAnchor.ConstraintEqualTo(_btnFilter.BottomAnchor, 8),
            _tableView.LeadingAnchor.ConstraintEqualTo(g.LeadingAnchor),
            _tableView.TrailingAnchor.ConstraintEqualTo(g.TrailingAnchor),
            _tableView.BottomAnchor.ConstraintEqualTo(g.BottomAnchor),

            _emptyLabel.CenterXAnchor.ConstraintEqualTo(_tableView.CenterXAnchor),
            _emptyLabel.CenterYAnchor.ConstraintEqualTo(_tableView.CenterYAnchor),
        });
    }

    [Export("searchBarSearchButtonClicked:")]
    public void SearchButtonClicked(UISearchBar searchBar)
    {
        searchBar.ResignFirstResponder();
        var term = searchBar.Text?.Trim();
        if (!string.IsNullOrEmpty(term))
            _ = LoadResults(Cms.Content<CmsExampleModel>(CollectionName).Search(term));
    }

    private async Task LoadResults(ICmsQueryBuilder<CmsExampleModel> query)
    {
        try
        {
            var items = await query.GetListAsync();
            InvokeOnMainThread(() =>
            {
                _source.UpdateData(items);
                _tableView.ReloadData();
                _emptyLabel.Hidden = items.Count != 0;
            });
        }
        catch (Exception ex)
        {
            InvokeOnMainThread(() =>
            {
                var alert = UIAlertController.Create("Error", ex.Message, UIAlertControllerStyle.Alert);
                alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                PresentViewController(alert, true, null);
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

        _imgProduct = new UIImageView
        {
            ContentMode = UIViewContentMode.ScaleAspectFill,
            ClipsToBounds = true,
            BackgroundColor = UIColor.SystemGray5,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        _imgProduct.Layer.CornerRadius = 0;

        _lblProduct = new UILabel { Font = UIFont.BoldSystemFontOfSize(16), TranslatesAutoresizingMaskIntoConstraints = false };
        _lblCategory = new UILabel { Font = UIFont.BoldSystemFontOfSize(11), TextColor = UIColor.SystemBlue, TranslatesAutoresizingMaskIntoConstraints = false, TextAlignment = UITextAlignment.Right };
        _lblDesc = new UILabel { Font = UIFont.SystemFontOfSize(12), TextColor = UIColor.DarkGray, Lines = 2, TranslatesAutoresizingMaskIntoConstraints = false };

        _lblPrice = new UILabel { Font = UIFont.BoldSystemFontOfSize(13), TextColor = UIColor.SystemGreen, TranslatesAutoresizingMaskIntoConstraints = false };
        _lblSkuLine = new UILabel { Font = UIFont.SystemFontOfSize(12), TextColor = UIColor.Gray, TranslatesAutoresizingMaskIntoConstraints = false };

        _lblSupport = new UILabel { Font = UIFont.SystemFontOfSize(11), TextColor = UIColor.LightGray, TranslatesAutoresizingMaskIntoConstraints = false };
        _lblIdAndDates = new UILabel { Font = UIFont.SystemFontOfSize(10), TextColor = UIColor.LightGray, Lines = 2, TranslatesAutoresizingMaskIntoConstraints = false, LineBreakMode = UILineBreakMode.MiddleTruncation };

        ContentView.AddSubviews(_imgProduct, _lblProduct, _lblCategory, _lblDesc, _lblPrice, _lblSkuLine, _lblSupport, _lblIdAndDates);

        NSLayoutConstraint.ActivateConstraints(new[]
        {
            // Image — full width banner at top
            _imgProduct.TopAnchor.ConstraintEqualTo(ContentView.TopAnchor),
            _imgProduct.LeadingAnchor.ConstraintEqualTo(ContentView.LeadingAnchor),
            _imgProduct.TrailingAnchor.ConstraintEqualTo(ContentView.TrailingAnchor),
            _imgProduct.HeightAnchor.ConstraintEqualTo(150f),

            // Product name + category below image
            _lblProduct.TopAnchor.ConstraintEqualTo(_imgProduct.BottomAnchor, 12),
            _lblProduct.LeadingAnchor.ConstraintEqualTo(ContentView.LeadingAnchor, 16),
            _lblProduct.TrailingAnchor.ConstraintLessThanOrEqualTo(_lblCategory.LeadingAnchor, -8),

            _lblCategory.CenterYAnchor.ConstraintEqualTo(_lblProduct.CenterYAnchor),
            _lblCategory.TrailingAnchor.ConstraintEqualTo(ContentView.TrailingAnchor, -16),
            _lblCategory.WidthAnchor.ConstraintGreaterThanOrEqualTo(80),

            _lblDesc.TopAnchor.ConstraintEqualTo(_lblProduct.BottomAnchor, 8),
            _lblDesc.LeadingAnchor.ConstraintEqualTo(ContentView.LeadingAnchor, 16),
            _lblDesc.TrailingAnchor.ConstraintEqualTo(ContentView.TrailingAnchor, -16),

            _lblSkuLine.TopAnchor.ConstraintEqualTo(_lblDesc.BottomAnchor, 8),
            _lblSkuLine.LeadingAnchor.ConstraintEqualTo(ContentView.LeadingAnchor, 16),

            _lblPrice.CenterYAnchor.ConstraintEqualTo(_lblSkuLine.CenterYAnchor),
            _lblPrice.LeadingAnchor.ConstraintEqualTo(_lblSkuLine.TrailingAnchor, 16),

            _lblSupport.TopAnchor.ConstraintEqualTo(_lblSkuLine.BottomAnchor, 8),
            _lblSupport.LeadingAnchor.ConstraintEqualTo(ContentView.LeadingAnchor, 16),

            _lblIdAndDates.TopAnchor.ConstraintEqualTo(_lblSupport.BottomAnchor, 8),
            _lblIdAndDates.LeadingAnchor.ConstraintEqualTo(ContentView.LeadingAnchor, 16),
            _lblIdAndDates.TrailingAnchor.ConstraintEqualTo(ContentView.TrailingAnchor, -16),
            _lblIdAndDates.BottomAnchor.ConstraintEqualTo(ContentView.BottomAnchor, -12),
        });
    }

    public void Bind(CmsExampleModel item)
    {
        _lblProduct.Text = item.ProductName;
        _lblCategory.Text = item.Category?.Count > 0 ? $"🏷️ {string.Join(", ", item.Category)}" : "";
        _lblDesc.Text = item.Description;
        _lblSkuLine.Text = $"{item.ItemSku}  |  Stock: {item.InStock}";
        _lblPrice.Text = $"${item.Price:F2}";
        _lblSupport.Text = $"📧 {item.SupportEmail}";
        _lblIdAndDates.Text = $"ID: {item.Id}\nCr: {item.CreatedAt:dd/MM/yy} | Pub: {item.PublishedAt:dd/MM/yy} | Upd: {item.UpdatedAt:dd/MM/yy}";

        if (!string.IsNullOrWhiteSpace(item.ProductImageUrl))
            ImageUtils.LoadAsync(item.ProductImageUrl!, _imgProduct, item.Id ?? item.ProductImageUrl!);
        else
            _imgProduct.Image = null;
    }
}
