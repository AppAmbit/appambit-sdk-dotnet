# AppAmbit Push Notifications SDK (MAUI / Android)

**Seamlessly integrate push notifications with your AppAmbit analytics.**

Extension of the core AppAmbit MAUI SDK for handling Firebase Cloud Messaging (FCM). Android-only for now; iOS coming soon.

---

## Contents
* [Features](#features)
* [Requirements](#requirements)
* [Install](#install)
* [Quickstart](#quickstart)
* [Usage](#usage)
* [Customization](#customization)

---

## Features
* Simple setup after the core SDK.
* Enable/disable notifications at business + FCM level.
* Automatically handles standard FCM notification fields (`title`, `body`, `color`, `icon`, `channel_id`, `click_action`, `image`).
* Permission helper for `POST_NOTIFICATIONS` (Android 13+).
* Optional hook to fully customize the notification.

## Requirements
* .NET 8/9 MAUI targeting Android API 21+.
* Packages:
  * `com.AppAmbit.SdkMaui` (core)
  * `AppAmbit.PushNotifications` (Android)
* Firebase project + `google-services.json` matching your `ApplicationId` (package name).
* For background delivery, send FCM with high priority (`priority: "high"` in legacy or `android.priority: "HIGH"` in HTTP v1). Do **not** put `priority` inside `data`.

## Install
```bash
dotnet add package com.AppAmbit.SdkMaui
dotnet add package AppAmbit.PushNotifications
```

Add Firebase config to your MAUI project file and place the file under `Platforms/Android/`:
```xml
<GoogleServicesJson Include="Platforms/Android/google-services.json" />
```

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

The SDK already applies standard FCM fields. For advanced scenarios, register a customizer and use keys from your `data` payload to change the notification.

```csharp
class Customizer : Java.Lang.Object, PushNotifications.INotificationCustomizer
{
    public void Customize(Context ctx, NotificationCompat.Builder b, AppAmbitNotification n)
    {
        b.SetColor(Android.Graphics.Color.ParseColor("#0066FF"));

        // Example: add an action from custom data keys
        if (n.Data.TryGetValue("action_intent", out var action) &&
            n.Data.TryGetValue("action_title", out var title))
        {
            var intent = new Intent(action);
            var pending = PendingIntent.GetBroadcast(
                ctx, 0, intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            b.AddAction(0, title, pending);
        }
    }
}

PushNotifications.SetNotificationCustomizer(new Customizer());
```

Send any custom keys you need in `data`; `AppAmbitNotification.Data` exposes the full map.
