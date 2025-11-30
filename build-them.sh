#!/bin/bash 


# Define the project names and paths
desktop_project="Companion.Desktop"
android_project="Companion.Android"
ios_project="Companion.iOS"

# Build output directory
output_dir="build"

# Default verbosity level
verbosity="normal"

# Function to clean previous builds
clean_builds() {
    echo "Cleaning previous builds in output directory..."
    rm -rf "$output_dir"
    echo "Running dotnet clean for each project..."
    dotnet clean $desktop_project
    dotnet clean $android_project
    dotnet clean $ios_project
    mkdir -p "$output_dir"
}

run_tests() {
    echo "Running tests..."
    dotnet test --logger "trx;LogFileName=TestResults.xml"
}
# Function to create macOS .app bundle
create_macos_app_bundle() {
    app_name="Companion"
    src="$output_dir/$desktop_project/osx-arm64"
    app_bundle="$output_dir/$desktop_project/osx-arm64/$app_name.app"

    echo "Creating .app bundle at $app_bundle..."
    rm -rf "$app_bundle"
    mkdir -p "$app_bundle/Contents/MacOS" "$app_bundle/Contents/Resources"

    # Icon (adjust path if needed)
    cp "Companion/Assets/Icons/OpenIPC.icns" "$app_bundle/Contents/Resources/$app_name.icns" 2>/dev/null || true

    # Copy published payload into MacOS
    cp -R "$src/"* "$app_bundle/Contents/MacOS/"

    # Ensure the apphost is executable; if framework-dependent, use the dll runner
    if [ -f "$app_bundle/Contents/MacOS/Companion.Desktop" ]; then
        chmod +x "$app_bundle/Contents/MacOS/Companion.Desktop"
    fi

    # Info.plist
    cat > "$app_bundle/Contents/Info.plist" <<EOL
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>$app_name</string>
    <key>CFBundleDisplayName</key>
    <string>$app_name</string>
    <key>CFBundleExecutable</key>
    <string>Companion.Desktop</string>
    <key>CFBundleIdentifier</key>
    <string>com.openipc.$app_name</string>
    <key>CFBundleVersion</key>
    <string>1.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>LSMinimumSystemVersion</key>
    <string>13.0</string>
    <key>CFBundleIconFile</key>
    <string>$app_name.icns</string>
</dict>
</plist>
EOL

    echo "$app_name.app created: $app_bundle"
}

# Function to build for macOS with verbose output
build_macos() {
    echo "Building $desktop_project for macOS (osx-arm64) as .app bundle..."
    out="$output_dir/$desktop_project/osx-arm64"
    dotnet publish "$desktop_project" -c Release -r osx-arm64 \
      --output "$out" --self-contained true -v "$verbosity" \
      -p:PublishSingleFile=true -p:UseAppHost=true

    # Expect a native apphost when self-contained single-file
    apphost="$out/Companion.Desktop"
    dll="$out/Companion.Desktop.dll"

    if [ -f "$apphost" ] || [ -f "$dll" ]; then
        create_macos_app_bundle
    else
        echo "Error: macOS publish didn't produce Companion.Desktop or Companion.Desktop.dll in $out"
        ls -la "$out"
    fi
}

# Function to build for Windows
build_windows() {
    echo "Building $desktop_project for win-arm64..."
    dotnet publish $desktop_project -c Release -r win-arm64 --output "$output_dir/$desktop_project/win-arm64" --self-contained -v $verbosity -p:PublishSingleFile=true

    echo "Building $desktop_project for win-x64..."
    dotnet publish $desktop_project -c Release -r win-x64 --output "$output_dir/$desktop_project/win-x64" --self-contained -v $verbosity -p:PublishSingleFile=true
}

# Function to build for Linux
build_linux() {
    echo "Building $desktop_project for linux-arm64..."
    dotnet publish $desktop_project -c Release -r linux-arm64 --output "$output_dir/$desktop_project/linux-arm64" --self-contained -v $verbosity -p:PublishSingleFile=true

    echo "Building $desktop_project for linux-x64..."
    dotnet publish $desktop_project -c Release -r linux-x64 --output "$output_dir/$desktop_project/linux-x64" --self-contained -v $verbosity -p:PublishSingleFile=true
}

# Function to build for Android
build_android() {
    echo "Building $android_project as APK..."
    dotnet publish $android_project -c Release -r android-arm64 --output "$output_dir/$android_project" -v $verbosity
}

# Function to build for iOS
build_ios() {
    echo "Building $ios_project as an IPA for iOS..."
    dotnet publish $ios_project -c Release -r ios-arm64 --output "$output_dir/$ios_project" -v $verbosity
}

# Parse arguments
build_all=false
while [[ $# -gt 0 ]]; do
    case $1 in
        all)
            build_all=true
            ;;
        macos)
            build_macos=true
            ;;
        windows)
            build_windows=true
            ;;
        linux)
            build_linux=true
            ;;
        android)
            build_android=true
            ;;
        ios)
            build_ios=true
            ;;
        -v|--verbosity)
            shift
            verbosity="$1"
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [all|macos|windows|linux|android|ios] [-v verbosity_level]"
            echo "Verbosity levels: quiet, minimal, normal, detailed, diagnostic"
            exit 1
            ;;
    esac
    shift
done

# Clean previous builds
clean_builds
run_tests

# Execute builds based on the selected options
if [ "$build_all" = true ] || [ "$build_macos" = true ]; then
    build_macos
fi

if [ "$build_all" = true ] || [ "$build_windows" = true ]; then
    build_windows
fi

if [ "$build_all" = true ] || [ "$build_linux" = true ]; then
    build_linux
fi

if [ "$build_all" = true ] || [ "$build_android" = true ]; then
    build_android
fi

if [ "$build_all" = true ] || [ "$build_ios" = true ]; then
    build_ios
fi

echo "Build process complete. Outputs are in the '$output_dir' directory."
