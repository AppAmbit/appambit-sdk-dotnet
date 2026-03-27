using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppAmbit;
using AppAmbitTestingiOS.Models;
using Foundation;
using UIKit;

namespace AppAmbitTestingiOS;

public class CmsViewController : UIViewController, IUISearchBarDelegate
{
    private UITableView _tableView = null!;
    private UISearchBar _searchBar = null!;
    private UIButton _btnFilter = null!;
    private UIButton _btnGetAll = null!;

    private CmsTableViewSource _source = null!;
    private const string CollectionName = "tech_inventory";

    private List<(string Label, Func<CmsQueryBuilder<CmsExampleModel>> Build)> _cmsFilters = new();

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Title = "CMS Query Builder";
        View!.BackgroundColor = UIColor.SystemBackground;

        SetupFilters();
        SetupUI();

        _ = LoadResults(Cms.For<CmsExampleModel>(CollectionName));
    }

    private void SetupFilters()
    {
        _cmsFilters = new()
        {
            ("Equals: item_sku = TEC-02", () => Cms.For<CmsExampleModel>(CollectionName).Equals("item_sku", "TEC-02")),
            ("Not Equals: item_sku ≠ TEC-02", () => Cms.For<CmsExampleModel>(CollectionName).NotEquals("item_sku", "TEC-02")),
            ("Contains: product_name contains 'Pro'", () => Cms.For<CmsExampleModel>(CollectionName).Contains("product_name", "Pro")),
            ("Starts With: item_sku starts with 'TEC'", () => Cms.For<CmsExampleModel>(CollectionName).StartsWith("item_sku", "TEC")),
            ("In List: [TEC-01, TEC-02]", () => Cms.For<CmsExampleModel>(CollectionName).InList("item_sku", new[] { "TEC-01", "TEC-02" })),
            ("Greater Than: price > 500", () => Cms.For<CmsExampleModel>(CollectionName).GreaterThan("price", 500)),
            ("Order By price DESC", () => Cms.For<CmsExampleModel>(CollectionName).OrderByDescending("price")),
            ("Pagination: Page 1, 2 items", () => Cms.For<CmsExampleModel>(CollectionName).SetPage(1).SetPerPage(2)),
            ("Pagination: Page 2, 2 items", () => Cms.For<CmsExampleModel>(CollectionName).SetPage(2).SetPerPage(2)),
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
            await Cms.Clear("sistema_de_gestion_de_propiedades_de_una_marinaclub_nautico");
            await LoadResults(Cms.For<CmsExampleModel>(CollectionName));
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
            RowHeight = 160
        };
        _tableView.RegisterClassForCellReuse(typeof(CmsCell), CmsCell.Key);
        _source = new CmsTableViewSource();
        _tableView.Source = _source;

        View!.AddSubviews(_searchBar, _btnFilter, _btnGetAll, _tableView);

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
            _tableView.BottomAnchor.ConstraintEqualTo(g.BottomAnchor)
        });
    }

    [Export("searchBarSearchButtonClicked:")]
    public void SearchButtonClicked(UISearchBar searchBar)
    {
        searchBar.ResignFirstResponder();
        var term = searchBar.Text?.Trim();
        if (!string.IsNullOrEmpty(term))
            _ = LoadResults(Cms.For<CmsExampleModel>(CollectionName).Search(term));
    }

    private async Task LoadResults(CmsQueryBuilder<CmsExampleModel> query)
    {
        try
        {
            var items = await query.GetListAsync();
            InvokeOnMainThread(() =>
            {
                _source.UpdateData(items);
                _tableView.ReloadData();
                
                if (items.Count == 0)
                {
                    var alert = UIAlertController.Create("Info", "No entries found. Cache may be empty.", UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                    PresentViewController(alert, true, null);
                }
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
        
        _lblProduct = new UILabel { Font = UIFont.BoldSystemFontOfSize(16), TranslatesAutoresizingMaskIntoConstraints = false };
        _lblCategory = new UILabel { Font = UIFont.BoldSystemFontOfSize(11), TextColor = UIColor.SystemBlue, TranslatesAutoresizingMaskIntoConstraints = false, TextAlignment = UITextAlignment.Right };
        _lblDesc = new UILabel { Font = UIFont.SystemFontOfSize(12), TextColor = UIColor.DarkGray, Lines = 2, TranslatesAutoresizingMaskIntoConstraints = false };
        
        _lblPrice = new UILabel { Font = UIFont.BoldSystemFontOfSize(13), TextColor = UIColor.SystemGreen, TranslatesAutoresizingMaskIntoConstraints = false };
        _lblSkuLine = new UILabel { Font = UIFont.SystemFontOfSize(12), TextColor = UIColor.Gray, TranslatesAutoresizingMaskIntoConstraints = false };
        
        _lblSupport = new UILabel { Font = UIFont.SystemFontOfSize(11), TextColor = UIColor.LightGray, TranslatesAutoresizingMaskIntoConstraints = false };
        _lblIdAndDates = new UILabel { Font = UIFont.SystemFontOfSize(10), TextColor = UIColor.LightGray, Lines = 2, TranslatesAutoresizingMaskIntoConstraints = false, LineBreakMode = UILineBreakMode.MiddleTruncation };

        ContentView.AddSubviews(_lblProduct, _lblCategory, _lblDesc, _lblPrice, _lblSkuLine, _lblSupport, _lblIdAndDates);

        NSLayoutConstraint.ActivateConstraints(new[]
        {
            _lblProduct.TopAnchor.ConstraintEqualTo(ContentView.TopAnchor, 12),
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
        });
    }

    public void Bind(CmsExampleModel item)
    {
        _lblProduct.Text = item.ProductName;
        _lblCategory.Text = string.IsNullOrEmpty(item.Category) ? "" : $"🏷️ {item.Category}";
        _lblDesc.Text = item.Description;
        _lblSkuLine.Text = $"{item.ItemSku}  |  Stock: {item.InStock}";
        _lblPrice.Text = $"${item.Price:F2}";
        _lblSupport.Text = $"📧 {item.SupportEmail}";
        _lblIdAndDates.Text = $"ID: {item.Id}\nCr: {item.CreatedAt:dd/MM/yy} | Pub: {item.PublishedAt:dd/MM/yy} | Upd: {item.UpdatedAt:dd/MM/yy}";
    }
}
