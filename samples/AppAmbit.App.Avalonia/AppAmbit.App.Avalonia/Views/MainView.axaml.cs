using System;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Collections.Generic;
using AppAmbitAvalonia;
using AppAmbit.PushNotifications;
using System.Threading.Tasks;
using System.Linq;

namespace AppAmbitTestingAppAvalonia.Views;

public partial class MainView : UserControl
{
    private readonly bool _isMobile;
    private bool _hasNotificationPermission;
    private bool _notificationsEnabled;
    private bool _isUpdatingPushButton;

    public MainView()
    {
        InitializeComponent();
        _isMobile = OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

        if (_isMobile)
        {
            Classes.Add("mobile-sample");
            MobileTitleBar.IsVisible = true;
            ConfigureMobileNavigation();
            SetMobileSection("Crashes", NavCrashes);
        }

        try
        {
            PushNotifications.SetForegroundListener(data =>
                Console.WriteLine($"[AppAmbitAvalonia][Foreground] {data.Title} — {data.Body}"));

            PushNotifications.SetOpenedListener(data =>
            {
                Console.WriteLine($"[AppAmbitAvalonia][Opened] {data.Title} — {data.Body}");
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var root = this.GetVisualRoot();
                    object previousContent = this;
                    if (root is Window w)
                    {
                        previousContent = w.Content ?? this;
                        w.Content = new SecondView(previousContent);
                        return;
                    }
                    if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime sv)
                        sv.MainView = new SecondView(previousContent);
                });
            });

            PushNotifications.Android.SetBackgroundListener(data =>
                Console.WriteLine($"[AppAmbitAvalonia][Background] {data.Title} — {data.Body}"));

            PushNotifications.Android.SetNotificationCustomizer(new SimpleNotificationCustomizer());

        txtChangeUserId.Text = Guid.NewGuid().ToString();
        txtChangeUserEmail.Text = "test@gmail.com";
        txtCustomLogError.Text = "Test Log Message";

        // Initial state update
        UpdateNotificationButtonState();

        // Refresh state when view appears (important for Resume)
        AttachedToVisualTree += (s, e) => UpdateNotificationButtonState();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppAmbit] Exception in MainView constructor: {ex}");
        }
    }

    private void OnNavCrashesClicked(object? sender, RoutedEventArgs e)
    {
        CrashesPanel.IsVisible = true;
        AnalyticsPanel.IsVisible = false;
        RemoteConfigPanel.IsVisible = false;
        CmsPanel.IsVisible = false;
        DatabasePanel.IsVisible = false;
        CloudCodePanel.IsVisible = false;
        UpdateNotificationButtonState();
        SetMobileSection("Crashes", NavCrashes);
    }

    private void OnNavAnalyticsClicked(object? sender, RoutedEventArgs e)
    {
        CrashesPanel.IsVisible = false;
        AnalyticsPanel.IsVisible = true;
        RemoteConfigPanel.IsVisible = false;
        CmsPanel.IsVisible = false;
        DatabasePanel.IsVisible = false;
        CloudCodePanel.IsVisible = false;
        SetMobileSection("Analytics", NavAnalytics);
    }

    private void OnNavRemoteConfigClicked(object? sender, RoutedEventArgs e)
    {
        CrashesPanel.IsVisible = false;
        AnalyticsPanel.IsVisible = false;
        RemoteConfigPanel.IsVisible = true;
        CmsPanel.IsVisible = false;
        DatabasePanel.IsVisible = false;
        CloudCodePanel.IsVisible = false;

        UpdateRemoteConfigUI();
        SetMobileSection("Config", NavRemoteConfig);
    }

    private void OnNavCmsClicked(object? sender, RoutedEventArgs e)
    {
        CrashesPanel.IsVisible = false;
        AnalyticsPanel.IsVisible = false;
        RemoteConfigPanel.IsVisible = false;
        CmsPanel.IsVisible = true;
        DatabasePanel.IsVisible = false;
        CloudCodePanel.IsVisible = false;
        SetMobileSection("CMS", NavCms);
    }

    private void OnNavDatabaseClicked(object? sender, RoutedEventArgs e)
    {
        CrashesPanel.IsVisible = false;
        AnalyticsPanel.IsVisible = false;
        RemoteConfigPanel.IsVisible = false;
        CmsPanel.IsVisible = false;
        DatabasePanel.IsVisible = true;
        CloudCodePanel.IsVisible = false;
        SetMobileSection("Data", NavDatabase);
    }

    private void OnNavCloudCodeClicked(object? sender, RoutedEventArgs e)
    {
        CrashesPanel.IsVisible = false;
        AnalyticsPanel.IsVisible = false;
        RemoteConfigPanel.IsVisible = false;
        CmsPanel.IsVisible = false;
        DatabasePanel.IsVisible = false;
        CloudCodePanel.IsVisible = true;
        SetMobileSection("Cloud Code", NavCloudCode);
    }

    private void SetMobileSection(string title, Avalonia.Controls.Button activeButton)
    {
        if (!_isMobile)
            return;

        MobileScreenTitle.Text = title;
        SetActiveNavigationButton(activeButton);
    }

    private void SetActiveNavigationButton(Avalonia.Controls.Button activeButton)
    {
        foreach (var button in new[] { NavCrashes, NavAnalytics, NavRemoteConfig, NavCms, NavDatabase, NavCloudCode })
            button.Classes.Remove("active");

        activeButton.Classes.Add("active");
    }

    private void ConfigureMobileNavigation()
    {
        ConfigureNavigationButton(NavCrashes, "Crashes", "M12,2 L22,20 L2,20 Z M11,8 L13,8 L13,14 L11,14 Z M11,16 L13,16 L13,18 L11,18 Z");
        ConfigureNavigationButton(NavAnalytics, "Analytics", "M4,19 L20,19 L20,21 L2,21 L2,3 L4,3 Z M6,15 L9,12 L11,14 L16,8 L18,10 L11,18 L9,16 L6,19 Z");
        ConfigureNavigationButton(NavRemoteConfig, "Config", "M3,6 L10,6 L10,8 L3,8 Z M14,6 L21,6 L21,8 L14,8 Z M11,4 L13,4 L13,10 L11,10 Z M3,11 L6,11 L6,13 L3,13 Z M10,11 L21,11 L21,13 L10,13 Z M7,9 L9,9 L9,15 L7,15 Z M3,16 L14,16 L14,18 L3,18 Z M18,16 L21,16 L21,18 L18,18 Z M15,14 L17,14 L17,20 L15,20 Z");
        ConfigureNavigationButton(NavCms, "CMS", "M6,2 L15,2 L20,7 L20,22 L6,22 Z M14,4 L14,9 L18,9 Z M9,12 L17,12 L17,14 L9,14 Z M9,16 L17,16 L17,18 L9,18 Z");
        ConfigureNavigationButton(NavDatabase, "Data", "M12,3 C6,3 3,5 3,7 L3,17 C3,20 7,21 12,21 C17,21 21,20 21,17 L21,7 C21,5 18,3 12,3 Z M5,7 C5,6 8,5 12,5 C16,5 19,6 19,7 C19,8 16,9 12,9 C8,9 5,8 5,7 Z M19,12 C19,13 16,14 12,14 C8,14 5,13 5,12 L5,10 C7,11 9,11 12,11 C15,11 17,11 19,10 Z");
        ConfigureNavigationButton(NavCloudCode, "Cloud", "M7,19 C4,19 2,17 2,14 C2,11 4,9 7,9 C8,5 11,3 15,3 C19,3 22,6 22,10 C22,10 22,10 22,11 C24,11 25,13 25,15 C25,17 23,19 20,19 Z");
    }

    private static void ConfigureNavigationButton(Avalonia.Controls.Button button, string label, string path)
    {
        var icon = new PathIcon
        {
            Data = StreamGeometry.Parse(path),
            Width = 18,
            Height = 18,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        var text = new TextBlock
        {
            Text = label,
            FontSize = 10,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        button.Content = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
            Spacing = 2,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Children = { icon, text }
        };
    }

    private void UpdateRemoteConfigUI()
    {
        bool showBanner = RemoteConfig.GetBoolean("banner");
        string dataText = RemoteConfig.GetString("data");
        long discount = RemoteConfig.GetLong("discount");

        BannerView.IsVisible = showBanner;
        DataLabel.Text = dataText;
        DiscountLabel.Text = $"{discount}% OFF";
    }

    private async void OnDidCrashClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var didCrash = await Crashes.DidCrashInLastSession();
            var message = didCrash ? "Application did crash in the last session" : "Application did not crash in the last session.";

            await AlertWindow.ShowAlert(message);
        }
        catch (Exception) { }
    }

    private async void OnChangeUserIdClicked(object? sender, RoutedEventArgs e)
    {
        var text = txtChangeUserId?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            Analytics.SetUserId(text);
        }
        await AlertWindow.ShowAlert("User ID changed");
    }

    private async void OnChangeUserEmailClicked(object? sender, RoutedEventArgs e)
    {
        var text = txtChangeUserEmail?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            Analytics.SetUserEmail(text);
        }
        else
        {
            Analytics.SetUserEmail("test@gmail.com");
        }
        await AlertWindow.ShowAlert("User email changed");
    }

    private async void OnCustomLogErrorClicked(object? sender, RoutedEventArgs e)
    {
        var text = txtCustomLogError?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            Crashes.LogError(text);
        }
        else
        {
            Crashes.LogError("Test Log Message");
        }
        await AlertWindow.ShowAlert("LogError Custom sent");
    }

    private async void OnDefaultLogErrorClicked(object? sender, RoutedEventArgs e)
    {
        Crashes.LogError("Test Log Error", new Dictionary<string, string>() { { "user_id", "1" } });
        await AlertWindow.ShowAlert("LogError Default sent");
    }

    private async void OnSendExceptionLogErrorClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            throw new InvalidOperationException();
        }
        catch (Exception ex)
        {
            Crashes.LogError(ex);
        }
        await AlertWindow.ShowAlert("LogError Exception sent");
    }

    private async void OnThrowNewCrashClicked(object? sender, RoutedEventArgs e)
    {
        throw new NullReferenceException();
    }

    private async void OnGenerateCrashClicked(object? sender, RoutedEventArgs e)
    {
        await Crashes.GenerateTestCrash();
    }

    private async void OnPushNotificationsClicked(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingPushButton)
            return;

        _isUpdatingPushButton = true;
        Console.WriteLine($"[AppAmbit][Debug] OnPushNotificationsClicked start _isUpdatingPushButton={_isUpdatingPushButton} _notificationsEnabled={_notificationsEnabled}");
        try
        {
            _hasNotificationPermission = AppAmbit.PushNotifications.PushNotifications.HasNotificationPermission();
            Console.WriteLine($"[AppAmbit][Debug] HasNotificationPermission={_hasNotificationPermission}");

            if (!_hasNotificationPermission)
            {
                // Request permission
                AppAmbit.PushNotifications.PushNotifications.RequestNotificationPermission(new PermissionListener(granted =>
                {
                    // Ensure we run on UI Thread
                    Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                    {
                        Console.WriteLine($"[AppAmbit][Debug] Permission Callback: granted={granted}");

                        if (granted)
                        {
                            // SUCCESS: Force UI update immediately
                            _hasNotificationPermission = true;
                            _notificationsEnabled = true;
                            btnPushNotifications.Content = "Disable Notifications";

                            // Tell SDK
                            AppAmbit.PushNotifications.PushNotifications.SetNotificationsEnabled(true);

                            await AlertWindow.ShowAlert("Notifications have been enabled.");
                        }
                        else
                        {
                            // FAILURE: Check state
                            UpdateNotificationButtonState();
                            await AlertWindow.ShowAlert("Permission Denied or Dialog Cancelled.");
                        }

                        _isUpdatingPushButton = false;
                    });
                }));

                return;
            }

            // Toggle notifications enabled state using local cached value (keep in sync with MAUI behavior)
            _notificationsEnabled = !_notificationsEnabled;
            Console.WriteLine($"[AppAmbit][Debug] Toggling notifications -> new value = {_notificationsEnabled}");
            AppAmbit.PushNotifications.PushNotifications.SetNotificationsEnabled(_notificationsEnabled);

            btnPushNotifications.Content = _notificationsEnabled ? "Disable Notifications" : "Enable Notifications";

            var message = $"Notifications have been {(_notificationsEnabled ? "enabled" : "disabled")}.";
            await AlertWindow.ShowAlert(message);
        }
        catch (Exception ex)
        {
            await AlertWindow.ShowAlert($"Error: {ex.Message}");
        }
        finally
        {
            _isUpdatingPushButton = false;
            Console.WriteLine($"[AppAmbit][Debug] OnPushNotificationsClicked end _isUpdatingPushButton={_isUpdatingPushButton} _notificationsEnabled={_notificationsEnabled}");
        }
    }

    private void UpdateNotificationButtonState()
    {
        _hasNotificationPermission = AppAmbit.PushNotifications.PushNotifications.HasNotificationPermission();
        Console.WriteLine($"[AppAmbit][Debug] UpdateNotificationButtonState hasPermission={_hasNotificationPermission}");

        if (!_hasNotificationPermission)
        {
            btnPushNotifications.Content = "Allow Notifications";
            return;
        }

        _notificationsEnabled = AppAmbit.PushNotifications.PushNotifications.IsNotificationsEnabled();
        Console.WriteLine($"[AppAmbit][Debug] UpdateNotificationButtonState native IsNotificationsEnabled={_notificationsEnabled}");
        btnPushNotifications.Content = _notificationsEnabled ? "Disable Notifications" : "Enable Notifications";
    }

    private sealed class PermissionListener : AppAmbit.PushNotifications.PushNotifications.IPermissionListener
    {
        private readonly Action<bool> _onResult;

        public PermissionListener(Action<bool> onResult)
        {
            _onResult = onResult;
        }

        public void OnPermissionResult(bool isGranted) => _onResult(isGranted);
    }

    private sealed class SimpleNotificationCustomizer : PushNotifications.INotificationCustomizer
    {
        public void Customize(object context, object builder, PushNotificationData notification)
        {
            Console.WriteLine($"[AppAmbitAvalonia][Customizer] {notification.Title}");
            dynamic b = builder;
            b.SetContentTitle($"Custom {notification.Title}");
        }
    }

    private async void OnSessionStartClicked(object? sender, RoutedEventArgs e)
    {
        await Analytics.StartSession();
        await AlertWindow.ShowAlert("Session started");
    }

    private async void OnSessionEndClicked(object? sender, RoutedEventArgs e)
    {
        await Analytics.EndSession();
    }

    private async void OnInvalidateTokenClicked(object? sender, RoutedEventArgs e)
    {
        Analytics.ClearToken();
    }

    private async void OnTokenRefreshTestClicked(object? sender, RoutedEventArgs e)
    {
        Analytics.ClearToken();
        var logsTask = Enumerable.Range(0, 5).Select(_ =>
            Crashes.LogError("Sending 5 errors after an invalid token"));

        await Task.WhenAll(logsTask);
        Analytics.ClearToken();

        var eventsTask = Enumerable.Range(0, 5).Select(_ =>
            Analytics.TrackEvent("Sending 5 events after an invalid token",
                new Dictionary<string, string>
                {{"Test Token", "5 events sent"}}));

        await Task.WhenAll(eventsTask);
        await AlertWindow.ShowAlert("5 events and errors sent");
    }

    private async void OnSendButtonClickedClicked(object? sender, RoutedEventArgs e)
    {
        await Analytics.TrackEvent("ButtonClicked", new Dictionary<string, string> { { "Count", "41" } });
    }

    private async void OnSendDefaultEventClicked(object? sender, RoutedEventArgs e)
    {
        await Analytics.GenerateTestEvent();
    }

    private async void OnSendMax300LengthEventClicked(object? sender, RoutedEventArgs e)
    {
        //300 characters:
        var _300Characters = "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890";
        var _300Characters2 = "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678902";
        var properties = new Dictionary<string, string>
        {
            { _300Characters, _300Characters },
            { _300Characters2, _300Characters2 }
        };
        await Analytics.TrackEvent(_300Characters, properties);
    }

    private async void OnSendMax20PropertiesEventClicked(object? sender, RoutedEventArgs e)
    {
        var properties = new Dictionary<string, string>
        {
            { "01", "01"},
            { "02", "02"},
            { "03", "03"},
            { "04", "04"},
            { "05", "05"},
            { "06", "06"},
            { "07", "07"},
            { "08", "08"},
            { "09", "09"},
            { "10", "10"},
            { "11", "11"},
            { "12", "12"},
            { "13", "13"},
            { "14", "14"},
            { "15", "15"},
            { "16", "16"},
            { "17", "17"},
            { "18", "18"},
            { "19", "19"},
            { "20", "20"},
            { "21", "21"},
            { "22", "22"},
            { "23", "23"},
            { "24", "24"},
            { "25", "25"},//25
        };
        await Analytics.TrackEvent("TestMaxProperties", properties);
    }

    private async void OnSend220EventsClicked(object? sender, RoutedEventArgs e)
    {
        foreach (int _ in Enumerable.Range(1, 220))
        {
            await Analytics.TrackEvent("Test Batch TrackEvent", new Dictionary<string, string> { { "test1", "test1" } });
        }
        await AlertWindow.ShowAlert("220 events sent");
    }

    private async void OnChangeSecondActivityClicked(object? sender, RoutedEventArgs e)
    {
        var root = this.GetVisualRoot();
        object previousContent = this;
        if (root is Window w)
        {
            previousContent = w.Content ?? this;
            var second = new SecondView(previousContent);
            w.Content = second;
            return;
        }
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime singleView)
            {
                var second = new SecondView(previousContent);
                singleView.MainView = second;
            }
        }
        catch { }
    }

}
