#!/bin/bash
set -e

# Definir variables (relative to appambit-sdk-maui)
SDK_IOS_PATH="../appambit-sdk-ios"
PUSH_PACKAGE_PATH="$SDK_IOS_PATH/Push/AppAmbitPushNotifications"
OUTPUT_DIR="push/AppAmbit.PushNotifications/libs"
FRAMEWORK_NAME="AppAmbitPushNotifications"

# Verify dirs
if [ ! -d "$PUSH_PACKAGE_PATH" ]; then
    echo "Error: Directory $PUSH_PACKAGE_PATH does not exist."
    exit 1
fi

# Ir al directorio del paquete
cd "$PUSH_PACKAGE_PATH" || exit 1

echo "Building for iOS Simulator..."
xcodebuild archive \
    -scheme $FRAMEWORK_NAME \
    -destination "generic/platform=iOS Simulator" \
    -archivePath "archives/$FRAMEWORK_NAME-Simulator" \
    -derivedDataPath ".build" \
    SKIP_INSTALL=NO \
    BUILD_LIBRARY_FOR_DISTRIBUTION=NO

echo "Building for iOS Device..."
xcodebuild archive \
    -scheme $FRAMEWORK_NAME \
    -destination "generic/platform=iOS" \
    -archivePath "archives/$FRAMEWORK_NAME-Device" \
    -derivedDataPath ".build" \
    SKIP_INSTALL=NO \
    BUILD_LIBRARY_FOR_DISTRIBUTION=NO

echo "Creating XCFramework..."
# Limpiar previo
rm -rf "$FRAMEWORK_NAME.xcframework"

xcodebuild -create-xcframework \
    -framework "archives/$FRAMEWORK_NAME-Simulator.xcarchive/Products/Library/Frameworks/$FRAMEWORK_NAME.framework" \
    -framework "archives/$FRAMEWORK_NAME-Device.xcarchive/Products/Library/Frameworks/$FRAMEWORK_NAME.framework" \
    -output "$FRAMEWORK_NAME.xcframework"

# Copiar al directorio de libs en MAUI
echo "Copying to $OUTPUT_DIR..."

# We are in ../appambit-sdk-ios/Push/AppAmbitPushNotifications
# We need to go back to appambit-sdk-maui/push/AppAmbit.PushNotifications/libs
# Path from here to maui root: ../../../appambit-sdk-maui

DESTINATION="../../../appambit-sdk-maui/$OUTPUT_DIR"

# Ensure dest exists
mkdir -p "$DESTINATION"

rm -rf "$DESTINATION/$FRAMEWORK_NAME.xcframework"
mv "$FRAMEWORK_NAME.xcframework" "$DESTINATION/"

echo "Done! Framework copied to $DESTINATION/$FRAMEWORK_NAME.xcframework"
