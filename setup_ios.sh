#!/bin/bash
# ============================================================
# setup_ios.sh — AppAmbit iOS Framework Bootstrap (optional)
#
# The AppAmbit.PushNotifications.targets file now runs
# pod install + xcodebuild automatically during `dotnet build`.
# Use this script only if you need to pre-build the frameworks
# manually before the first dotnet build.
#
# Usage (from repo root):
#   ./setup_ios.sh
# ============================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
IOS_BUILD_DIR="$SCRIPT_DIR/push/AppAmbit.PushNotifications/Platforms/iOS_Build"
PODS_PROJECT="$IOS_BUILD_DIR/Pods/Pods.xcodeproj"
BUILD_DIR="$IOS_BUILD_DIR/build"

echo ""
echo "AppAmbit iOS Framework Setup"
echo "================================"

# ── 1. Check CocoaPods ──────────────────────────────────────
if ! command -v pod &>/dev/null; then
  echo "CocoaPods not found. Install with: sudo gem install cocoapods"
  exit 1
fi
echo "CocoaPods: $(pod --version)"

# ── 2. pod install ──────────────────────────────────────────
echo ""
echo "Running pod install..."
cd "$IOS_BUILD_DIR"
pod install --repo-update --silent
echo "Pods installed"

# ── 3. Common xcodebuild args ───────────────────────────────
XCODE_COMMON=(
  -project "$PODS_PROJECT"
  -configuration Debug
  CONFIGURATION_BUILD_DIR="$BUILD_DIR/Debug-PLACEHOLDER"
  ONLY_ACTIVE_ARCH=NO
  BUILD_LIBRARY_FOR_DISTRIBUTION=YES
  -quiet
)

build_framework() {
  local SCHEME="$1"
  local PLATFORM="$2"   # "iOS Simulator" or "iOS"
  local DIR_SUFFIX="$3" # "iphonesimulator" or "iphoneos"

  echo "  → $SCHEME ($PLATFORM)..."
  xcodebuild \
    -project "$PODS_PROJECT" \
    -scheme "$SCHEME" \
    -configuration Debug \
    -destination "generic/platform=$PLATFORM" \
    CONFIGURATION_BUILD_DIR="$BUILD_DIR/Debug-$DIR_SUFFIX" \
    ONLY_ACTIVE_ARCH=NO \
    BUILD_LIBRARY_FOR_DISTRIBUTION=YES \
    -quiet
}

# ── 4. Build for iOS Simulator ──────────────────────────────
echo ""
echo "🔨 Building for iOS Simulator..."
build_framework "AppAmbitPushNotifications" "iOS Simulator" "iphonesimulator"
build_framework "AppAmbitPushNotificationsExtension" "iOS Simulator" "iphonesimulator"
build_framework "AppAmbitSdk"              "iOS Simulator" "iphonesimulator"
echo "Simulator frameworks built"

# ── 5. Build for iOS Device ─────────────────────────────────
echo ""
echo "🔨 Building for iOS Device..."
build_framework "AppAmbitPushNotifications" "iOS" "iphoneos"
build_framework "AppAmbitPushNotificationsExtension" "iOS" "iphoneos"
build_framework "AppAmbitSdk"              "iOS" "iphoneos"
echo "Device frameworks built"

# ── 6. Verify output ────────────────────────────────────────
echo ""
echo "🔍 Verifying..."
ALL_OK=true
for FW in \
  "$BUILD_DIR/Debug-iphonesimulator/AppAmbitPushNotifications.framework" \
  "$BUILD_DIR/Debug-iphonesimulator/AppAmbitPushNotificationsExtension.framework" \
  "$BUILD_DIR/Debug-iphonesimulator/AppAmbit.framework" \
  "$BUILD_DIR/Debug-iphoneos/AppAmbitPushNotifications.framework" \
  "$BUILD_DIR/Debug-iphoneos/AppAmbitPushNotificationsExtension.framework" \
  "$BUILD_DIR/Debug-iphoneos/AppAmbit.framework"
do
  if [ -d "$FW" ]; then
    echo " $(basename "$FW") ($(dirname "$FW" | xargs basename))"
  else
    echo "  MISSING: $FW"
    ALL_OK=false
  fi
done

if [ "$ALL_OK" = false ]; then
  echo ""
  echo " Some frameworks are missing. Check the xcodebuild output above."
  exit 1
fi

echo ""
echo " Done! You can now build and run the iOS app:"
echo "   dotnet build samples/AppAmbit.App.Maui/AppAmbit.App.Maui.csproj \\"
echo "     -f net10.0-ios -r iossimulator-arm64"
echo ""
