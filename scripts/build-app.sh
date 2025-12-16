#!/bin/bash

# Build script for PortKiller.app
set -e

APP_NAME="PortKiller"
BUNDLE_ID="com.portkiller.app"
BUILD_DIR=".build/release"
APP_DIR="$BUILD_DIR/$APP_NAME.app"
CONTENTS_DIR="$APP_DIR/Contents"
MACOS_DIR="$CONTENTS_DIR/MacOS"
RESOURCES_DIR="$CONTENTS_DIR/Resources"

echo "🔨 Building release binary..."
swift build -c release

echo "📦 Creating app bundle..."
rm -rf "$APP_DIR"
mkdir -p "$MACOS_DIR"
mkdir -p "$RESOURCES_DIR"
mkdir -p "$CONTENTS_DIR/Frameworks"

echo "📋 Copying files..."
cp "$BUILD_DIR/$APP_NAME" "$MACOS_DIR/"
cp "Resources/Info.plist" "$CONTENTS_DIR/"

# Copy icon if exists
if [ -f "Resources/AppIcon.icns" ]; then
    cp "Resources/AppIcon.icns" "$RESOURCES_DIR/"
fi

# Copy SPM resource bundle (contains toolbar icons)
if [ -d "$BUILD_DIR/PortKiller_PortKiller.bundle" ]; then
    cp -r "$BUILD_DIR/PortKiller_PortKiller.bundle" "$RESOURCES_DIR/"
fi

# Copy Sparkle framework from artifacts (preserves symlinks)
SPARKLE_FRAMEWORK=".build/artifacts/sparkle/Sparkle/Sparkle.xcframework/macos-arm64_x86_64/Sparkle.framework"
if [ -d "$SPARKLE_FRAMEWORK" ]; then
    echo "📦 Copying Sparkle.framework from artifacts..."
    ditto "$SPARKLE_FRAMEWORK" "$CONTENTS_DIR/Frameworks/Sparkle.framework"

    # Add rpath so executable can find the framework
    echo "🔗 Setting up framework path..."
    install_name_tool -add_rpath "@executable_path/../Frameworks" "$MACOS_DIR/$APP_NAME" 2>/dev/null || true
fi

echo "🔏 Signing app bundle..."
codesign --force --deep --sign - "$APP_DIR"

echo "✅ App bundle created at: $APP_DIR"
echo ""
echo "To install, run:"
echo "  cp -r $APP_DIR /Applications/"
echo ""
echo "Or open directly:"
echo "  open $APP_DIR"
