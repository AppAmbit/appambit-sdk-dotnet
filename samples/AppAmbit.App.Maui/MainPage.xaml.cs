using System.Threading.Tasks;
#if ANDROID
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Activity;
using AndroidX.Core.Content;
using AppAmbit.PushNotifications;
#endif
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
#if ANDROID
        await RefreshPushButtonAsync();
#else
        ButtonPushNotifications.IsVisible = false;
#endif
    }

    private async void OnPushNotificationsClicked(object? sender, EventArgs e)
    {
#if ANDROID
        await HandlePushNotificationsAsync();
#else
        await DisplayAlert("Info", "Push notifications are available only on Android in this sample.", "OK");
#endif
    }

#if ANDROID
    private async Task RefreshPushButtonAsync()
    {
        ButtonPushNotifications.IsVisible = true;

        var context = Platform.AppContext;
        if (context == null)
            return;

        _hasNotificationPermission = HasSystemPermission(context);
        _notificationsEnabled = PushNotifications.IsNotificationsEnabled(context);
        UpdatePushButtonText();
    }

    private async Task HandlePushNotificationsAsync()
    {
        if (_isUpdatingPushButton)
            return;

        _isUpdatingPushButton = true;
        try
        {
            var context = Platform.AppContext;
            if (context == null)
            {
                await DisplayAlert("Error", "Unable to get application context.", "OK");
                return;
            }

            if (!_hasNotificationPermission)
            {
                if (Platform.CurrentActivity is not ComponentActivity activity)
                {
                    await DisplayAlert("Error", "Unable to get current activity to request permission.", "OK");
                    return;
                }

                var tcs = new TaskCompletionSource<bool>();
                PushNotifications.RequestNotificationPermission(activity, new PermissionListener(granted => tcs.TrySetResult(granted)));

                var granted = await tcs.Task;
                if (granted)
                {
                    PushNotifications.SetNotificationsEnabled(context, true);
                    _hasNotificationPermission = true;
                    _notificationsEnabled = true;
                    UpdatePushButtonText();
                    await DisplayAlert("Done", "Notifications enabled", "OK");
                }
                else
                {
                    await DisplayAlert("Permission denied", "Notification permission denied by the user.", "OK");
                }

                return;
            }

            var targetEnabled = !_notificationsEnabled;
            PushNotifications.SetNotificationsEnabled(context, targetEnabled);
            _notificationsEnabled = targetEnabled;
            UpdatePushButtonText();
            await DisplayAlert("Done", targetEnabled ? "Notifications enabled" : "Notifications disabled", "OK");
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
#else
    private Task RefreshPushButtonAsync()
    {
        ButtonPushNotifications.IsVisible = false;
        return Task.CompletedTask;
    }

    private async Task HandlePushNotificationsAsync()
    {
        ButtonPushNotifications.IsVisible = false;
        await DisplayAlert("Info", "Push notifications are available only on Android in this sample.", "OK");
    }
#endif

    private void UpdatePushButtonText()
    {
        ButtonPushNotifications.Text = !_hasNotificationPermission
            ? "Allow Notifications"
            : _notificationsEnabled
                ? "Disable Notifications"
                : "Enable Notifications";
    }

#if ANDROID
    private static bool HasSystemPermission(Context context)
    {
        if ((int)Build.VERSION.SdkInt < 33)
            return true;

        return ContextCompat.CheckSelfPermission(context, Android.Manifest.Permission.PostNotifications) == Permission.Granted;
    }

    private sealed class PermissionListener : Java.Lang.Object, PushNotifications.IPermissionListener
    {
        private readonly Action<bool> _onResult;

        public PermissionListener(Action<bool> onResult)
        {
            _onResult = onResult;
        }

        public void OnPermissionResult(bool isGranted) => _onResult(isGranted);
    }
#endif

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
