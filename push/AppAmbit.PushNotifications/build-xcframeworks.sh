#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/Platforms/iOS_Build"
LIBS_DIR="$SCRIPT_DIR/libs"
PODS_PROJ="$BUILD_DIR/Pods/Pods.xcodeproj"

DEVICE_DIR="$BUILD_DIR/build/Release-iphoneos"
SIM_DIR="$BUILD_DIR/build/Release-iphonesimulator"
SCHEMES=(AppAmbitSdk AppAmbitPushNotifications AppAmbitPushNotificationsExtension)
FW_NAMES=(AppAmbit AppAmbitPushNotifications AppAmbitPushNotificationsExtension)

echo "=== Building iOS XCFrameworks (Release, stripped) ==="

# Step 1: pod install if needed
if [ ! -d "$PODS_PROJ" ]; then
  echo "[1/4] Running pod install..."
  (cd "$BUILD_DIR" && pod install --repo-update)
else
  echo "[1/4] Pods already installed."
fi

# Step 2: Build device + simulator for each scheme
echo "[2/4] Building Release frameworks..."
for scheme in "${SCHEMES[@]}"; do
  echo "  Building $scheme (device)..."
  xcodebuild -project "$PODS_PROJ" -scheme "$scheme" -configuration Release \
    -destination "generic/platform=iOS" \
    CONFIGURATION_BUILD_DIR="$DEVICE_DIR" \
    ONLY_ACTIVE_ARCH=NO BUILD_LIBRARY_FOR_DISTRIBUTION=YES \
    SWIFT_COMPILATION_MODE=wholemodule -quiet 2>/dev/null

  echo "  Building $scheme (simulator)..."
  xcodebuild -project "$PODS_PROJ" -scheme "$scheme" -configuration Release \
    -destination "generic/platform=iOS Simulator" \
    CONFIGURATION_BUILD_DIR="$SIM_DIR" \
    ONLY_ACTIVE_ARCH=NO BUILD_LIBRARY_FOR_DISTRIBUTION=YES \
    SWIFT_COMPILATION_MODE=wholemodule -quiet 2>/dev/null
done

# Step 3: Strip debug symbols
echo "[3/4] Stripping symbols..."
for arch_dir in "$DEVICE_DIR" "$SIM_DIR"; do
  for fw in "$arch_dir"/*.framework; do
    name=$(basename "$fw")
    binary="$fw/${name%.framework}"
    [ -f "$binary" ] && strip -x "$binary" 2>/dev/null || true
  done
done

# Step 4: Create XCFrameworks
echo "[4/5] Creating XCFrameworks..."
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

# Step 5: Remove build artifacts not needed at runtime (Swift modules, headers, signatures)
echo "[5/5] Trimming non-runtime artifacts..."
find "$LIBS_DIR" -name "*.swiftmodule" -type d -exec rm -rf {} + 2>/dev/null || true
find "$LIBS_DIR" -name "*.swiftdoc" -delete 2>/dev/null || true
find "$LIBS_DIR" -name "*.swiftsourceinfo" -delete 2>/dev/null || true
find "$LIBS_DIR" -name "_CodeSignature" -type d -exec rm -rf {} + 2>/dev/null || true
find "$LIBS_DIR" -name "Headers" -type d -exec rm -rf {} + 2>/dev/null || true

total=$(du -sh "$LIBS_DIR" | cut -f1)
echo ""
echo "=== Done. Total: $total in $LIBS_DIR ==="
