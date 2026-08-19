namespace AppAmbitTestingApp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

#if ANDROID || IOS
        Navigated += OnNavigated;
#endif
    }

#if ANDROID || IOS
    private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        if (CurrentPage is Page page)
        {
            // The platform-specific custom bar supplies all six destinations, so
            // hide Shell's native tab bar (and its iOS "More" item).
            Shell.SetTabBarIsVisible(page, false);

#if ANDROID
            var bottomBarHeight = MauiBottomNavigationView.BarHeightDp;
#else
            var bottomBarHeight = MauiBottomNavigationView.BarHeight;
#endif

            page.Padding = new Thickness(
                page.Padding.Left,
                page.Padding.Top,
                page.Padding.Right,
                Math.Max(page.Padding.Bottom, bottomBarHeight));
        }
    }
#endif
}
