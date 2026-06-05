## Version 4.0.1

### AppAmbit

* **[Refactor]** Removed CMS response caching and pagination logic, more reliable content fetching.

---

## Version 4.0.0

### AppAmbit Push Notifications

* **[Feature]** Added full iOS Push Notifications support for MAUI and Avalonia SDKs, using native `.xcframework` bundles and a companion Notification Service Extension.
* **[Breaking changes]** Scoped Android-only APIs under `PushNotifications.Android` namespace: `SetBackgroundListener`, `SetNotificationCustomizer`, `HandleNotificationOpened`.
* **[Fix]** Fixed notification data conversion issue on Android and Avalonia iOS.
* **[Fix]** Fixed bug where tapped (opened) notifications were not delivered correctly in native .NET apps.
* **[Fix]** Fixed consumer update problem at SDK startup when push token was present.

### AppAmbit

* **[Fix]** Fixed consumer update issue causing duplicate or missed token syncs on startup.

## Version 3.1.0

### AppAmbit

* **[Feature]** Added support for CMS (Content Management System) integration, allowing dynamic content updates and management within the app without requiring app updates. Using fluent API design for easy integration and configuration of CMS features.

## Version 3.0.0

### AppAmbit Push Notifications

* **[Feature]** Added iOS Push Notifications for MAUI and Avalonia SDKs, allowing apps to receive push notifications on iOS devices.

### AppAmbit

* **[Breaking changes]** Added support for new version NET 10 for all SDKs

## Version 2.1.0

### AppAmbit Push Notifications

* **[Feature]** Added Push Notifications SDK for .NET MAUI applications to handle push notifications.

## Version 2.0.2

### AppAmbit

* **[Hotfix]** Fixed problem in method `OnConnectivityChanged` with SDK initialization services in MAUI apps .

## Version 2.0.1

### AppAmbit

* **[Fix]** Fixed issue with MAUI SDK initialization using builder in iOS and Android projects.

## Version 2.0.0

### AppAmbit

* **[Refactor]** Added a shared .NET SDK (netstandard2.0 + .NET 9); MAUI SDK now extends this base.
* **[Feature]** New Avalonia and .NET SDK support.

## Version 1.1.0

### AppAmbit

* **[Feature]** Add Ambit Trail for tracking detailed app and page lifecycle events.
* **[Feature]** Added Windows and Mac Catalyst support.

## Version 1.0.3

### AppAmbit

* **[Fix]** Fixed bug in detecting user app version

## Version 1.0.2

### AppAmbit

* **[Fix]** Fixed errors in early SDK initialization

## Version 1.0.1

### AppAmbit

* **[Feature]** SDK Updated to support all mobile related .NET version 9 frameworks

## Version 1.0.0

### AppAmbit

* **[Feature]** SDK Updated to support .NET version 9

## Version 0.0.4

Remove optional date parameters from public interfaces.

## Version 0.0.3

Description updated

## Version 0.0.2

Production site base url.

## Version 0.0.1

First publish.