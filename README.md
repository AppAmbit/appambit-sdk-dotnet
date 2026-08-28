<picture>
  <source media="(prefers-color-scheme: light)" srcset="https://assets.appambit.com/logo-light.svg">
  <source media="(prefers-color-scheme: dark)" srcset="https://assets.appambit.com/logo-dark.svg">
  <img alt="AppAmbit logo" src="https://assets.appambit.com/logo-dark.svg" width="280">
</picture>

# AppAmbit .NET SDK

**The App Command Center.**
Everything your app needs after you build it, in one connected platform instead of stitching together separate tools.

[![Discord](https://img.shields.io/discord/1418426396836888617?label=Discord&logo=discord&color=5865F2)](https://discord.gg/nJyetYue2s)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![NuGet MAUI](https://img.shields.io/nuget/v/com.AppAmbit.Maui.svg?label=MAUI)](https://www.nuget.org/packages/com.AppAmbit.Maui)
[![NuGet WPF/WinUI](https://img.shields.io/nuget/v/com.AppAmbit.Sdk.svg?label=WPF%2FWinUI)](https://www.nuget.org/packages/com.AppAmbit.Sdk)
[![NuGet Avalonia](https://img.shields.io/nuget/v/com.AppAmbit.Avalonia.svg?label=Avalonia)](https://www.nuget.org/packages/com.AppAmbit.Avalonia)

> One repo, three UI targets. **MAUI**, **WPF/WinUI**, and **Avalonia** ship as separate NuGet packages built from the same core source, so the API below is identical across all three except for how you start the SDK.

---

## Quick start

1. Sign up free at [appambit.com](https://appambit.com), no credit card required
2. Create an app in the dashboard and grab your app key
3. Install the package for your UI stack ([see below](#install))
4. Initialize it at app startup:

**.NET MAUI** (`MauiProgram.cs`)

```csharp
using AppAmbitMaui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseAppAmbit("<YOUR-APPKEY>");

        return builder.Build();
    }
}
```

**WPF / WinUI** (`App.xaml.cs`)

```csharp
using AppAmbit;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppAmbitSdk.Start("<YOUR-APPKEY>");
    }
}
```

**Avalonia** (`App.axaml.cs`)

```csharp
using AppAmbitAvalonia;

public override void OnFrameworkInitializationCompleted()
{
    AppAmbitSdk.Start("<YOUR-APPKEY>");
    base.OnFrameworkInitializationCompleted();
}
```

That's it. Crashes, sessions, and analytics start flowing immediately. Full setup guides live in the [docs](https://docs.appambit.com).

---

## What's inside

### 🚀 Ship
- **Build delivery**: push a build from GitHub, Bitbucket, Azure DevOps, or manually, then send it to team, testers, or clients by email or direct install, and track who installed it
- **Live updates**: ship changes without waiting on an app store review

### 📊 Monitor
- **Crash & error monitoring**: uncaught crashes are captured with full stack traces and threads, then uploaded on the next launch, grouped with who's affected and email alerts on new issues
- **Error logging**: structured log messages with custom properties for quick diagnostics, sent even when the app does not crash
- **Session timeline & breadcrumbs**: automatic page navigation trail so you see exactly what led to a crash
- **Analytics & event tracking**: automatic session starts, stops, and durations plus structured events with custom properties, live and compared across versions

### 📈 Grow
- **Push notifications**: APNs and FCM through the optional `com.AppAmbit.PushNotifications` package, targeted by segment and scheduled from the dashboard
- **Remote config & feature flags**: typed keys (`GetString`, `GetBoolean`, `GetLong`, `GetDouble`) with version targeting, so you can flip features, run gradual rollouts, or hit the kill switch without a release
- **CMS**: define content types and entries in the dashboard, then read articles, FAQs, and promos with a fluent query builder that supports filters, full-text search, sorting, and pagination, decoded straight into your own model classes

### 🗄️ Backend
- **App database**: a managed SQL database with a fluent query builder, batches, and transactions, straight from the SDK or the dashboard
- **Cloud code**: deploy JavaScript functions triggered by HTTP, data events, or manually, then invoke them from the app with typed results, cancellation, and request correlation. Every deploy is a version, so rollback is one click
- **AI agent (MCP)**: build your backend from a conversation with Claude or Cursor ([more below](#built-for-agentic-coding))

### 👥 Teams
- Workspaces, squads, roles and access, per-app reporting

---

## Built for agentic coding

Point Claude or Cursor at the AppAmbit MCP server and it can provision your entire backend from a conversation (content types, database schema, and cloud code functions) while writing the app code that calls them. Paired with a [sample app](#sample-apps) or a [starter app](#starter-apps), that means going from a prompt to a working app with a live backend in a single sitting.

Set it up from the AppAmbit dashboard under **Settings → AI Assistant**, where you create the personal access token and get the connection details for your assistant.

---

## Requirements

* .NET 10.0 SDK or newer
* Visual Studio 2022 (17.6 or newer), JetBrains Rider, or VS Code with the C# Dev Kit
* The **.NET MAUI workload** if you target MAUI (`dotnet workload install maui`)

**Supported target platforms**

| Package | iOS | Android | macOS | Windows | Linux |
| --- | :-: | :-: | :-: | :-: | :-: |
| `com.AppAmbit.Maui` | ✅ | ✅ | ✅ | ✅ | |
| `com.AppAmbit.Sdk` (WPF/WinUI) | ✅ | ✅ | ✅ | ✅ | |
| `com.AppAmbit.Avalonia` | ✅ | ✅ | ✅ | ✅ | ✅ |

Minimum OS versions: iOS 12.2, Android API 21, macOS Catalyst 13.1, Windows 10.0.19041.

---

## Getting started

- [Install](#install)
  - [NuGet](#nuget)
  - [Push setup](#choose-a-push-setup)
- [Track events](#track-events)
- [Logs](#logs)
- [Breadcrumbs](#breadcrumbs)
- [Remote config](#remote-config)
- [Release distribution](#release-distribution)
- [CMS](#cms)
- [Database](#database)
- [Cloud code](#cloud-code)

### Install

#### NuGet

> Requires **v4.2.0 or newer**. Earlier versions do not include Cloud Code support.

Pick the one package that matches your UI stack:

```bash
# .NET MAUI
dotnet add package com.AppAmbit.Maui --version 4.2.0

# WPF / WinUI
dotnet add package com.AppAmbit.Sdk --version 4.2.0

# Avalonia
dotnet add package com.AppAmbit.Avalonia --version 4.2.0
```

Or, using Visual Studio: right-click your project → **Manage NuGet Packages…**, search for **AppAmbit**, and install the one for your stack.

| Package | Namespace | Start with |
|---|---|---|
| `com.AppAmbit.Maui` | `AppAmbitMaui` | `builder.UseAppAmbit(appKey)` |
| `com.AppAmbit.Sdk` | `AppAmbit` | `AppAmbitSdk.Start(appKey)` |
| `com.AppAmbit.Avalonia` | `AppAmbitAvalonia` | `AppAmbitSdk.Start(appKey)` |
| `com.AppAmbit.PushNotifications` | `AppAmbit.PushNotifications` | `PushNotifications.Start()` *(optional)* |

The facades you call afterwards (`Analytics`, `Crashes`, `RemoteConfig`, `Cms`, `AppAmbitDb`, `CloudCode`) live in the `AppAmbit` namespace regardless of which package you installed.

#### Choose a push setup

Push is an add-on package that works alongside any of the three:

```bash
dotnet add package com.AppAmbit.PushNotifications --version 4.2.0
```

Start it after the core SDK:

```csharp
using AppAmbit.PushNotifications;

PushNotifications.Start();
```

Then wire up whatever you need: `SetForegroundListener`, `SetOpenedListener`, `RequestNotificationPermission`, `SetNotificationsEnabled`, `IsNotificationsEnabled`, and `HasNotificationPermission`. On iOS, forward your launch options with `PushNotifications.HandleLaunchOptions(launchOptions)`; on Android, use `PushNotifications.Android.SetBackgroundListener`.

Android needs a `google-services.json` from your Firebase project. iOS needs a push entitlement and an APNs key uploaded to the dashboard. If you want to modify a notification before it is displayed on iOS, add a Notification Service Extension: this repo ships working examples at [samples/AppAmbit.App.Maui.NotificationExtension](samples/AppAmbit.App.Maui.NotificationExtension) and [samples/AppAmbit.App.Avalonia.NotificationExtension](samples/AppAmbit.App.Avalonia.NotificationExtension).

See the [Push Notifications guide](push/AppAmbit.PushNotifications/README.md) for the complete setup.

---

### Usage

Everything below works once the SDK has been started. Session activity (starts, stops, and durations) is tracked automatically, and uncaught crashes are captured and uploaded on the next launch with no extra code.

Most entry points are `async`, so `await` them.

### Track events

Send structured events with custom properties.

```csharp
await Analytics.TrackEvent("Audio started", new Dictionary<string, string>
{
    { "Category", "Music" },
    { "FileName", "favorite.mp3" }
});
```

Also available: `Analytics.SetUserId`, `SetUserEmail`, `StartSession`, `EndSession`, and `EnableManualSession` if you want to drive sessions yourself.

---

### Logs

Add structured log messages for debugging, sent even when the app does not crash.

```csharp
await Crashes.LogError("This code should not be reached");

// With properties and an exception
try
{
    // ...
}
catch (Exception ex)
{
    await Crashes.LogError(ex, new Dictionary<string, string> { { "user_id", "1" } });
}
```

`Crashes.DidCrashInLastSession()` tells you whether the previous run ended in a crash.

---

### Breadcrumbs

Screen-change breadcrumbs are recorded automatically as the user navigates. To display the intended screen name, set the page `Title`. Without a title, the screen appears in the dashboard under the page class name.

```xml
<ContentPage Title="MyPage" ... />
```

On MAUI, both Shell and `NavigationPage` navigation are hooked for you.

---

### Remote config

Fetch and apply remote configuration values asynchronously using type-safe methods.

```csharp
// Enable remote config
RemoteConfig.Enable();

// Get remote config values with type-safe methods
string message = RemoteConfig.GetString("data");
bool isFeatureEnabled = RemoteConfig.GetBoolean("banner");
long discount = RemoteConfig.GetLong("discount");
double maxUpload = RemoteConfig.GetDouble("max_upload");
```

---

### Release distribution

Ship a build to your team, testers, or clients without waiting on a store review. Connect GitHub, Bitbucket, or Azure DevOps so every pipeline run uploads its artifact. Send it out by email or a direct install link, and see who actually installed it.

This repo ships a pipeline for each one that builds, signs, and exports the app, ready to copy into your own:

| CI | Pipeline |
| --- | --- |
| GitHub Actions (Android) | [.github/workflows/build-apk.yml](.github/workflows/build-apk.yml) |
| GitHub Actions (iOS) | [.github/workflows/build-ipa.yml](.github/workflows/build-ipa.yml) |
| Bitbucket Pipelines | [bitbucket-pipelines.yml](bitbucket-pipelines.yml) |
| Azure DevOps (Android) | [azure-pipelines-android.yml](azure-pipelines-android.yml) |
| Azure DevOps (iOS) | [azure-pipelines-ios.yml](azure-pipelines-ios.yml) |

---

### CMS

Read content you publish from the dashboard (articles, FAQs, promos) without shipping a new build. `Cms.Content<T>(contentType)` decodes entries straight into your own model.

```csharp
var posts = await Cms.Content<BlogPost>("blog_extended")
    .Equals("is_published", "true")
    .OrderByDescending("views_count")
    .GetPerPage(20)
    .GetListAsync();
```

Also available: `Search`, `NotEquals`, `Contains`, `StartsWith`, `GreaterThan(OrEqual)`, `LessThan(OrEqual)`, `InList`, `NotInList`, `OrderByAscending`, `GetPage`.

---

### Database

Query, insert, update, and delete rows in your AppAmbit database with a fluent builder.

```csharp
// Query rows
var notes = await AppAmbitDb.From("notes")
    .Where("done", false)
    .OrderByDesc("id")
    .Limit(10)
    .Get();

// Insert a row
await AppAmbitDb.From("notes").Insert(new Dictionary<string, object?>
{
    { "title", "Shopping list" },
    { "done", false }
});

// Update requires at least one Where()
await AppAmbitDb.From("notes")
    .Where("id", 1)
    .Update(new Dictionary<string, object?> { { "done", true } });
```

Use the generic overload to decode rows into your own type:

```csharp
var users = await AppAmbitDb.From<User>("users")
    .Where("age", ">", 18)
    .Get();
```

Also available: `Select`, `OrWhere`, `WhereIn`, `WhereGroup`, `OrderBy`, `Offset`, `First`, `Count`, `Delete`, plus raw SQL through `AppAmbitDb.Execute`, `Batch`, and `BatchInTransaction`. Every terminal accepts an optional `CancellationToken`.

---

### Cloud code

Invoke authenticated HTTP functions hosted by AppAmbit. Cloud Code uses the same consumer and Bearer token as the rest of the SDK, so no extra setup is needed beyond starting the SDK. Configure an active Cloud Function with an enabled HTTP trigger and slug in the dashboard, then call it:

```csharp
using AppAmbit;
using AppAmbit.Enums;

var response = await CloudCode.Call(
    "hello",
    CloudCodeHttpMethod.Post,
    body: new { name = "Ada" });

Console.WriteLine(response.Data);
```

Use the generic overload for a typed result:

```csharp
var result = await CloudCode.Call<Greeting>("hello", CloudCodeHttpMethod.Get);
Console.WriteLine(result.Data?.Greeting);
```

With the dynamic response API, a successful empty body, a `204 No Content` response, and an explicit JSON `null` all surface as `null` in `CloudCodeResponse.Data`. Typed responses preserve their status and request metadata, and an empty successful body produces `null` typed data.

Backend examples are included in [`CloudCodeExamplesAndroid.js`](samples/CloudCode/CloudCodeExamplesAndroid.js) and [`CloudCodeExamplesiOS.js`](samples/CloudCode/CloudCodeExamplesiOS.js).

See the [Cloud Code mobile guide](https://docs.appambit.com/sdk-guides/cloud-code/) for function setup, HTTP triggers, typed and dynamic responses, errors, request IDs, cancellation, timeouts, and backend examples.

---

## Sample apps

This repo ships a sample per UI stack, each exercising every public feature one screen at a time:

| App | Stack | Path |
| --- | --- | --- |
| `AppAmbit.App.Maui` | .NET MAUI | [samples/AppAmbit.App.Maui](samples/AppAmbit.App.Maui) |
| `AppAmbit.App.WPF` | WPF | [samples/AppAmbit.App.WPF](samples/AppAmbit.App.WPF) |
| `AppAmbit.App.Avalonia` | Avalonia (desktop, iOS, Android, browser) | [samples/AppAmbit.App.Avalonia](samples/AppAmbit.App.Avalonia) |
| `AppAmbit.App.Maui.Native.*` | Native iOS / Android / macOS heads | [samples/](samples/) |

Replace `<YOUR-APPKEY>` with a real app key before running them. The Cloud Code screens are backed by the deployable handlers in [samples/CloudCode](samples/CloudCode).

---

## Starter apps

Skip the blank-project setup. Clone a starter with AppAmbit already wired in: auth, push notifications, analytics, and a CMS-driven feed that needs no rebuild to change content. Each one ships with ready-made content sets you can import directly into your AppAmbit dashboard, then customize to make the app your own.

| Starter | Repo |
| --- | --- |
| .NET MAUI | [organization-app-starter-maui](https://github.com/AppAmbit/organization-app-starter-maui) |
| Flutter | [organization-app-starter-flutter](https://github.com/AppAmbit/organization-app-starter-flutter) |
| React Native | [organization-app-starter-react-native](https://github.com/AppAmbit/organization-app-starter-react-native) |

---

## Other SDKs

Open-source, one per platform. Analytics, crashes, session timeline, CMS, database, and remote config all in the same package.

> One .NET SDK repo, three targets: MAUI, WPF/WinUI, and Avalonia each ship as separate packages from the same source.

| Platform | Repo | Package |
| --- | --- | --- |
| **.NET MAUI** *(you are here)* | [appambit-sdk-dotnet](https://github.com/AppAmbit/appambit-sdk-dotnet) | [NuGet](https://www.nuget.org/packages/com.AppAmbit.Maui) |
| **.NET (WPF/WinUI)** *(you are here)* | [appambit-sdk-dotnet](https://github.com/AppAmbit/appambit-sdk-dotnet) | [NuGet](https://www.nuget.org/packages/com.AppAmbit.Sdk) |
| **Avalonia** *(you are here)* | [appambit-sdk-dotnet](https://github.com/AppAmbit/appambit-sdk-dotnet) | [NuGet](https://www.nuget.org/packages/com.AppAmbit.Avalonia) |
| iOS | [appambit-sdk-ios](https://github.com/AppAmbit/appambit-sdk-ios) | [CocoaPods](https://cocoapods.org/pods/appambitsdk) · [Swift Package Manager](https://github.com/AppAmbit/appambit-sdk-ios) |
| Android | [appambit-sdk-android](https://github.com/AppAmbit/appambit-sdk-android) | [Maven Central](https://central.sonatype.com/artifact/com.appambit/appambit) |
| Flutter | [appambit-sdk-flutter](https://github.com/AppAmbit/appambit-sdk-flutter) | [pub.dev](https://pub.dev/packages/appambit_sdk_flutter) |
| React Native | [appambit-sdk-react-native](https://github.com/AppAmbit/appambit-sdk-react-native) | [npm](https://www.npmjs.com/package/appambit) |

---

## REST API

No SDK? No problem. Every capability (sessions, events, logs, breadcrumbs, consumers, CMS, and the database) is also reachable directly over HTTP, for web apps, backend services, or anything without a native SDK.

📖 [Getting started guide](https://docs.appambit.com/Rest/getting-started/)

---

## Troubleshooting

* **No data in dashboard** → check the app key, network access, and that the SDK is started exactly once at startup
* **NuGet restore issues** → run `dotnet restore`, or clear caches with `dotnet nuget locals all --clear`
* **MAUI workload missing** → run `dotnet workload install maui`
* **Crash not appearing** → crashes are sent on next launch
* **Push not arriving** → confirm `google-services.json` (Android) or the APNs key and push entitlement (iOS), and that `PushNotifications.Start()` runs after the core SDK

---

## Documentation

📚 [docs.appambit.com](https://docs.appambit.com)

---

## Community

- 💬 [Discord](https://discord.gg/nJyetYue2s)
- ✉️ [hello@appambit.com](mailto:hello@appambit.com)

---

## Pricing

Free plan with all core features, no credit card required. Paid plans start at $5.99/mo with hard spend caps, so there are no overage surprises.

🔗 [appambit.com](https://appambit.com) · [See pricing](https://appambit.com/pricing)

---

## License

Open source under the MIT License. See the [LICENSE](./LICENSE) file for the full terms.
