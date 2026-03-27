using System;
using UIKit;

namespace AppAmbitTestingiOS;

public class MainTabBarController : UITabBarController
{
    static UIImage? SysImg(string name)
    {
        if (OperatingSystem.IsIOSVersionAtLeast(13))
            return UIImage.GetSystemImage(name);
        var fromBundle = UIImage.FromBundle(name);
        return fromBundle;
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        var crashes = new CrashesViewController();
        var analytics = new AnalyticsViewController();

        var imgCrashes = SysImg("exclamationmark.triangle");
        var imgAnalytics = SysImg("chart.bar");

        var crashesNav = new UINavigationController(crashes);
        var analyticsNav = new UINavigationController(analytics);

        crashesNav.TabBarItem = imgCrashes != null
            ? new UITabBarItem("Crashes", imgCrashes, 0)
            : new UITabBarItem("Crashes", null, 0);

        analyticsNav.TabBarItem = imgAnalytics != null
            ? new UITabBarItem("Analytics", imgAnalytics, 1)
            : new UITabBarItem("Analytics", null, 1);

        var config = new RemoteConfigViewController();
        var configNav = new UINavigationController(config);
        var imgConfig = SysImg("slider.horizontal.3");
        configNav.TabBarItem = imgConfig != null
             ? new UITabBarItem("Config", imgConfig, 2)
             : new UITabBarItem("Config", null, 2);

        var cms = new CmsViewController();
        var cmsNav = new UINavigationController(cms);
        var imgCms = SysImg("doc.text.magnifyingglass");
        cmsNav.TabBarItem = imgCms != null
             ? new UITabBarItem("CMS", imgCms, 3)
             : new UITabBarItem("CMS", null, 3);

        ViewControllers = new UIViewController[] { crashesNav, analyticsNav, configNav, cmsNav };
    }
}
