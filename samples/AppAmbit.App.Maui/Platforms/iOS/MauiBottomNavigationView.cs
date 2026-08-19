using CoreGraphics;
using UIKit;

namespace AppAmbitTestingApp;

internal sealed class MauiBottomNavigationView : UIView
{
    public const int BarHeight = 76;

    private static readonly (string Label, string Route, string Icon)[] Items =
    [
        ("Crashes", "MainPage", "exclamationmark.triangle"),
        ("Analytics", "AnalyticsPage", "chart.bar"),
        ("Config", "RemoteConfigPage", "slider.horizontal.3"),
        ("CMS", "CmsPage", "doc.text.magnifyingglass"),
        ("Data", "DatabasePage", "externaldrive"),
        ("Cloud", "CloudCodePage", "cloud")
    ];

    private readonly UIControl[] _navigationItems = new UIControl[Items.Length];
    private readonly UIView[] _selectionBackgrounds = new UIView[Items.Length];
    private readonly UIImageView[] _navigationIcons = new UIImageView[Items.Length];
    private readonly UILabel[] _navigationLabels = new UILabel[Items.Length];
    private int _selectedIndex;

    private MauiBottomNavigationView()
    {
        BackgroundColor = UIColor.FromRGB(248, 247, 252);
        TranslatesAutoresizingMaskIntoConstraints = false;
        AccessibilityViewIsModal = false;

        Layer.ShadowColor = UIColor.Black.CGColor;
        Layer.ShadowOpacity = 0.08f;
        Layer.ShadowOffset = new CGSize(0, -2);
        Layer.ShadowRadius = 6;

        var navigationStack = new UIStackView
        {
            Axis = UILayoutConstraintAxis.Horizontal,
            Alignment = UIStackViewAlignment.Fill,
            Distribution = UIStackViewDistribution.FillEqually,
            Spacing = 0,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        AddSubview(navigationStack);
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            navigationStack.TopAnchor.ConstraintEqualTo(TopAnchor, 4),
            navigationStack.LeadingAnchor.ConstraintEqualTo(LeadingAnchor, 4),
            navigationStack.TrailingAnchor.ConstraintEqualTo(TrailingAnchor, -4),
            navigationStack.BottomAnchor.ConstraintEqualTo(SafeAreaLayoutGuide.BottomAnchor, -4)
        });

        for (var index = 0; index < Items.Length; index++)
        {
            var item = CreateNavigationItem(index);
            _navigationItems[index] = item;
            navigationStack.AddArrangedSubview(item);
        }

        SelectItem(0);
    }

    public static MauiBottomNavigationView AttachTo(UIWindow window)
    {
        var navigation = new MauiBottomNavigationView();
        window.AddSubview(navigation);

        var safeArea = window.SafeAreaLayoutGuide;
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            navigation.TopAnchor.ConstraintEqualTo(safeArea.BottomAnchor, -BarHeight),
            navigation.LeadingAnchor.ConstraintEqualTo(window.LeadingAnchor),
            navigation.TrailingAnchor.ConstraintEqualTo(window.TrailingAnchor),
            navigation.BottomAnchor.ConstraintEqualTo(window.BottomAnchor)
        });

        return navigation;
    }

    private static UIImage? SysImg(string name)
    {
        if (OperatingSystem.IsIOSVersionAtLeast(13))
            return UIImage.GetSystemImage(name);

        return UIImage.FromBundle(name);
    }

    private UIControl CreateNavigationItem(int index)
    {
        var itemData = Items[index];
        var item = new UIControl
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            IsAccessibilityElement = true,
            AccessibilityLabel = itemData.Label,
            AccessibilityTraits = UIAccessibilityTrait.Button
        };

        var selectedBackground = new UIView
        {
            BackgroundColor = UIColor.FromRGB(233, 229, 255),
            UserInteractionEnabled = false,
            TranslatesAutoresizingMaskIntoConstraints = false,
            Hidden = true
        };
        selectedBackground.Layer.CornerRadius = 16;
        item.AddSubview(selectedBackground);

        var icon = new UIImageView(SysImg(itemData.Icon))
        {
            ContentMode = UIViewContentMode.ScaleAspectFit,
            TintColor = UIColor.FromRGB(102, 98, 116),
            TranslatesAutoresizingMaskIntoConstraints = false,
            UserInteractionEnabled = false
        };

        var label = new UILabel
        {
            Text = itemData.Label,
            Font = UIFont.SystemFontOfSize(10)!,
            TextColor = UIColor.FromRGB(102, 98, 116),
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
            _ = NavigateToAsync(index);
        };
        item.TouchUpOutside += (_, _) => item.Alpha = 1;
        item.TouchCancel += (_, _) => item.Alpha = 1;

        _selectionBackgrounds[index] = selectedBackground;
        _navigationIcons[index] = icon;
        _navigationLabels[index] = label;
        return item;
    }

    private async Task NavigateToAsync(int index)
    {
        try
        {
            await Shell.Current.GoToAsync($"//{Items[index].Route}");
            SelectItem(index);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] Could not navigate to {Items[index].Route}: {exception}");
        }
    }

    private void SelectItem(int index)
    {
        _selectedIndex = index;

        for (var itemIndex = 0; itemIndex < Items.Length; itemIndex++)
        {
            var isSelected = itemIndex == _selectedIndex;
            _selectionBackgrounds[itemIndex].Hidden = !isSelected;
            _navigationIcons[itemIndex].TintColor = isSelected
                ? UIColor.FromRGB(64, 49, 143)
                : UIColor.FromRGB(102, 98, 116);
            _navigationLabels[itemIndex].TextColor = isSelected
                ? UIColor.FromRGB(64, 49, 143)
                : UIColor.FromRGB(102, 98, 116);
            _navigationLabels[itemIndex].Font = isSelected
                ? UIFont.BoldSystemFontOfSize(10)!
                : UIFont.SystemFontOfSize(10)!;
            _navigationItems[itemIndex].AccessibilityTraits = isSelected
                ? UIAccessibilityTrait.Button | UIAccessibilityTrait.Selected
                : UIAccessibilityTrait.Button;
        }
    }
}
