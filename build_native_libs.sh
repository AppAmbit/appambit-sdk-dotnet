#!/bin/bash
set -e

CD_DIR="push/AppAmbit.PushNotifications/Platforms/iOS_Build"
cd "$CD_DIR" || exit 1

echo "Installing Pods..."
pod install

echo "Building Frameworks (Simulator)..."
xcodebuild -workspace AppAmbit.xcworkspace \
    -scheme "AppAmbitPushNotifications" \
    -sdk iphonesimulator \
    -configuration Debug \
    -derivedDataPath ./build/pods

echo "Native build complete (Simulator)."

echo "Building Frameworks (Device)..."
xcodebuild -workspace AppAmbit.xcworkspace \
    -scheme "AppAmbitPushNotifications" \
    -sdk iphoneos \
    -configuration Debug \
    -derivedDataPath ./build/pods

echo "Native build complete (Device)."
