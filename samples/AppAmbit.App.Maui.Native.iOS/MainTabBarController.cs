using CoreGraphics;
using UIKit;

namespace AppAmbitTestingiOS;

public class MainTabBarController : UITabBarController
{
    private const int BottomBarContentHeight = 76;

    private UIControl[] _navigationItems = Array.Empty<UIControl>();
    private UIView[] _selectionBackgrounds = Array.Empty<UIView>();
    private UIImageView[] _navigationIcons = Array.Empty<UIImageView>();
    private UILabel[] _navigationLabels = Array.Empty<UILabel>();

    private static UIImage? SysImg(string name)
    {
        if (OperatingSystem.IsIOSVersionAtLeast(13))
            return UIImage.GetSystemImage(name);

        return UIImage.FromBundle(name);
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        // UITabBarController moves six items into a "More" tab. The custom bar
        // keeps every destination visible while retaining the existing tab host.
        TabBar.Hidden = true;
        TabBar.UserInteractionEnabled = false;

        var crashesNav = CreateNavigationController(
            new CrashesViewController(), "Crashes", "exclamationmark.triangle", 0);
        var analyticsNav = CreateNavigationController(
            new AnalyticsViewController(), "Analytics", "chart.bar", 1);
        var configNav = CreateNavigationController(
            new RemoteConfigViewController(), "Config", "slider.horizontal.3", 2);
        var cmsNav = CreateNavigationController(
            new CmsViewController(), "CMS", "doc.text.magnifyingglass", 3);
        var databaseNav = CreateNavigationController(
            new DatabaseViewController(), "DB", "externaldrive", 4);
        var cloudCodeNav = CreateNavigationController(
            new CloudCodeViewController(), "Cloud", "cloud", 5);

        var navigationControllers = new[]
        {
            crashesNav,
            analyticsNav,
            configNav,
            cmsNav,
            databaseNav,
            cloudCodeNav
        };

        ViewControllers = navigationControllers;

        // The custom bar is outside the child navigation controllers, so keep
        // scroll views and forms above it on every tab.
        foreach (var navigationController in navigationControllers)
            navigationController.AdditionalSafeAreaInsets = new UIEdgeInsets(0, 0, BottomBarContentHeight, 0);

        BuildBottomNavigation();
        SelectTab(0);
    }

    private static UINavigationController CreateNavigationController(
        UIViewController viewController,
        string title,
        string imageName,
        nint index)
    {
        viewController.Title = title;
        var navigationController = new UINavigationController(viewController);
        var image = SysImg(imageName);
        navigationController.TabBarItem = image != null
            ? new UITabBarItem(title, image, index)
            : new UITabBarItem(title, null, index);
        return navigationController;
    }

    private void BuildBottomNavigation()
    {
        var bottomBar = new UIView
        {
            BackgroundColor = NativeTheme.NavSurface,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        bottomBar.Layer.ShadowColor = UIColor.Black.CGColor;
        bottomBar.Layer.ShadowOpacity = 0.08f;
        bottomBar.Layer.ShadowOffset = new CGSize(0, -2);
        bottomBar.Layer.ShadowRadius = 6;

        var navigationStack = new UIStackView
        {
            Axis = UILayoutConstraintAxis.Horizontal,
            Alignment = UIStackViewAlignment.Fill,
            Distribution = UIStackViewDistribution.FillEqually,
            Spacing = 0,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        bottomBar.AddSubview(navigationStack);
        View.AddSubview(bottomBar);

        var safeArea = View.SafeAreaLayoutGuide;
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            bottomBar.TopAnchor.ConstraintEqualTo(safeArea.BottomAnchor, -BottomBarContentHeight),
            bottomBar.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
            bottomBar.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
            bottomBar.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),

            navigationStack.TopAnchor.ConstraintEqualTo(bottomBar.TopAnchor, 4),
            navigationStack.LeadingAnchor.ConstraintEqualTo(bottomBar.LeadingAnchor, 4),
            navigationStack.TrailingAnchor.ConstraintEqualTo(bottomBar.TrailingAnchor, -4),
            navigationStack.BottomAnchor.ConstraintEqualTo(safeArea.BottomAnchor, -4)
        });

        var labels = new[] { "Crashes", "Analytics", "Config", "CMS", "DB", "Cloud" };
        var imageNames = new[]
        {
            "exclamationmark.triangle",
            "chart.bar",
            "slider.horizontal.3",
            "doc.text.magnifyingglass",
            "externaldrive",
            "cloud"
        };

        _navigationItems = new UIControl[labels.Length];
        _selectionBackgrounds = new UIView[labels.Length];
        _navigationIcons = new UIImageView[labels.Length];
        _navigationLabels = new UILabel[labels.Length];

        for (var index = 0; index < labels.Length; index++)
        {
            var item = CreateNavigationItem(index, labels[index], SysImg(imageNames[index]));
            _navigationItems[index] = item;
            navigationStack.AddArrangedSubview(item);
        }
    }

    private UIControl CreateNavigationItem(int index, string labelText, UIImage? image)
    {
        var item = new UIControl
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            IsAccessibilityElement = true,
            AccessibilityLabel = labelText,
            AccessibilityTraits = UIAccessibilityTrait.Button
        };

        var selectedBackground = new UIView
        {
            BackgroundColor = NativeTheme.NavActive,
            UserInteractionEnabled = false,
            TranslatesAutoresizingMaskIntoConstraints = false,
            Hidden = true
        };
        selectedBackground.Layer.CornerRadius = 16;
        item.AddSubview(selectedBackground);

        var icon = new UIImageView(image)
        {
            ContentMode = UIViewContentMode.ScaleAspectFit,
            TintColor = NativeTheme.NavMutedText,
            TranslatesAutoresizingMaskIntoConstraints = false,
            UserInteractionEnabled = false
        };

        var label = new UILabel
        {
            Text = labelText,
            Font = UIFont.SystemFontOfSize(10),
            TextColor = NativeTheme.NavMutedText,
            Lines = 1,
            AdjustsFontSizeToFitWidth = true,
            MinimumScaleFactor = 0.7f,
            TextAlignment = UITextAlignment.Center,
            TranslatesAutoresizingMaskIntoConstraints = false,
            UserInteractionEnabled = false
        };

        var contentStack = new UIStackView(new UIView[] { icon, label })
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Alignment = UIStackViewAlignment.Center,
            Distribution = UIStackViewDistribution.Fill,
            Spacing = 2,
            TranslatesAutoresizingMaskIntoConstraints = false,
            UserInteractionEnabled = false
        };

        item.AddSubview(contentStack);

        NSLayoutConstraint.ActivateConstraints(new[]
        {
            selectedBackground.TopAnchor.ConstraintEqualTo(item.TopAnchor, 4),
            selectedBackground.LeadingAnchor.ConstraintEqualTo(item.LeadingAnchor, 4),
            selectedBackground.TrailingAnchor.ConstraintEqualTo(item.TrailingAnchor, -4),
            selectedBackground.BottomAnchor.ConstraintEqualTo(item.BottomAnchor, -4),

            contentStack.TopAnchor.ConstraintEqualTo(item.TopAnchor, 4),
            contentStack.LeadingAnchor.ConstraintEqualTo(item.LeadingAnchor, 2),
            contentStack.TrailingAnchor.ConstraintEqualTo(item.TrailingAnchor, -2),
            contentStack.BottomAnchor.ConstraintEqualTo(item.BottomAnchor, -4),

            icon.WidthAnchor.ConstraintEqualTo(20),
            icon.HeightAnchor.ConstraintEqualTo(22),
            label.HeightAnchor.ConstraintEqualTo(15)
        });

        item.TouchDown += (_, _) => item.Alpha = 0.72f;
        item.TouchUpInside += (_, _) =>
        {
            item.Alpha = 1;
            SelectTab(index);
        };
        item.TouchUpOutside += (_, _) => item.Alpha = 1;
        item.TouchCancel += (_, _) => item.Alpha = 1;

        _selectionBackgrounds[index] = selectedBackground;
        _navigationIcons[index] = icon;
        _navigationLabels[index] = label;
        return item;
    }

    private void SelectTab(int index)
    {
        if (index < 0 || index >= _navigationItems.Length)
            return;

        SelectedIndex = index;

        for (var itemIndex = 0; itemIndex < _navigationItems.Length; itemIndex++)
        {
            var isSelected = itemIndex == index;
            _selectionBackgrounds[itemIndex].Hidden = !isSelected;
            _navigationIcons[itemIndex].TintColor = isSelected
                ? NativeTheme.NavActiveText
                : NativeTheme.NavMutedText;
            _navigationLabels[itemIndex].TextColor = isSelected
                ? NativeTheme.NavActiveText
                : NativeTheme.NavMutedText;
            _navigationLabels[itemIndex].Font = isSelected
                ? UIFont.BoldSystemFontOfSize(10)
                : UIFont.SystemFontOfSize(10);
            _navigationItems[itemIndex].AccessibilityTraits = isSelected
                ? UIAccessibilityTrait.Button | UIAccessibilityTrait.Selected
                : UIAccessibilityTrait.Button;
        }
    }
}
