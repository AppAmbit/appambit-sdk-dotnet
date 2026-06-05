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
    private const string CollectionName = "blog_extended";

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
            ("Title = T20",                  () => Cms.Content<CmsExampleModel>(CollectionName).Equals("title", "T20")),
            ("Title ≠ T20",                  () => Cms.Content<CmsExampleModel>(CollectionName).NotEquals("title", "T20")),
            ("Is Published = true",          () => Cms.Content<CmsExampleModel>(CollectionName).Equals("is_published", "true")),
            ("Is Published = false",         () => Cms.Content<CmsExampleModel>(CollectionName).Equals("is_published", "false")),
            ("Title contains 't1'",          () => Cms.Content<CmsExampleModel>(CollectionName).Contains("title", "t1")),
            ("Title starts with 't'",        () => Cms.Content<CmsExampleModel>(CollectionName).StartsWith("title", "t")),
            ("Category IN [science, tech]",  () => Cms.Content<CmsExampleModel>(CollectionName).InList("category", new[] { "science", "tech" })),
            ("Category NOT IN [tech, news]", () => Cms.Content<CmsExampleModel>(CollectionName).NotInList("category", new[] { "tech", "news" })),
            ("Views > 1000",                 () => Cms.Content<CmsExampleModel>(CollectionName).GreaterThan("views_count", 1000)),
            ("Views ≥ 555",                  () => Cms.Content<CmsExampleModel>(CollectionName).GreaterThanOrEqual("views_count", 555)),
            ("Views < 15000",                () => Cms.Content<CmsExampleModel>(CollectionName).LessThan("views_count", 15000)),
            ("Views ≤ 15000",                () => Cms.Content<CmsExampleModel>(CollectionName).LessThanOrEqual("views_count", 15000)),
            ("Sort Title ↑",                 () => Cms.Content<CmsExampleModel>(CollectionName).OrderByAscending("title")),
            ("Sort Title ↓",                 () => Cms.Content<CmsExampleModel>(CollectionName).OrderByDescending("title")),
            ("Sort Views ↑",                 () => Cms.Content<CmsExampleModel>(CollectionName).OrderByAscending("views_count")),
            ("Sort Views ↓",                 () => Cms.Content<CmsExampleModel>(CollectionName).OrderByDescending("views_count")),
            ("Page 1 (2 per page)",          () => Cms.Content<CmsExampleModel>(CollectionName).GetPage(1).GetPerPage(2)),
            ("Page 2 (2 per page)",          () => Cms.Content<CmsExampleModel>(CollectionName).GetPage(2).GetPerPage(2)),
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

    private UIImageView _imgFeatured = null!;
    private UIView _card = null!;
    private UILabel _lblTitle = null!;
    private UILabel _lblAuthor = null!;
    private UILabel _lblBody = null!;
    private UILabel _lblStats = null!;
    private UILabel _lblCategory = null!;
    private UILabel _lblPublished = null!;
    private UILabel _lblPublishedAt = null!;

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

        _imgFeatured = new UIImageView
        {
            ContentMode = UIViewContentMode.ScaleAspectFill,
            ClipsToBounds = true,
            BackgroundColor = UIColor.SystemGray5,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        _imgFeatured.Layer.CornerRadius = 8;

        _lblTitle     = new UILabel { Font = UIFont.BoldSystemFontOfSize(16), TranslatesAutoresizingMaskIntoConstraints = false };
        _lblAuthor    = new UILabel { Font = UIFont.SystemFontOfSize(11), TextColor = UIColor.SystemGray, TranslatesAutoresizingMaskIntoConstraints = false, TextAlignment = UITextAlignment.Right };
        _lblBody      = new UILabel { Font = UIFont.SystemFontOfSize(13), TextColor = UIColor.DarkGray, Lines = 3, TranslatesAutoresizingMaskIntoConstraints = false };
        _lblStats     = new UILabel { Font = UIFont.SystemFontOfSize(12), TextColor = UIColor.SystemGray, TranslatesAutoresizingMaskIntoConstraints = false };
        _lblCategory  = new UILabel { Font = UIFont.BoldSystemFontOfSize(11), TextColor = UIColor.SystemBlue, TranslatesAutoresizingMaskIntoConstraints = false };
        _lblPublished = new UILabel { Font = UIFont.BoldSystemFontOfSize(11), TranslatesAutoresizingMaskIntoConstraints = false };
        _lblPublishedAt = new UILabel { Font = UIFont.SystemFontOfSize(11), TextColor = UIColor.SystemGray, TranslatesAutoresizingMaskIntoConstraints = false, TextAlignment = UITextAlignment.Right };

        ContentView.AddSubview(card);
        card.AddSubviews(_imgFeatured, _lblTitle, _lblAuthor, _lblBody, _lblStats, _lblCategory, _lblPublished, _lblPublishedAt);

        NSLayoutConstraint.ActivateConstraints(new[]
        {
            // Card margins
            card.TopAnchor.ConstraintEqualTo(ContentView.TopAnchor, 6),
            card.LeadingAnchor.ConstraintEqualTo(ContentView.LeadingAnchor, 16),
            card.TrailingAnchor.ConstraintEqualTo(ContentView.TrailingAnchor, -16),
            card.BottomAnchor.ConstraintEqualTo(ContentView.BottomAnchor, -6),

            // Featured image: full width, 160pt tall
            _imgFeatured.TopAnchor.ConstraintEqualTo(card.TopAnchor, 12),
            _imgFeatured.LeadingAnchor.ConstraintEqualTo(card.LeadingAnchor, 12),
            _imgFeatured.TrailingAnchor.ConstraintEqualTo(card.TrailingAnchor, -12),
            _imgFeatured.HeightAnchor.ConstraintEqualTo(160),

            // Title + Author
            _lblTitle.TopAnchor.ConstraintEqualTo(_imgFeatured.BottomAnchor, 10),
            _lblTitle.LeadingAnchor.ConstraintEqualTo(card.LeadingAnchor, 12),
            _lblTitle.TrailingAnchor.ConstraintLessThanOrEqualTo(_lblAuthor.LeadingAnchor, -8),

            _lblAuthor.CenterYAnchor.ConstraintEqualTo(_lblTitle.CenterYAnchor),
            _lblAuthor.TrailingAnchor.ConstraintEqualTo(card.TrailingAnchor, -12),

            // Body
            _lblBody.TopAnchor.ConstraintEqualTo(_lblTitle.BottomAnchor, 6),
            _lblBody.LeadingAnchor.ConstraintEqualTo(card.LeadingAnchor, 12),
            _lblBody.TrailingAnchor.ConstraintEqualTo(card.TrailingAnchor, -12),

            // Stats (likes, rating, reading time)
            _lblStats.TopAnchor.ConstraintEqualTo(_lblBody.BottomAnchor, 6),
            _lblStats.LeadingAnchor.ConstraintEqualTo(card.LeadingAnchor, 12),
            _lblStats.TrailingAnchor.ConstraintEqualTo(card.TrailingAnchor, -12),

            // Categories
            _lblCategory.TopAnchor.ConstraintEqualTo(_lblStats.BottomAnchor, 4),
            _lblCategory.LeadingAnchor.ConstraintEqualTo(card.LeadingAnchor, 12),
            _lblCategory.TrailingAnchor.ConstraintEqualTo(card.TrailingAnchor, -12),

            // Published status + date
            _lblPublished.TopAnchor.ConstraintEqualTo(_lblCategory.BottomAnchor, 6),
            _lblPublished.LeadingAnchor.ConstraintEqualTo(card.LeadingAnchor, 12),

            _lblPublishedAt.CenterYAnchor.ConstraintEqualTo(_lblPublished.CenterYAnchor),
            _lblPublishedAt.TrailingAnchor.ConstraintEqualTo(card.TrailingAnchor, -12),
            _lblPublishedAt.BottomAnchor.ConstraintEqualTo(card.BottomAnchor, -12),
        });
    }

    public void Bind(CmsExampleModel item)
    {
        _lblTitle.Text       = item.Title;
        _lblAuthor.Text      = !string.IsNullOrWhiteSpace(item.AuthorEmail) ? $"✍️ {item.AuthorEmail}" : "";
        _lblBody.Text        = item.Body;
        _lblStats.Text       = $"👁️ {item.ViewsCount:N0} views" +
                               (!string.IsNullOrWhiteSpace(item.EventDate) ? $"   📅 {item.EventDate}" : "");
        _lblCategory.Text    = item.Category?.Count > 0 ? string.Join(", ", item.Category) : "";
        _lblPublished.Text   = item.IsPublished == true ? "✓ Published" : "✕ Draft";
        _lblPublished.TextColor = item.IsPublished == true ? UIColor.SystemGreen : UIColor.SystemOrange;
        _lblPublishedAt.Text = !string.IsNullOrWhiteSpace(item.PublishedAt) ? $"🗓 {item.PublishedAt}" : "";

        if (!string.IsNullOrWhiteSpace(item.FeaturedImageUrl))
            ImageUtils.LoadAsync(item.FeaturedImageUrl!, _imgFeatured, item.Id ?? item.FeaturedImageUrl!);
        else
            _imgFeatured.Image = null;
    }
}
