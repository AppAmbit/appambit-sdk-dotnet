#!/bin/bash
# ============================================================
# build-ios.sh — Build iOS XCFrameworks for AppAmbit Push SDK
#
# Runs CocoaPods install, builds device + simulator frameworks,
# creates XCFrameworks in push/AppAmbit.PushNotifications/libs/,
# and strips non-runtime artifacts for smaller NuGet packages.
#
# Usage (from repo root):
#   ./build-ios.sh           # Release build (default, for NuGet)
#   ./build-ios.sh --debug   # Debug build (for local development)
# ============================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PUSH_DIR="$SCRIPT_DIR/push/AppAmbit.PushNotifications"
BUILD_DIR="$PUSH_DIR/Platforms/iOS_Build"
LIBS_DIR="$PUSH_DIR/libs"
PODS_PROJ="$BUILD_DIR/Pods/Pods.xcodeproj"

SCHEMES=(AppAmbitSdk AppAmbitPushNotifications AppAmbitPushNotificationsExtension)
FW_NAMES=(AppAmbit AppAmbitPushNotifications AppAmbitPushNotificationsExtension)

CONFIG="Release"
if [[ "${1:-}" == "--debug" ]]; then
  CONFIG="Debug"
fi

DEVICE_DIR="$BUILD_DIR/build/$CONFIG-iphoneos"
SIM_DIR="$BUILD_DIR/build/$CONFIG-iphonesimulator"

echo ""
echo "=== AppAmbit iOS XCFramework Build ($CONFIG) ==="
echo ""

# 1. Check CocoaPods
if ! command -v pod &>/dev/null; then
  echo "ERROR: CocoaPods not found. Install with: sudo gem install cocoapods"
  exit 1
fi
echo "CocoaPods: $(pod --version)"

# 2. pod install
if [ ! -d "$PODS_PROJ" ]; then
  echo "[1/5] Running pod install..."
  (cd "$BUILD_DIR" && pod install --repo-update)
else
  echo "[1/5] Pods already installed."
fi

# 3. Build device + simulator for each scheme
echo "[2/5] Building $CONFIG frameworks..."
for scheme in "${SCHEMES[@]}"; do
  echo "  Building $scheme (device)..."
  xcodebuild -project "$PODS_PROJ" -scheme "$scheme" -configuration "$CONFIG" \
    -destination "generic/platform=iOS" \
    CONFIGURATION_BUILD_DIR="$DEVICE_DIR" \
    ONLY_ACTIVE_ARCH=NO BUILD_LIBRARY_FOR_DISTRIBUTION=YES \
    -quiet 2>/dev/null

  echo "  Building $scheme (simulator)..."
  xcodebuild -project "$PODS_PROJ" -scheme "$scheme" -configuration "$CONFIG" \
    -destination "generic/platform=iOS Simulator" \
    CONFIGURATION_BUILD_DIR="$SIM_DIR" \
    ONLY_ACTIVE_ARCH=NO BUILD_LIBRARY_FOR_DISTRIBUTION=YES \
    -quiet 2>/dev/null
done

# 4. Strip debug symbols
echo "[3/5] Stripping symbols..."
for arch_dir in "$DEVICE_DIR" "$SIM_DIR"; do
  for fw in "$arch_dir"/*.framework; do
    name=$(basename "$fw")
    binary="$fw/${name%.framework}"
    [ -f "$binary" ] && strip -x "$binary" 2>/dev/null || true
  done
done

# 5. Create XCFrameworks
echo "[4/5] Creating XCFrameworks..."
mkdir -p "$LIBS_DIR"
for i in "${!SCHEMES[@]}"; do
  fw_name="${FW_NAMES[$i]}"
  xcfw="$LIBS_DIR/$fw_name.xcframework"
  rm -rf "$xcfw"
  xcodebuild -create-xcframework \
    -framework "$DEVICE_DIR/$fw_name.framework" \
    -framework "$SIM_DIR/$fw_name.framework" \
    -output "$xcfw" 2>/dev/null
  size=$(du -sh "$xcfw" | cut -f1)
  echo "  $fw_name.xcframework: $size"
done

# 6. Trim non-runtime artifacts
echo "[5/5] Trimming non-runtime artifacts..."
find "$LIBS_DIR" -name "*.swiftmodule" -type d -exec rm -rf {} + 2>/dev/null || true
find "$LIBS_DIR" -name "*.swiftdoc" -delete 2>/dev/null || true
find "$LIBS_DIR" -name "*.swiftsourceinfo" -delete 2>/dev/null || true
find "$LIBS_DIR" -name "_CodeSignature" -type d -exec rm -rf {} + 2>/dev/null || true
find "$LIBS_DIR" -name "Headers" -type d -exec rm -rf {} + 2>/dev/null || true
find "$LIBS_DIR" -name "Modules" -type d -exec rm -rf {} + 2>/dev/null || true

# 7. Verify all frameworks exist
ALL_OK=true
for fw_name in "${FW_NAMES[@]}"; do
  xcfw="$LIBS_DIR/$fw_name.xcframework"
  if [ -d "$xcfw" ]; then
    echo "  OK: $fw_name.xcframework"
  else
    echo "  MISSING: $fw_name.xcframework"
    ALL_OK=false
  fi
done

if [ "$ALL_OK" = false ]; then
  echo ""
  echo "ERROR: Some XCFrameworks are missing. Check xcodebuild output above."
  exit 1
fi

total=$(du -sh "$LIBS_DIR" | cut -f1)
echo ""
echo "=== Done. Total: $total in $LIBS_DIR ==="
echo ""
echo "Next steps:"
echo "  dotnet build push/AppAmbit.PushNotifications/AppAmbit.PushNotifications.csproj -f net10.0-ios"
echo "  dotnet pack push/AppAmbit.PushNotifications/AppAmbit.PushNotifications.csproj --configuration Release --output artifacts"
echo ""
