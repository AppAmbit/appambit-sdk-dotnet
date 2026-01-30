#!/bin/bash
set -e

CD_DIR="push/AppAmbit.PushNotifications/Platforms/iOS_Build"
cd "$CD_DIR" || exit 1

echo "Installing Pods..."
pod install

echo "Building Frameworks (Simulator)..."
xcodebuild -workspace BuildProject.xcworkspace \
    -scheme "Pods-AppAmbitPushNotificationsBuild" \
    -sdk iphonesimulator \
    -configuration Debug \
    -derivedDataPath ./build/pods

echo "Native build complete (Simulator)."

echo "Building Frameworks (Device)..."
xcodebuild -workspace BuildProject.xcworkspace \
    -scheme "Pods-AppAmbitPushNotificationsBuild" \
    -sdk iphoneos \
    -configuration Debug \
    -derivedDataPath ./build/pods

echo "Native build complete (Device)."
