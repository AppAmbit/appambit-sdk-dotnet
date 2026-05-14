# AppAmbit Push Notifications SDK (MAUI / Android)

**Seamlessly integrate push notifications with your AppAmbit analytics.**

Extension of the core AppAmbit MAUI SDK for handling Firebase Cloud Messaging (FCM). Supports both Android and iOS — the native iOS frameworks ship inside the package, no CocoaPods step required.

---

## Contents
* [Features](#features)
* [Requirements](#requirements)
* [Install](#install)
* [Quickstart](#quickstart)
* [Usage](#usage)
* [Customization](#customization)
* [iOS — Notification Service Extension](#ios--notification-service-extension)

---

## Features
* Simple setup after the core SDK.
* Enable/disable notifications at business + FCM level.
* Automatically handles standard FCM notification fields (`title`, `body`, `color`, `icon`, `channel_id`, `click_action`, `image`).
* Permission helper for `POST_NOTIFICATIONS` (Android 13+).
* Optional hook to fully customize the notification.

## Requirements
* .NET 10. The push package multi-targets `net10.0;net10.0-android;net10.0-ios` — Android API 21+ and iOS 12.2+.
* Packages:
  * `com.AppAmbit.Sdk` (core) — or `com.AppAmbit.Maui` when using the MAUI host integration
  * `com.AppAmbit.PushNotifications`
* Firebase project + `google-services.json` matching your `ApplicationId` (package name).
* iOS: no CocoaPods step. The native `AppAmbitPushNotifications.framework` and `AppAmbit.framework` are bundled in the package and copied into the app bundle automatically at build time.
* For background delivery, send FCM with high priority (`priority: "high"` in legacy or `android.priority: "HIGH"` in HTTP v1). Do **not** put `priority` inside `data`.

## Install
```bash
# MAUI host
dotnet add package com.AppAmbit.Maui
dotnet add package com.AppAmbit.PushNotifications

# .NET Android / .NET iOS (native, no MAUI host)
dotnet add package com.AppAmbit.Sdk
dotnet add package com.AppAmbit.PushNotifications
```

Add the Firebase config to your project file and place `google-services.json` under `Platforms/Android/`:
```xml
<GoogleServicesJson Include="Platforms/Android/google-services.json" />
```

On iOS nothing else is needed to install the SDK — the bundled frameworks are referenced and code-signed by the build. To process pushes before they are shown (background/killed app), add a [Notification Service Extension](#ios--notification-service-extension).

## Quickstart

### MAUI
`MauiProgram.cs`
```csharp
using AppAmbit;

var builder = MauiApp.CreateBuilder();
builder
    .UseMauiApp<App>()
    .UseAppAmbit("<YOUR-APPKEY>");
```

`Platforms/Android/MainActivity.cs`
```csharp
using AppAmbit.PushNotifications;
using AndroidX.Activity;

protected override void OnCreate(Bundle? savedInstanceState)
{
    base.OnCreate(savedInstanceState);
    PushNotifications.Start(ApplicationContext);
    PushNotifications.RequestNotificationPermission((ComponentActivity)this);
}
```

### .NET Android (native Activity)
```csharp
using AppAmbitMaui; // core SDK
using AppAmbit.PushNotifications;
using AndroidX.AppCompat.App;

[Activity(Theme = "@style/Theme.AppCompat.Light.NoActionBar", MainLauncher = true)]
public class MainActivity : AppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        AppAmbitSdk.Start("<YOUR-APPKEY>");

        PushNotifications.Start(ApplicationContext);
        PushNotifications.RequestNotificationPermission(this);
    }
}
```

## Usage

### Enable/Disable & Status
```csharp
// Disable (updates backend + deletes FCM token)
PushNotifications.SetNotificationsEnabled(ctx, false);

// Enable again
PushNotifications.SetNotificationsEnabled(ctx, true);

// Query current setting
bool enabled = PushNotifications.IsNotificationsEnabled(ctx);
```

### Permission listener (optional)
```csharp
class PermissionListener : Java.Lang.Object, PushNotifications.IPermissionListener
{
    public void OnPermissionResult(bool granted) =>
        System.Diagnostics.Debug.WriteLine($"Push permission: {granted}");
}

PushNotifications.RequestNotificationPermission(activity, new PermissionListener());
```

## Customization

The SDK already applies standard FCM fields. To react to incoming pushes, register listeners — they receive the full payload (including any custom `data` keys) so you can route, log, or trigger app behavior accordingly.

### Where to register listeners

Register the listeners below (and the customizer) **once, at host startup, right after `PushNotifications.Start(...)`** — never inside a button handler or a screen that may not exist yet. A push can launch a killed app, so the listeners must already be set when the host comes up. The exact location depends on the framework:

| Framework | Where `PushNotifications.Start(...)` and the listeners go |
|---|---|
| **MAUI** | `Platforms/Android/MainActivity.cs` → `OnCreate` (Android). Register the cross-platform listeners in your startup page constructor or `App` ctor, after `UseAppAmbit(...)` in `MauiProgram.cs` has run. |
| **Avalonia** | The main view code-behind constructor (e.g. `MainView.axaml.cs`), after the core SDK and `PushNotifications.Start(...)`. |
| **.NET Android (native)** | `MainActivity.cs` → `OnCreate`, immediately after `AppAmbitSdk.Start(...)` and `PushNotifications.Start(this)`. |
| **.NET iOS (native)** | `AppDelegate.cs` → `FinishedLaunching`, alongside `AppAmbitSdk.Start(...)` and `PushNotifications.Start()`. |

> Pre-display processing for background/killed apps on iOS does **not** go here — it belongs in the [Notification Service Extension](#ios--notification-service-extension).

```csharp
PushNotifications.SetForegroundListener(data =>
{
    System.Diagnostics.Debug.WriteLine($"[Foreground] {data.Title} — {data.Body}");
});

PushNotifications.SetOpenedListener(data =>
{
    System.Diagnostics.Debug.WriteLine($"[Opened] {data.Title}");
});

// Android only — invoked when a push arrives while the app is in the background.
PushNotifications.Android.SetBackgroundListener(data =>
{
    System.Diagnostics.Debug.WriteLine($"[Background] {data.Title}");
});
```

Send any custom keys you need in `data`; `PushNotificationData.Data` exposes the full map.

### Android — modify the notification before display

On Android only, register a customizer to mutate `NotificationCompat.Builder` before the system posts the notification (tint color, subtext, actions, channel, etc.). On iOS, the equivalent mutation point is the Notification Service Extension.

```csharp
class MyCustomizer : PushNotifications.INotificationCustomizer
{
    public void Customize(object context, object builder, PushNotificationData notification)
    {
        if (builder is AndroidX.Core.App.NotificationCompat.Builder b)
        {
            b.SetColor(Android.Graphics.Color.ParseColor("#0066FF"));
            if (notification.Data is { } data && data.TryGetValue("subtext", out var sub))
                b.SetSubText(sub);
        }
    }
}

PushNotifications.Android.SetNotificationCustomizer(new MyCustomizer());
```

In cross-targeted MAUI projects where direct references to `AndroidX.Core.App.NotificationCompat.Builder` won't compile for iOS/Windows TFMs, use `dynamic` to defer the type binding to runtime (the customizer is only invoked on Android, so the dynamic call never executes on other platforms):

```csharp
public void Customize(object context, object builder, PushNotificationData notification)
{
    dynamic b = builder;
    b.SetColor(unchecked((int)0xFF0066FF));
}
```

## iOS — Notification Service Extension

On iOS the `PushNotifications` facade forwards the native framework API (`Start()`, `SetNotificationsEnabled()`, `IsNotificationsEnabled()`, `RequestNotificationPermission()`, `SetForegroundListener()`, `SetOpenedListener()`). The bundled `AppAmbitPushNotifications.framework` and `AppAmbit.framework` are copied into the app bundle and signed by the build — there is no CocoaPods or manual setup.

To run code on a push **before the banner is shown** — including when the app is in the background or killed — add a Notification Service Extension (NSE). The NSE is a separate project; iOS is the only platform that supports it (on Android, use `PushNotifications.Android.SetBackgroundListener`).

### 1. Create the NSE project

A `net10.0-ios` app-extension project that references this package:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-ios</TargetFramework>
    <IsAppExtension>true</IsAppExtension>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <SupportedOSPlatformVersion>12.2</SupportedOSPlatformVersion>
    <ApplicationId>com.yourapp.NotificationExtension</ApplicationId>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="com.AppAmbit.PushNotifications" Version="*" />
  </ItemGroup>
</Project>
```

`Info.plist` registers the principal class:

```xml
<key>NSExtension</key>
<dict>
  <key>NSExtensionPointIdentifier</key>
  <string>com.apple.usernotifications.service</string>
  <key>NSExtensionPrincipalClass</key>
  <string>NotificationService</string>
</dict>
```

### 2. Reference the NSE from the app

In the iOS app project, reference the extension as an app extension:

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net10.0-ios'">
  <ProjectReference Include="..\YourApp.NotificationExtension\YourApp.NotificationExtension.csproj">
    <IsAppExtension>true</IsAppExtension>
  </ProjectReference>
</ItemGroup>
```

### 3. Subclass `AppAmbitNotificationServiceExtension`

```csharp
using AppAmbit.PushNotifications;
using Foundation;
using UserNotifications;

[Register("NotificationService")]
public class NotificationService : AppAmbitNotificationServiceExtension
{
    // Runs before the banner is shown, regardless of app state
    // (foreground, background, killed). Mutate `content` to change
    // the title, body, badge, or attachments.
    protected override void HandlePayload(
        AppAmbitNotificationData notification,
        UNMutableNotificationContent content)
    {
        content.Title = $"{notification.Title} ✦";
    }

    // Optional: iOS is about to terminate the extension (30-second limit).
    protected override void OnTimeExpiring() { }
}
```

`HandlePayload` requires `mutable-content: 1` in the APNs payload. The base class handles delivering the final content (and downloading any image attachment) for you.

> **API surface:** `AppAmbitNotificationData` exposes `Title`, `Subtitle`, `Body`, `ImageUrl`, and `Data` (`NSDictionary`). For advanced cases, `AppAmbitNotificationProcessor.Process(...)` is also public if you need to drive processing manually instead of subclassing.
