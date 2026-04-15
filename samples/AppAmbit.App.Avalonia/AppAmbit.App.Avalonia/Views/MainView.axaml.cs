using System;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Collections.Generic;
using AppAmbitAvalonia;
using System.Threading.Tasks;
using System.Linq;

namespace AppAmbitTestingAppAvalonia.Views;

public partial class MainView : UserControl
{
    private bool _hasNotificationPermission;
    private bool _notificationsEnabled;
    private bool _isUpdatingPushButton;

    public MainView()
    {
        InitializeComponent();

        try
        {
            
        txtChangeUserId.Text = Guid.NewGuid().ToString();
        txtChangeUserEmail.Text = "test@gmail.com";
        txtCustomLogError.Text = "Test Log Message";

        // Configure notification customizer
        AppAmbit.PushNotifications.PushNotifications.SetNotificationCustomizer(new SimpleNotificationCustomizer());

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
        UpdateNotificationButtonState();
    }

    private void OnNavAnalyticsClicked(object? sender, RoutedEventArgs e)
    {
        CrashesPanel.IsVisible = false;
        AnalyticsPanel.IsVisible = true;
        RemoteConfigPanel.IsVisible = false;
    }

    private void OnNavRemoteConfigClicked(object? sender, RoutedEventArgs e)
    {
        CrashesPanel.IsVisible = false;
        AnalyticsPanel.IsVisible = false;
        RemoteConfigPanel.IsVisible = true;
        CmsPanel.IsVisible = false;

        UpdateRemoteConfigUI();
    }

    private void OnNavCmsClicked(object? sender, RoutedEventArgs e)
    {
        CrashesPanel.IsVisible = false;
        AnalyticsPanel.IsVisible = false;
        RemoteConfigPanel.IsVisible = false;
        CmsPanel.IsVisible = true;
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
            _hasNotificationPermission = AppAmbit.PushNotifications.PushNotifications.HasSystemPermission();
            Console.WriteLine($"[AppAmbit][Debug] HasSystemPermission={_hasNotificationPermission}");

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
        _hasNotificationPermission = AppAmbit.PushNotifications.PushNotifications.HasSystemPermission();
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

    private sealed class SimpleNotificationCustomizer : AppAmbit.PushNotifications.PushNotifications.INotificationCustomizer
    {
        public void Customize(object context, object builder, AppAmbit.PushNotifications.PushNotificationData notification)
        {
            System.Diagnostics.Debug.WriteLine($"[Customizer] Title: {notification.Title}, Body: {notification.Body}");

            if (notification.Data is System.Collections.IDictionary dict)
            {
                foreach (var key in dict.Keys)
                {
                    System.Diagnostics.Debug.WriteLine($"[Customizer] Data[{key}] = {dict[key]}");
                }
            }
            else if (notification.Data != null)
            {
                System.Diagnostics.Debug.WriteLine($"[Customizer] Data (raw): {notification.Data}");
            }
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
        var logsTask = Enumerable.Range(0, 5).Select(i => Task.Run(async () =>
        {
            await Crashes.LogError("Sending 5 errors after an invalid token");
        }));

        var eventsTask = Enumerable.Range(0, 5).Select(i => Task.Run(async () =>
        {
            await Analytics.TrackEvent("Sending 5 events after an invalid token",
                new Dictionary<string, string>
                {{"Test Token", "5 events sent"}});
        }));
        await Task.WhenAll(logsTask);
        Analytics.ClearToken();
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