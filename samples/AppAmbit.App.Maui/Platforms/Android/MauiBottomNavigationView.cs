using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using Android.Views;
using Android.Widget;
using Microsoft.Maui.Controls;
using AndroidColor = Android.Graphics.Color;
using AndroidView = Android.Views.View;

namespace AppAmbitTestingApp;

internal sealed class MauiBottomNavigationView : LinearLayout
{
    public const int BarHeightDp = 76;
    private static readonly AndroidColor ActiveTextColor = AndroidColor.Rgb(64, 49, 143);
    private static readonly AndroidColor MutedTextColor = AndroidColor.Rgb(102, 98, 116);
    private static readonly AndroidColor ActiveSurfaceColor = AndroidColor.Rgb(233, 229, 255);
    private static readonly AndroidColor BarSurfaceColor = AndroidColor.Rgb(248, 247, 252);

    private static readonly (string Label, string Route, int Icon)[] Items =
    [
        ("Crashes", "MainPage", Resource.Drawable.bottom_nav_crashes),
        ("Analytics", "AnalyticsPage", Resource.Drawable.bottom_nav_analytics),
        ("Config", "RemoteConfigPage", Resource.Drawable.bottom_nav_config),
        ("CMS", "CmsPage", Resource.Drawable.bottom_nav_cms),
        ("Data", "DatabasePage", Resource.Drawable.bottom_nav_data),
        ("Cloud", "CloudCodePage", Resource.Drawable.bottom_nav_cloud)
    ];

    private readonly TextView[] _labels = new TextView[Items.Length];
    private readonly ImageView[] _icons = new ImageView[Items.Length];
    private int _selectedIndex;

    public MauiBottomNavigationView(Context context)
        : base(context)
    {
        Orientation = Orientation.Horizontal;
        SetGravity(GravityFlags.CenterVertical);
        SetBackgroundColor(BarSurfaceColor);
        Elevation = Dp(4);
        ImportantForAccessibility = ImportantForAccessibility.Yes;
        SetPadding(0, Dp(4), 0, Dp(4));

        for (var index = 0; index < Items.Length; index++)
        {
            AddNavigationItem(index);
        }

        SelectItem(0);
    }

    public static int HeightInDp(Context context) =>
        (int)(BarHeightDp * context.Resources!.DisplayMetrics!.Density + 0.5f);

    private void AddNavigationItem(int index)
    {
        var item = Items[index];
        var container = new LinearLayout(Context)
        {
            Orientation = Orientation.Vertical,
            ContentDescription = item.Label,
            Focusable = true,
            Clickable = true
        };
        container.SetGravity(GravityFlags.Center);
        container.SetPadding(Dp(2), Dp(2), Dp(2), Dp(2));
        container.LayoutParameters = new LinearLayout.LayoutParams(0, LayoutParams.MatchParent, 1);
        container.Click += (_, _) => _ = NavigateToAsync(index);

        var icon = new ImageView(Context);
        icon.SetImageResource(item.Icon);
        icon.LayoutParameters = new LinearLayout.LayoutParams(Dp(24), Dp(24));
        _icons[index] = icon;
        container.AddView(icon);

        var label = new TextView(Context)
        {
            Text = item.Label,
            Gravity = GravityFlags.Center,
        };
        label.SetIncludeFontPadding(false);
        label.SetTextSize(ComplexUnitType.Sp, 10);
        label.LayoutParameters = new LinearLayout.LayoutParams(LayoutParams.MatchParent, Dp(22));
        _labels[index] = label;
        container.AddView(label);

        AddView(container);
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
            var background = new GradientDrawable();
            background.SetColor(isSelected ? ActiveSurfaceColor : AndroidColor.Transparent);
            background.SetCornerRadius(Dp(12));

            if (GetChildAt(itemIndex) is not AndroidView container)
            {
                continue;
            }

            container.Background = background;
            _labels[itemIndex].SetTextColor(isSelected ? ActiveTextColor : MutedTextColor);
            _labels[itemIndex].SetTypeface(Typeface.Default, isSelected ? TypefaceStyle.Bold : TypefaceStyle.Normal);
            _icons[itemIndex].SetColorFilter(isSelected ? ActiveTextColor : MutedTextColor, PorterDuff.Mode.SrcIn);
        }
    }

    private int Dp(int value) => (int)(value * Resources!.DisplayMetrics!.Density + 0.5f);
}
