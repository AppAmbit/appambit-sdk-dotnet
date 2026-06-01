# AppAmbit Push Notifications SDK

**Seamlessly integrate push notifications with your AppAmbit analytics.**

Extension of the core AppAmbit SDK for **Android** (Firebase Cloud Messaging) and **iOS** (APNs). Works with MAUI, Avalonia, and native .NET Android / iOS projects.

---

## Contents

- [Features](#features)
- [Requirements](#requirements)
- [Install](#install)
- [Quickstart](#quickstart)
  - [MAUI](#maui)
  - [Avalonia](#avalonia)
  - [Native .NET iOS](#native-net-ios)
  - [Native .NET Android](#native-net-android)
- [Usage](#usage)
- [Native Setup](#native-setup)
  - [Android Setup](#android-setup)
  - [iOS Setup](#ios-setup)
  - [iOS Notification Service Extension](#ios-notification-service-extension)
- [Customization](#customization)

---

## Features

- **Simple setup** — integrates in minutes on both platforms after the core SDK.
- **Enable / disable notifications** — manage user preferences at the SDK level, independent of OS permission.
- **Listeners** — foreground, opened (tapped), and Android background callbacks.
- **Cold-start taps** — iOS buffers tapped-notification payloads when the app was fully terminated and delivers them to your opened listener.
- **Automatic field handling** — FCM payload fields (`title`, `body`, `color`, `icon`, `channel_id`, `click_action`, `image`) and APNs `aps` fields are parsed automatically.
- **Rich media** — image attachment support on iOS via the Notification Service Extension; BigPicture style on Android.
- **Permission helper** — `RequestNotificationPermission` on Android 13+ and iOS.

---

## Requirements

- **.NET 10** — multi-targets `net10.0`, `net10.0-android`, `net10.0-ios`.
- **Core SDK**: `com.AppAmbit.Sdk` — or `com.AppAmbit.Maui` / `com.AppAmbit.Avalonia` when using a host integration.
- **Android**: Firebase project with `google-services.json`. Android API 21+.
- **iOS**: APNs-enabled app identifier with the **Push Notifications** capability. iOS 12.0+. The native frameworks (`AppAmbit.framework`, `AppAmbitPushNotifications.framework`) ship inside the package — no CocoaPods step needed.

---

## Install

```bash
# MAUI or Avalonia host
dotnet add package com.AppAmbit.Maui          # or com.AppAmbit.Avalonia
dotnet add package com.AppAmbit.PushNotifications

# Native .NET Android / iOS (no MAUI/Avalonia host)
dotnet add package com.AppAmbit.Sdk
dotnet add package com.AppAmbit.PushNotifications
```

Place `google-services.json` under `Platforms/Android/` and add to your project file:

```xml
<GoogleServicesJson Include="Platforms/Android/google-services.json" />
```

---

## Quickstart

Pick your framework below. Each section covers the full setup end to end.

---

### MAUI

**`MauiProgram.cs`**
```csharp
builder.UseMauiApp<App>().UseAppAmbit("<YOUR-APPKEY>");
```

**`Platforms/iOS/AppDelegate.cs`**

`FinishedLaunching` runs before the MAUI Shell is ready, so **do not register listeners here** — register them from a shared page (e.g. `MainPage`). `Start` and `HandleLaunchOptions` must be called here.

```csharp
using Foundation;
using UIKit;
using AppAmbit.PushNotifications;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication app, NSDictionary options)
    {
        var result = base.FinishedLaunching(app, options);

        PushNotifications.Start();

        // Required for cold-start taps: when the app is fully terminated and the user taps
        // a notification, iOS delivers the payload here instead of the notification delegate.
        // The SDK buffers it and dispatches to SetOpenedListener once it is registered.
        PushNotifications.HandleLaunchOptions(options);

        return result;
    }
}
```

**`Platforms/Android/MainActivity.cs`**

Register the notification customizer and call `Start` here. **Do not register listeners here** — register them from a shared page so the MAUI Shell is ready to navigate.

```csharp
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AppAmbit.PushNotifications;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ...)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        PushNotifications.Android.SetNotificationCustomizer(new MyNotificationCustomizer());
        base.OnCreate(savedInstanceState);
        PushNotifications.Start(this);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        // Required for background taps (app in background, user taps notification).
        PushNotifications.Android.HandleNotificationOpened(intent);
    }
}
```

**Shared page (e.g. `MainPage.xaml.cs`)**

Register all listeners here, once the MAUI Shell is ready:

```csharp
public MainPage()
{
    InitializeComponent();

    PushNotifications.SetForegroundListener(data => /* ... */);
    PushNotifications.SetOpenedListener(data =>
    {
        // Navigate here — Shell is guaranteed to be ready.
        Shell.Current.GoToAsync("//SecondPage");
    });
    PushNotifications.Android.SetBackgroundListener(data => /* ... */);

    // Request permission (Android 13+ and iOS).
    PushNotifications.RequestNotificationPermission(callback: granted =>
    {
        if (granted) PushNotifications.SetNotificationsEnabled(true);
    });
}
```

---

### Avalonia

**`Platforms/iOS/AppDelegate.cs`**

`CustomizeAppBuilder` does not receive launch options, so cold-start taps (app fully terminated) are **not supported** via `HandleLaunchOptions` in Avalonia iOS. All other scenarios (foreground, background tap) work normally — register listeners from your main view.

```csharp
using Avalonia;
using Avalonia.iOS;
using AppAmbit;
using AppAmbit.PushNotifications;

[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        AppAmbitSdk.Start("<YOUR-APPKEY>");
        PushNotifications.Start();
        return base.CustomizeAppBuilder(builder).WithInterFont();
    }
}
```

**`Platforms/Android/MainActivity.cs`**

```csharp
using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using AppAmbit;
using AppAmbit.PushNotifications;

[Activity(MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        AppAmbitSdk.Start("<YOUR-APPKEY>");
        PushNotifications.Start(this);
        return base.CustomizeAppBuilder(builder);
    }

    protected override void OnNewIntent(Android.Content.Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        PushNotifications.Android.HandleNotificationOpened(intent);
    }
}
```

**Main view (e.g. `MainView.axaml.cs`)**

```csharp
public MainView()
{
    InitializeComponent();

    PushNotifications.SetForegroundListener(data => /* ... */);
    PushNotifications.SetOpenedListener(data => /* ... */);
    PushNotifications.Android.SetBackgroundListener(data => /* ... */);

    PushNotifications.RequestNotificationPermission(new PermissionListener(granted =>
    {
        if (granted) PushNotifications.SetNotificationsEnabled(true);
    }));
}

class PermissionListener : PushNotifications.IPermissionListener
{
    private readonly Action<bool> _onResult;
    public PermissionListener(Action<bool> onResult) => _onResult = onResult;
    public void OnPermissionResult(bool isGranted) => _onResult(isGranted);
}
```

---

### Native .NET iOS

**`AppDelegate.cs`**

Register listeners **before** calling `Start` so the SDK can dispatch cold-start payloads immediately. Call `HandleLaunchOptions` **after** `Start` to handle taps on notifications received while the app was fully terminated.

```csharp
using AppAmbit;
using AppAmbit.PushNotifications;

[Register("AppDelegate")]
public class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        // 1. Register listeners first so the SDK can dispatch cold-start immediately.
        PushNotifications.SetForegroundListener(data =>
            Console.WriteLine($"[Foreground] {data}"));

        PushNotifications.SetOpenedListener(data =>
        {
            Console.WriteLine($"[Opened] {data}");
            // Navigate on main thread:
            Foundation.NSThread.MainThread.BeginInvokeOnMainThread(() => { /* navigate */ });
        });

        // 2. Init the AppAmbit core SDK.
        AppAmbitSdk.Start("<YOUR-APPKEY>");

        // 3. Start push — registers the native notification delegate.
        PushNotifications.Start();

        // 4. Deliver cold-start tap if the user launched the app by tapping a notification.
        PushNotifications.HandleLaunchOptions(launchOptions);

        // 5. Request system permission.
        PushNotifications.RequestNotificationPermission(callback: granted =>
        {
            if (granted) PushNotifications.SetNotificationsEnabled(true);
        });

        Window = new UIWindow(UIScreen.MainScreen.Bounds);
        Window.RootViewController = new MainViewController();
        Window.MakeKeyAndVisible();
        return true;
    }
}
```

**Notification scenarios covered:**

| Scenario | How it works |
|---|---|
| App in **foreground**, notification arrives | `SetForegroundListener` fires immediately. |
| App in **background**, user taps notification | `SetOpenedListener` fires via the notification delegate. |
| App **fully terminated**, user taps notification | `HandleLaunchOptions` buffers the payload; `SetOpenedListener` fires immediately after `Start`. |

---

### Native .NET Android

**`MainActivity.cs`**

Register listeners and call `Start` from `OnCreate`. Android delivers cold-start taps via the initial `Intent`, and background taps via `OnNewIntent`.

```csharp
using AppAmbit;
using AppAmbit.PushNotifications;

[Activity(MainLauncher = true, LaunchMode = LaunchMode.SingleTop)]
public class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        AppAmbitSdk.Start("<YOUR-APPKEY>");

        // Register listeners before Start so the SDK can dispatch the cold-start intent.
        PushNotifications.SetForegroundListener(data => /* ... */);
        PushNotifications.SetOpenedListener(data => /* ... */);
        PushNotifications.Android.SetBackgroundListener(data => /* ... */);
        PushNotifications.Android.SetNotificationCustomizer(new MyNotificationCustomizer());

        PushNotifications.Start(this);

        // Request Android 13+ notification permission.
        PushNotifications.RequestNotificationPermission(this);
    }

    protected override void OnNewIntent(Android.Content.Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        // Required for background taps (app in background, user taps notification).
        PushNotifications.Android.HandleNotificationOpened(intent);
    }
}
```

**Notification scenarios covered:**

| Scenario | How it works |
|---|---|
| App in **foreground**, notification arrives | `SetForegroundListener` fires immediately. |
| App in **background**, user taps notification | `OnNewIntent` → `HandleNotificationOpened` → `SetOpenedListener` fires. |
| App **fully terminated**, user taps notification | `OnCreate` → `PushNotifications.Start(this)` reads the launch `Intent` → `SetOpenedListener` fires. |

---

## Usage

### Enable / Disable Notifications

```csharp
PushNotifications.SetNotificationsEnabled(false); // opt out
PushNotifications.SetNotificationsEnabled(true);  // opt back in

bool enabled = PushNotifications.IsNotificationsEnabled();
```

### System Permission vs. SDK Toggle

These are two independent states — check the one that applies:

| Method | What it returns |
|---|---|
| `HasNotificationPermission()` | Whether the **OS** allows this app to show notifications (iOS authorization / Android 13+ grant). |
| `IsNotificationsEnabled()` | The **SDK toggle** set via `SetNotificationsEnabled`, synced to the AppAmbit dashboard. |

A device shows notifications only when **both** are true.

### Listeners

Register listeners **once at startup**, right after `PushNotifications.Start(...)`. A push can launch a killed app, so the listeners must already be set when the host comes up.

```csharp
// Foreground — fires when a push arrives while the app is open.
PushNotifications.SetForegroundListener(data =>
    Debug.WriteLine($"[Foreground] {data.Title}: {data.Body}"));

// Opened — fires when the user taps a notification.
PushNotifications.SetOpenedListener(data =>
    Debug.WriteLine($"[Opened] {data.Title}"));

// Background (Android only) — fires when a push arrives with the app in background/killed.
PushNotifications.Android.SetBackgroundListener(data =>
    Debug.WriteLine($"[Background] {data.Title}"));
```

> **Where to register by framework**
> | Framework | Location |
> |---|---|
> | MAUI | `App.xaml.cs` constructor or startup page, after `UseAppAmbit(...)` |
> | Avalonia | Main view constructor (e.g. `MainView.axaml.cs`), after `PushNotifications.Start(...)` in `CustomizeAppBuilder` has run |
> | Native Android | `MainActivity.OnCreate`, after `PushNotifications.Start(...)` |
> | Native iOS | `AppDelegate.FinishedLaunching`, after `PushNotifications.Start()` |

### Notification Data Model

Every listener receives a `PushNotificationData` object:

| Field | Type | Platform | Notes |
|---|---|---|---|
| `Title` | `string?` | Android + iOS | Notification title. |
| `Body` | `string?` | Android + iOS | Notification body text. |
| `ImageUrl` | `string?` | Android + iOS | URL of an attached image, if any. |
| `Data` | `IDictionary<string, string>?` | Android + iOS | Custom payload key-value pairs. |
| `Android` | `AndroidPushData?` | Android only | Android-specific extras. `null` on iOS. |
| `Ios` | `IosPushData?` | iOS only | iOS-specific extras from `aps`. `null` on Android. |

**`AndroidPushData`** fields: `Color`, `SmallIconName`, `ChannelId`, `Priority`, `Sound`, `ClickAction`, `Ticker`, `Visibility`, `Tag`, `Sticky`.

**`IosPushData`** fields: `Badge`, `Sound`, `ThreadId`, `Category`.

```csharp
PushNotifications.SetOpenedListener(data =>
{
    var badge   = data.Ios?.Badge;          // iOS only
    var channel = data.Android?.ChannelId;  // Android only
    var custom  = data.Data?["your_key"];   // both platforms
});
```

---

## Native Setup

### Android Setup

#### 1. Add `google-services.json`

Download from your Firebase console and place it in your app module:

```
Platforms/Android/google-services.json
```

Then reference it in your `.csproj`:

```xml
<GoogleServicesJson Include="Platforms/Android/google-services.json" />
```

#### 2. Apply the Google Services Gradle plugin

`android/build.gradle.kts`
```kotlin
buildscript {
    dependencies {
        classpath("com.google.gms:google-services:4.3.15")
    }
}
```

`android/app/build.gradle.kts`
```kotlin
plugins {
    id("com.google.gms.google-services")
}
```

The `POST_NOTIFICATIONS` permission (Android 13+) is declared by the SDK and merged automatically — no manual manifest entry needed.

---

### iOS Setup

#### 1. Enable Push Notifications capability

In Xcode, open your `.xcworkspace`, select your app target → **Signing & Capabilities** → **+ Capability** → **Push Notifications**.

#### 2. No CocoaPods step needed

The native `AppAmbit.framework` and `AppAmbitPushNotifications.framework` are bundled inside the NuGet package and copied into your app bundle automatically at build time.

---

### iOS Notification Service Extension

To download images and mutate notification content before the banner is shown (including when the app is in the background or killed), add a Notification Service Extension (NSE). This runs in a separate process — iOS only.

> On Android, use `PushNotifications.Android.SetBackgroundListener` instead.

#### 1. Create the NSE project

Add a `net10.0-ios` app-extension project referencing this package:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-ios</TargetFramework>
    <IsAppExtension>true</IsAppExtension>
    <SupportedOSPlatformVersion>12.0</SupportedOSPlatformVersion>
    <ApplicationId>com.yourapp.NotificationExtension</ApplicationId>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="com.AppAmbit.PushNotifications" Version="*" />
  </ItemGroup>
</Project>
```

`Info.plist` — register the principal class:

```xml
<key>NSExtension</key>
<dict>
  <key>NSExtensionPointIdentifier</key>
  <string>com.apple.usernotifications.service</string>
  <key>NSExtensionPrincipalClass</key>
  <string>NotificationService</string>
</dict>
```

#### 2. Reference the NSE from your iOS app

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net10.0-ios'">
  <ProjectReference Include="..\YourApp.NotificationExtension\YourApp.NotificationExtension.csproj">
    <IsAppExtension>true</IsAppExtension>
  </ProjectReference>
</ItemGroup>
```

#### 3. Subclass `AppAmbitNotificationServiceExtension`

**Minimal** — no custom code needed; the SDK downloads the image from the `"image"` key and attaches it:

```csharp
using AppAmbit.PushNotifications;

[Register("NotificationService")]
public class NotificationService : AppAmbitNotificationServiceExtension { }
```

**With payload mutation** — override `HandlePayload` to mutate `content` before the image is downloaded and the banner is shown:

```csharp
using AppAmbit.PushNotifications;
using UserNotifications;

[Register("NotificationService")]
public class NotificationService : AppAmbitNotificationServiceExtension
{
    protected override void HandlePayload(
        AppAmbitNotificationData notification,
        UNMutableNotificationContent content)
    {
        content.Title = $"{notification.Title} ✦";
        // notification.Body, notification.ImageUrl, notification.Data also available
    }

    // Optional — called when iOS is about to terminate the extension (~30 s limit).
    protected override void OnTimeExpiring() { }
}
```

The NSE payload **must** include `mutable-content: 1` for `HandlePayload` to be called. The base class handles image download and delivery.

---

## Customization

### What the SDK applies automatically

**iOS** — the SDK parses the `aps` dictionary and the top-level `"image"` key automatically. No customizer needed for standard fields.

**Android** — the SDK reads the FCM payload and configures `NotificationCompat.Builder` before posting:

| Payload field | What the SDK does |
|---|---|
| `title` / `body` | Sets content title and text. |
| `icon` | Sets small icon (drawable lookup; falls back to app icon). |
| `color` | Sets accent color (hex, e.g. `#FF5722`). |
| `image` | Downloads and sets BigPicture style. |
| `channel_id` | Creates and assigns the notification channel (Android 8+). |
| `sound` | Sets sound (`"default"` or resource name in `res/raw/`). |
| `notification_priority` | Sets priority (`-2..2` or `"high"`, `"low"`, …). |
| `click_action` | Exposed in `Android.ClickAction` for routing. |
| Custom `data` keys | Passed through verbatim in `PushNotificationData.Data`. |

### Android — modify the notification before display

Register a customizer for changes beyond what the SDK applies (actions, group keys, RemoteViews, …):

```csharp
class MyCustomizer : PushNotifications.INotificationCustomizer
{
    public void Customize(object context, object builder, PushNotificationData notification)
    {
        if (builder is AndroidX.Core.App.NotificationCompat.Builder b)
            b.SetColor(Android.Graphics.Color.ParseColor("#0066FF"));
    }
}

PushNotifications.Android.SetNotificationCustomizer(new MyCustomizer());
```

In cross-targeted MAUI projects, use `dynamic` to avoid iOS/Windows compile errors:

```csharp
public void Customize(object context, object builder, PushNotificationData notification)
{
    dynamic b = builder;
    b.SetColor(unchecked((int)0xFF0066FF));
}
```
