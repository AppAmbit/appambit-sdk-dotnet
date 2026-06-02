using System.Threading.Tasks;
using AppAmbit.PushNotifications;
using AppAmbitMaui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using static System.Linq.Enumerable;

namespace AppAmbitTestingApp;

public partial class MainPage : ContentPage
{
    private bool _inited;

    private bool _hasNotificationPermission;
    private bool _notificationsEnabled;
    private bool _isUpdatingPushButton;

    private string _logMessage = "Test Log Message";
    public string LogMessage
    {
        get => _logMessage;
        set
        {
            if (_logMessage != value)
            {
                _logMessage = value;
                OnPropertyChanged();
            }
        }
    }

    private string _userId = "";

    public string UserId
    {
        get => _userId;
        set
        {
            if (_userId != value)
            {
                _userId = value;
                OnPropertyChanged();
            }
        }
    }

    private string _userEmail = "test@gmail.com";

    public string UserEmail
    {
        get => _userEmail;
        set
        {
            if (_userEmail != value)
            {
                _userEmail = value;
                OnPropertyChanged();
            }
        }
    }

    public MainPage()
    {
        InitializeComponent();
        this.BindingContext = this;
        UserId = Guid.NewGuid().ToString();

        PushNotifications.SetForegroundListener(data =>
            Console.WriteLine($"[AppAmbitMaui] Foreground push: {data.Title}"));

        PushNotifications.SetOpenedListener(data =>
        {
            Console.WriteLine($"[AppAmbitMaui] Opened push: {data.Title}");
            MainThread.BeginInvokeOnMainThread(async () =>
                await Shell.Current.Navigation.PushModalAsync(new SecondPage()));
        });

        PushNotifications.Android.SetBackgroundListener(data =>
            Console.WriteLine($"[AppAmbitMaui] Background push: {data.Title}"));

        PushNotifications.Android.SetNotificationCustomizer(new SimpleNotificationCustomizer());
    }

    // The SDK already applies title/body/icon/color/sound/tag/ticker/sticky/visibility/
    // channelId/priority/image from the backend payload. This customizer demonstrates
    // ADDITIONAL changes on top — things the SDK does not do for you, like adding action
    // buttons, group keys, RemoteViews, or reading custom `data` keys.
    private sealed class SimpleNotificationCustomizer : PushNotifications.INotificationCustomizer
    {
        public void Customize(object context, object builder, PushNotificationData notification)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AppAmbitMaui][Customizer] title='{notification.Title}' " +
                $"ticker='{notification.Android?.Ticker}' sticky={notification.Android?.Sticky} " +
                $"visibility='{notification.Android?.Visibility}' priority='{notification.Android?.Priority}' " +
                $"channel='{notification.Android?.ChannelId}' clickAction='{notification.Android?.ClickAction}' " +
                $"data={{{string.Join(", ", notification.Data ?? new Dictionary<string, string>())}}}");

            dynamic b = builder;

            // Example additive change 1: append a marker the SDK didn't add.
            b.SetSubText("via AppAmbit");

            // Example additive change 2: read a custom `group_key` from the data payload
            // and group related notifications together — the SDK doesn't do grouping.
            if (notification.Data is { } data && data.TryGetValue("group_key", out var groupKey))
            {
                b.SetGroup(groupKey);
            }
        }
    }

    private async void OnGenerateLogsForBatch(object? sender, EventArgs e)
    {
        await DisplayAlert("Info", "Turn off internet", "Ok");
        foreach (int index in Range(1, 220))
        {
            await Crashes.LogError("Test Batch LogError");
        }
        await DisplayAlert("Info", "Logs generated", "Ok");
        await DisplayAlert("Info", "Turn on internet to send the logs", "Ok");
    }

    private async void OnHasCrashedTheLastSession(object? sender, EventArgs eventArgs)
    {
        if (await Crashes.DidCrashInLastSession())
        {
            await DisplayAlert("Info", "Application crashed in the last session", "Ok");
        }
        else
        {
            await DisplayAlert("Info", "Application did not crash in the last session", "Ok");
        }
    }

    private async void OnChangeUserId(object? sender, EventArgs e)
    {
        Analytics.SetUserId(UserId);
        await DisplayAlert("Info", "User id changed", "Ok");
    }

    private async void OnChangeUserEmail(object? sender, EventArgs e)
    {
        Analytics.SetUserEmail(UserEmail);
        await DisplayAlert("Info", "User email changed", "Ok");
    }

    private async void OnSendTestLog(object sender, EventArgs e)
    {
        await Crashes.LogError("Test Log Error", new Dictionary<string, string>() { { "user_id", "1" } });
        await DisplayAlert("Info", "LogError Sent", "Ok");
    }

    private async void OnSendTestException(object sender, EventArgs e)
    {
        try
        {
            throw new NullReferenceException();
        }
        catch (Exception exception)
        {
            await Crashes.LogError(exception, new Dictionary<string, string>() { { "user_id", "1" } });
            await DisplayAlert("Info", "LogError Sent", "Ok");
        }
    }

    private async void OnSendTestLogWithClassFQN(object sender, EventArgs e)
    {
        await Crashes.LogError("Test Log Error", new Dictionary<string, string>() { { "user_id", "1" } }, this.GetType().FullName);
        await DisplayAlert("Info", "LogError Sent", "Ok");
    }

    private async void OnGenerate30daysTestErrors(object sender, EventArgs e)
    {
        
    }


    private async void OnGenerate30daysTestCrash(object sender, EventArgs e)
    {
        
    }

    private void OnCounterClicked(object sender, EventArgs e)
    {
        throw new NullReferenceException();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshPushButtonAsync();
    }

    private async void OnPushNotificationsClicked(object? sender, EventArgs e)
    {
        await HandlePushNotificationsAsync();
    }

    private async Task RefreshPushButtonAsync()
    {
        if (DeviceInfo.Platform == DevicePlatform.iOS || DeviceInfo.Platform == DevicePlatform.Android)
        {
            ButtonPushNotifications.IsVisible = true;
            UpdateNotificationButtonState();
            
        }
        else
        {
            ButtonPushNotifications.IsVisible = false;
        }

        await Task.CompletedTask;
    }

    private async Task HandlePushNotificationsAsync()
    {
        if (_isUpdatingPushButton)
            return;

        _isUpdatingPushButton = true;
        try
        {
            _hasNotificationPermission = PushNotifications.HasNotificationPermission();

            if (!_hasNotificationPermission)
            {
                // Request permission
                PushNotifications.RequestNotificationPermission(new PermissionListener(granted => 
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        if (granted)
                        {
                            // Update state fields directly
                            _hasNotificationPermission = true;
                            _notificationsEnabled = true;
                            
                            // Once permission is granted, we enable notifications
                            PushNotifications.SetNotificationsEnabled(true);
                            
                            // Update button text directly
                            ButtonPushNotifications.Text = "Disable Notifications";
                            
                            await DisplayAlert("Notification Status", "Notifications have been enabled.", "OK");
                        }
                        else
                        {
                            UpdateNotificationButtonState();
                            await DisplayAlert("Permission Denied", "Notifications cannot be enabled without permission.", "OK");
                        }
                        
                        _isUpdatingPushButton = false;
                    });
                }));

                return;
            }

            // Toggle notifications enabled state - just flip the current local state
            var newState = !_notificationsEnabled;
            PushNotifications.SetNotificationsEnabled(newState);
            
            // Update the local state to match what we just set
            _notificationsEnabled = newState;
            
            // Update button text directly based on new state
            ButtonPushNotifications.Text = _notificationsEnabled ? "Disable Notifications" : "Enable Notifications";
            
            var message = $"Notifications have been {(newState ? "enabled" : "disabled")}.";
            await DisplayAlert("Notification Status", message, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            _isUpdatingPushButton = false;
        }
    }

    private void UpdateNotificationButtonState()
    {
        _hasNotificationPermission = PushNotifications.HasNotificationPermission();
        
        if (!_hasNotificationPermission)
        {
            ButtonPushNotifications.Text = "Allow Notifications";
            return;
        }
        
        _notificationsEnabled = PushNotifications.IsNotificationsEnabled();
        ButtonPushNotifications.Text = _notificationsEnabled ? "Disable Notifications" : "Enable Notifications";
    }

    private sealed class PermissionListener : PushNotifications.IPermissionListener
    {
        private readonly Action<bool> _onResult;

        public PermissionListener(Action<bool> onResult)
        {
            _onResult = onResult;
        }

        public void OnPermissionResult(bool isGranted) => _onResult(isGranted);
    }

    private async void OnTestErrorLogClicked(object sender, EventArgs e)
    {
        await Crashes.LogError(LogMessage);
        await DisplayAlert("Info", "LogError Sent", "Ok");
    }

    private async void OnGenerateTestCrash(object sender, EventArgs e)
    {
        await Crashes.GenerateTestCrash();
        await DisplayAlert("Info", "LogError Sent", "Ok");
    }

    private void MessageInputView_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        _logMessage = e.NewTextValue;
    }   
}
