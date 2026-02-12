# Setup Instructions - iOS Push Notifications Build

## Prerequisites

1. **Clone repositories in parallel:**
   ```
   workspace/
   ├── appambit-sdk-dotnet/
   └── appambit-sdk-ios/
   ```

2. **Install CocoaPods:**
   ```bash
   sudo gem install cocoapods
   ```

## Build Commands (for any team member)

```bash
cd /path/to/appambit-sdk-dotnet
git pull
./build_native_libs.sh
dotnet clean
dotnet build -c Debug -f net10.0-ios
```

## What gets built

- **iOS Simulator frameworks** in `push/AppAmbit.PushNotifications/Platforms/iOS_Build/build/pods/`
- **iOS Device frameworks** in same location
- **.NET iOS assemblies** for MAUI/Avalonia apps

## Troubleshooting

### "BuildProject.xcworkspace does not exist"
- Run `git pull` to get latest `build_native_libs.sh`
- Delete `iOS_Build/Pods/` and re-run `./build_native_libs.sh`

### "AppAmbitPushNotificationsBuild target not found"  
- The Podfile uses target `AppAmbit.App.Swift` which matches the Xcode project
- Run `git pull` to get latest `Podfile`

### CocoaPods errors
- Verify `appambit-sdk-ios` is cloned at same level as `appambit-sdk-dotnet`
- Check paths in `Podfile` relative to your setup

## Files NOT in Git (auto-generated)

- `iOS_Build/Pods/` - CocoaPods dependencies
- `iOS_Build/build/` - Compiled frameworks
- `iOS_Build/*.xcworkspace` - Xcode workspace
- `iOS_Build/Podfile.lock` - Lock file

Only `Podfile` and `.gitignore` are in Git.
