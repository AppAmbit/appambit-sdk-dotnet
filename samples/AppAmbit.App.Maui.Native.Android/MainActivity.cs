using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Views;
using Android.Widget;
using AppAmbit.PushNotifications;
using AppAmbitMaui;
using AppAmbitTestingAppAndroid.Models;
using AppAmbitTestingAppAndroid.Adapters;

namespace AppAmbitTestingAppAndroid;

[Activity(Label = "@string/app_name", MainLauncher = true, Theme = "@android:style/Theme.Material.Light.NoActionBar")]
public class MainActivity : Activity
{
    FrameLayout? _container;
    View? _viewCrashes;
    View? _viewAnalytics;
    View? _viewRemoteConfig;
    View? _viewCms;
    Button? _btnPushNotifications;
    bool _hasNotificationPermission;
    bool _notificationsEnabled;
    bool _isUpdatingPushButton;

    // Filters for CMS
    private List<(string Label, Func<AppAmbit.ICmsQueryBuilder<CmsExampleModel>> Build)>? _cmsFilters;

    int L(string name) => Resources.GetIdentifier(name, "layout", PackageName);
    int I(string name) => Resources.GetIdentifier(name, "id", PackageName);

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        //Uncomment the line for automatic session management
        //Analytics.EnableManualSession();
        AppAmbit.RemoteConfig.Enable();
        AppAmbitSdk.Start("<YOUR-APPKEY>");

        PushNotifications.Start(this);
        PushNotifications.SetNotificationCustomizer(new SimpleNotificationCustomizer());

        SetContentView(L("activity_main"));

        _container = FindViewById<FrameLayout>(I("content_container"));

        var inflater = LayoutInflater.From(this);
        _viewCrashes = inflater?.Inflate(L("fragment_crashes"), _container, false);
        _viewAnalytics = inflater?.Inflate(L("fragment_analytics"), _container, false);
        _viewRemoteConfig = inflater?.Inflate(L("fragment_remote_config"), _container, false);
        _viewCms = inflater?.Inflate(L("fragment_cms"), _container, false);

        WireCrashesView(_viewCrashes!);
        WireAnalyticsView(_viewAnalytics!);
        WireCmsView(_viewCms!);
        
        var btnCrashes = FindViewById<Button>(I("btn_nav_crashes"))!;
        var btnAnalytics = FindViewById<Button>(I("btn_nav_analytics"))!;
        var btnRemoteConfig = FindViewById<Button>(I("btn_nav_remote_config"))!;
        var btnCms = FindViewById<Button>(I("btn_nav_cms"))!;

        btnCrashes.Click += (s, e) => ShowView(_viewCrashes!);
        btnAnalytics.Click += (s, e) => ShowView(_viewAnalytics!);
        btnRemoteConfig.Click += (s, e) => ShowRemoteConfigView(_viewRemoteConfig!);
        btnCms.Click += (s, e) => ShowView(_viewCms!);

        ShowView(_viewCrashes!);
    }

    protected override void OnResume()
    {
        base.OnResume();
        RefreshPushToggle();
    }

    void ShowView(View v)
    {
        _container!.RemoveAllViews();
        _container.AddView(v);
    }

    void WireAnalyticsView(View root)
    {
        Button B(string id) => root.FindViewById<Button>(I(id))!;
        var btnStartSession = B("btnStartSession");
        var btnEndSession = B("btnEndSession");
        var btnGenerate30DaysTestSessions = B("btnGenerate30DaysTestSessions");
        var btnClearToken = B("btnClearToken");
        var btnTokenRenew = B("btnTokenRenew");
        var btnEventWProperty = B("btnEventWProperty");
        var btnDefaultClickedEventWProperty = B("btnDefaultClickedEventWProperty");
        var btnMax300LengthEvent = B("btnMax300LengthEvent");
        var btnMax20PropertiesEvent = B("btnMax20PropertiesEvent");
        var btn220BatchEvents = B("btn220BatchEvents");
        var btnSecondActivity = B("btnSecondActivity");

        btnStartSession.Click += (s, e) => { try { Analytics.StartSession(); } catch { } };
        btnEndSession.Click += (s, e) => { try { Analytics.EndSession(); } catch { } };

        btnEventWProperty.Click += (s, e) =>
        {
            var map = new Dictionary<string, string> { { "Count", "41" } };
            Analytics.TrackEvent("ButtonClicked", map);
            Toast.MakeText(this, "OnClick event generated", ToastLength.Short)?.Show();
        };

        btnDefaultClickedEventWProperty.Click += (s, e) =>
        {
            Analytics.GenerateTestEvent();
            Toast.MakeText(this, "Event generated", ToastLength.Short)?.Show();
        };

        btnMax300LengthEvent.Click += (s, e) =>
        {
            var props = new Dictionary<string, string>();
            string c300 = new string(Enumerable.Repeat('1', 300).ToArray());
            string c302 = new string(Enumerable.Repeat('2', 302).ToArray());
            props[c300] = c300; props[c302] = c302;
            Analytics.TrackEvent(c300, props);
            Toast.MakeText(this, "1 event generated", ToastLength.Short)?.Show();
        };

        btnMax20PropertiesEvent.Click += (s, e) =>
        {
            var props = Enumerable.Range(1, 25).ToDictionary(i => i.ToString("00"), i => i.ToString("00"));
            Analytics.TrackEvent("TestMaxProperties", props);
            Toast.MakeText(this, "1 event generated", ToastLength.Short)?.Show();
        };

        btnClearToken.Click += (s, e) => Analytics.ClearToken();

        btnTokenRenew.Click += async (s, e) =>
        {
            Analytics.ClearToken();

            var errorTasks = Enumerable.Range(0, 5).Select(_ => Task.Run(() =>
            {
                var props = new Dictionary<string, string> { { "user_id", "1" } };
                Crashes.LogError("Sending 5 errors after an invalid token", props);
            })).ToArray();
            await Task.WhenAll(errorTasks);

            Analytics.ClearToken();

            var eventTasks = Enumerable.Range(0, 5).Select(_ => Task.Run(() =>
            {
                var ev = new Dictionary<string, string> { { "Test Token", "5 events sent" } };
                Analytics.TrackEvent("Sending 5 events after an invalid token", ev);
            })).ToArray();
            await Task.WhenAll(eventTasks);

            Toast.MakeText(this, "5 events and errors sent", ToastLength.Long)?.Show();
        };

        btnSecondActivity.Click += (s, e) =>
        {
            Finish();
            StartActivity(new Intent(this, typeof(SecondActivity)));
        };
    }

    void WireCrashesView(View root)
    {
        T Find<T>(string id) where T : View => (T)root.FindViewById(I(id))!;
        var btnDidCrash = Find<Button>("btnDidCrash");
        var btnSendCustomLogError = Find<Button>("btnSendCustomLogError");
        var btnSendDefaultLogError = Find<Button>("btnSendDefaultLogError");
        var btnSendExceptionLogError = Find<Button>("btnSendExceptionLogError");
        var btnSetUserId = Find<Button>("btnSetUserId");
        var btnSetUserEmail = Find<Button>("btnSetUserEmail");
        var btnThrowNewCrash = Find<Button>("btnThrowNewCrash");
        var btnGenerateTestCrash = Find<Button>("btnGenerateTestCrash");
        _btnPushNotifications = Find<Button>("btnPushNotifications");

        var etUserId = Find<EditText>("etUserId");
        var etUserEmail = Find<EditText>("etUserEmail");
        var etCustomLogErrorText = Find<EditText>("etCustomLogErrorText");

        etUserId.Text = Guid.NewGuid().ToString();
        etUserEmail.Text = "test@gmail.com";
        etCustomLogErrorText.Text = "Test Log Message";

        btnDidCrash.Click += async (s, e) =>
        {
            bool did = await Task.Run(() => Crashes.DidCrashInLastSession());
            ShowAlert("Crash", did ? "Application crashed in the last session"
                                   : "Application did not crash in the last session");
        };

        btnSendCustomLogError.Click += (s, e) =>
        {
            var msg = etCustomLogErrorText.Text ?? "Test Log Message";
            Task.Run(() => Crashes.LogError(msg));
            ShowAlert("Info", "LogError sent");
        };

        btnSendDefaultLogError.Click += (s, e) =>
        {
            Task.Run(() =>
            {
                var props = new Dictionary<string, string> { { "user_id", "1" } };
                Crashes.LogError("Test Log Error", props);
            });
            ShowAlert("Info", "Test Default LogError sent");
        };

        btnSendExceptionLogError.Click += (s, e) =>
        {
            try { throw new NullReferenceException(); }
            catch (Exception ex)
            {
                var props = new Dictionary<string, string> { { "user_id", "1" } };
                Crashes.LogError(ex, props);
                ShowAlert("Info", "Test Exception LogError sent");
            }
        };

        btnSetUserId.Click += (s, e) => { Analytics.SetUserId(etUserId.Text); ShowAlert("Info", "User ID changed"); };
        btnSetUserEmail.Click += (s, e) => { Analytics.SetUserEmail(etUserEmail.Text); ShowAlert("Info", "User email changed"); };
        btnThrowNewCrash.Click += (s, e) => { throw new NullReferenceException(); };
        btnGenerateTestCrash.Click += (s, e) => Crashes.GenerateTestCrash();
        _btnPushNotifications!.Click += async (s, e) => await HandlePushToggleAsync();

        RefreshPushToggle();
    }

    void ShowAlert(string title, string message)
    {
        RunOnUiThread(() =>
        {
            new AlertDialog.Builder(this)!
                .SetTitle(title)!
                .SetMessage(message)!
                .SetPositiveButton("OK", (s, e) => { })!
                .Show();
        });
    }

    async Task HandlePushToggleAsync()
    {
        if (_isUpdatingPushButton || _btnPushNotifications == null)
            return;

        _isUpdatingPushButton = true;

        try
        {
            if (!_hasNotificationPermission)
            {
                var tcs = new TaskCompletionSource<bool>();
                PushNotifications.RequestNotificationPermission(new PermissionListener(granted => tcs.TrySetResult(granted)));
                var granted = await tcs.Task;

                if (granted)
                {
                    _hasNotificationPermission = true;
                    _notificationsEnabled = true;
                    PushNotifications.SetNotificationsEnabled(true);
                    UpdatePushButtonText();
                    ShowAlert("Done", "Notifications enabled");
                }
                else
                {
                    RefreshPushToggle();
                    ShowAlert("Permission denied", "Notification permission denied by the user.");
                }

                return;
            }

            var targetEnabled = !_notificationsEnabled;
            PushNotifications.SetNotificationsEnabled(targetEnabled);
            _notificationsEnabled = targetEnabled;
            UpdatePushButtonText();
            ShowAlert("Done", targetEnabled ? "Notifications enabled" : "Notifications disabled");
        }
        catch (Exception ex)
        {
            ShowAlert("Error", ex.Message);
        }
        finally
        {
            _isUpdatingPushButton = false;
        }
    }

    void RefreshPushToggle()
    {
        if (_btnPushNotifications == null)
            return;

        _hasNotificationPermission = PushNotifications.HasSystemPermission();
        _notificationsEnabled = PushNotifications.IsNotificationsEnabled();
        UpdatePushButtonText();
    }

    void UpdatePushButtonText()
    {
        if (_btnPushNotifications == null)
            return;

        _btnPushNotifications.Text = !_hasNotificationPermission
            ? "Allow Notifications"
            : _notificationsEnabled
                ? "Disable Notifications"
                : "Enable Notifications";
    }

    void UpdateRemoteConfigUI(View v)
    {
        var banner = v.FindViewById<FrameLayout>(I("banner_view"));
        var dataLabel = v.FindViewById<TextView>(I("data_label"));
        var discountLabel = v.FindViewById<TextView>(I("discount_label"));

        if (banner != null) 
            banner.Visibility = AppAmbit.RemoteConfig.GetBoolean("banner") ? ViewStates.Visible : ViewStates.Gone;
        
        if (dataLabel != null)
            dataLabel.Text = AppAmbit.RemoteConfig.GetString("data");

        if (discountLabel != null)
        {
            long discount = AppAmbit.RemoteConfig.GetLong("discount");
            discountLabel.Text = $"{discount}% OFF";
        }
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

    private sealed class SimpleNotificationCustomizer : PushNotifications.INotificationCustomizer
    {
        public void Customize(object context, object builder, PushNotificationData notification)
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

    void ShowRemoteConfigView(View v)
    {
        ShowView(v);
        UpdateRemoteConfigUI(v);
    }

    void WireCmsView(View root)
    {
        T Find<T>(string id) where T : View => (T)root.FindViewById(I(id))!;
        
        var spinner = Find<Spinner>("spin_cms_filters");
        var btnApply = Find<Button>("btn_cms_apply_filter");
        var etSearch = Find<EditText>("et_cms_search");
        var btnSearch = Find<Button>("btn_cms_search");
        var btnGetAll = Find<Button>("btn_cms_get_all");
        var progress = Find<ProgressBar>("progress_cms_loading");
        var recycler = Find<AndroidX.RecyclerView.Widget.RecyclerView>("recycler_cms");
        var tvEmpty = Find<TextView>("tv_cms_empty");

        var adapter = new CmsAdapter();
        recycler.SetLayoutManager(new AndroidX.RecyclerView.Widget.LinearLayoutManager(this));
        recycler.SetAdapter(adapter);

        const string collection = "tech_inventory";

        _cmsFilters = new()
        {
            // Equality
            ("Equals: item_sku = TEC-02", () => AppAmbit.Cms.Content<CmsExampleModel>(collection).Equals("item_sku", "TEC-02")),
            ("Not Equals: item_sku ≠ TEC-02", () => AppAmbit.Cms.Content<CmsExampleModel>(collection).NotEquals("item_sku", "TEC-02")),
            ("In List: category = Cat 1", () => AppAmbit.Cms.Content<CmsExampleModel>(collection).InList("category", new[] { "Cat 1" })),
            ("Boolean: in_stock = true", () => AppAmbit.Cms.Content<CmsExampleModel>(collection).Equals("in_stock", "true")),

            // Text matching
            ("Contains: product_name contains 'Pro'", () => AppAmbit.Cms.Content<CmsExampleModel>(collection).Contains("product_name", "Pro")),
            ("Starts With: item_sku starts with 'TEC'", () => AppAmbit.Cms.Content<CmsExampleModel>(collection).StartsWith("item_sku", "TEC")),

            // List membership
            ("In List: item_sku in [TEC-01, TEC-02]", () => AppAmbit.Cms.Content<CmsExampleModel>(collection).InList("item_sku", new[] { "TEC-01", "TEC-02" })),
            ("Not In List: item_sku not in [TEC-01, TEC-02]", () => AppAmbit.Cms.Content<CmsExampleModel>(collection).NotInList("item_sku", new[] { "TEC-01", "TEC-02" })),

            // Numeric comparisons
            ("Greater Than: price > 500", () => AppAmbit.Cms.Content<CmsExampleModel>(collection).GreaterThan("price", 500)),
            ("Greater Or Equal: price >= 500", () => AppAmbit.Cms.Content<CmsExampleModel>(collection).GreaterThanOrEqual("price", 500)),
            ("Less Than: price < 500", () => AppAmbit.Cms.Content<CmsExampleModel>(collection).LessThan("price", 500)),
            ("Less Or Equal: price <= 500", () => AppAmbit.Cms.Content<CmsExampleModel>(collection).LessThanOrEqual("price", 500)),

            // Sorting
            ("Order By product_name ASC", () => AppAmbit.Cms.Content<CmsExampleModel>(collection).OrderByAscending("product_name")),
            ("Order By product_name DESC", () => AppAmbit.Cms.Content<CmsExampleModel>(collection).OrderByDescending("product_name")),
            ("Order By price ASC", () => AppAmbit.Cms.Content<CmsExampleModel>(collection).OrderByAscending("price")),
            ("Order By price DESC", () => AppAmbit.Cms.Content<CmsExampleModel>(collection).OrderByDescending("price")),

            // Pagination
            ("Pagination: Page 1, 2 per page", () => AppAmbit.Cms.Content<CmsExampleModel>(collection).GetPage(1).GetPerPage(2)),
            ("Pagination: Page 2, 2 per page", () => AppAmbit.Cms.Content<CmsExampleModel>(collection).GetPage(2).GetPerPage(2)),
        };

        var filterNames = _cmsFilters.Select(f => f.Label).ToList();
        var arrayAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, filterNames);
        arrayAdapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
        spinner.Adapter = arrayAdapter;

        void SetLoading(bool isLoading)
        {
            RunOnUiThread(() =>
            {
                progress.Visibility = isLoading ? Android.Views.ViewStates.Visible : Android.Views.ViewStates.Gone;
                btnApply.Enabled = !isLoading;
                btnSearch.Enabled = !isLoading;
                btnGetAll.Enabled = !isLoading;
            });
        }

        async Task LoadResults(AppAmbit.ICmsQueryBuilder<CmsExampleModel> query)
        {
            SetLoading(true);
            try
            {
                var items = await query.GetListAsync();
                RunOnUiThread(() =>
                {
                    adapter.UpdateData(items);
                    tvEmpty.Visibility = items.Count == 0 ? Android.Views.ViewStates.Visible : Android.Views.ViewStates.Gone;
                });
            }
            catch (Exception ex)
            {
                ShowAlert("Error", ex.Message);
            }
            finally
            {
                SetLoading(false);
            }
        }

        btnGetAll.Click += async (s, e) => 
        {
            await LoadResults(AppAmbit.Cms.Content<CmsExampleModel>(collection));
        };

        btnApply.Click += async (s, e) =>
        {
            int selected = spinner.SelectedItemPosition;
            if (selected >= 0 && selected < _cmsFilters.Count)
            {
                var query = _cmsFilters[selected].Build();
                await LoadResults(query);
            }
        };

        btnSearch.Click += async (s, e) =>
        {
            var term = etSearch.Text?.Trim();
            if (!string.IsNullOrEmpty(term))
                await LoadResults(AppAmbit.Cms.Content<CmsExampleModel>(collection).Search(term));
        };
    }
}
