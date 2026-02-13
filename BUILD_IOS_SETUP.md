# Setup iOS Build (Local Source)

This setup builds the iOS SDK from **local parallel repositories**. This is the standard development setup.

## Prerequisites

1.  **Clone repositories in parallel:**
    ```
    workspace/
    ├── appambit-sdk-dotnet/   (This repo)
    └── appambit-sdk-ios/      (Native iOS Repo)
    ```

## Build Process

Run the following commands to compile everything:

```bash
cd /path/to/appambit-sdk-dotnet
git pull
./build_native_libs.sh
dotnet clean
dotnet build -c Debug -f net10.0-ios
```

### Key Components

1.  **`Platforms/iOS_Build/Podfile`**: Points to `../../appambit-sdk-ios` locally. Uses standalone mode (`integrate_targets => false`) to avoid Xcode project conflicts.
2.  **`build_native_libs.sh`**: Compiles the native frameworks using the local sources.
3.  **`.targets` File**: Automatically copies the compiled frameworks to your App Bundle to prevent runtime crashes.

## Troubleshooting

-   **"Pod spec not found"**: Ensure `appambit-sdk-ios` is cloned next to `appambit-sdk-dotnet`.
-   **"Framework not found"**: Run `./build_native_libs.sh` again.
