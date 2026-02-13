# Universal iOS Build Setup

This setup allows building the AppAmbit iOS Push Notifications SDK without requiring any local clones of other repositories. It uses remote Git references and standalone CocoaPods builds.

## Prerequisites

1.  **Install CocoaPods**:
    ```bash
    sudo gem install cocoapods
    ```

## Build Process (One-Time Setup)

To compile the native iOS frameworks and the .NET bindings, run:

```bash
cd /path/to/appambit-sdk-dotnet
git pull
./build_native_libs.sh
dotnet clean
dotnet build -c Debug -f net10.0-ios
```

### What does `build_native_libs.sh` do?
1.  Downloads the iOS SDK source code directly from GitHub (using `Podfile` with remote `podspec` URLs).
2.  Compiles the frameworks for **Simulator** and **Device** using a standalone CocoaPods workspace.
3.  Places artifacts in `push/AppAmbit.PushNotifications/Platforms/iOS_Build/build/pods`.

### How does .NET binding work?
1.  `AppAmbit.PushNotifications.csproj` references the compiled frameworks from `build/pods`.
2.  `AppAmbit.PushNotifications.targets` automatically copies these frameworks into your App Bundle (`.app/Frameworks`) during build, ensuring they are available at runtime.

## Troubleshooting

-   **"Command not found: pod"**: Run `sudo gem install cocoapods`.
-   **"Framework not found"**: Ensure you ran `./build_native_libs.sh` successfully before building the .NET project.
-   **"Repo not found"**: If prompted for git credentials, ensure you have access to `https://github.com/AppAmbit/appambit-sdk-ios.git`.

## Wrapper Project Structure

-   `Platforms/iOS_Build/Podfile`: Defines remote dependencies.
-   `AppAmbit.PushNotifications.targets`: Handles copying frameworks to app bundle.
-   `build_native_libs.sh`: Orchestrates the native build.
