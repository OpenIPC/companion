#!/bin/bash
set -euo pipefail

desktop_project="Companion.Desktop/Companion.Desktop.csproj"
tests_project="Companion.Tests/Companion.Tests.csproj"

output_root="build"
publish_root="$output_root/publish"
package_root="$output_root/packages"
verbosity="minimal"
run_tests_flag=true

build_all=false
build_macos=false
build_windows=false
build_linux=false

usage() {
    cat <<EOF
Usage: $0 [all|macos|windows|linux] [-v verbosity] [--skip-tests]

Builds desktop publish outputs and archives them for local testing.

Examples:
  $0 linux
  $0 windows -v normal
  $0 all --skip-tests
EOF
}

ensure_command() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo "Missing required command: $1"
        exit 1
    fi
}

clean_outputs() {
    echo "Cleaning previous desktop outputs..."
    rm -rf "$output_root"
    mkdir -p "$publish_root" "$package_root"
    dotnet clean "$desktop_project" >/dev/null
}

run_tests() {
    if [ "$run_tests_flag" != true ]; then
        echo "Skipping tests."
        return
    fi

    echo "Running desktop test suite..."
    dotnet test "$tests_project" --logger "trx;LogFileName=TestResults.xml"
}

archive_directory() {
    local archive_path="$1"
    local source_dir="$2"
    local source_name
    local archive_dir
    local archive_name

    source_name="$(basename "$source_dir")"
    archive_dir="$(cd "$(dirname "$archive_path")" && pwd)"
    archive_name="$(basename "$archive_path")"
    (
        cd "$(dirname "$source_dir")"
        zip -qry "$archive_dir/$archive_name" "$source_name"
    )
}

publish_desktop() {
    local rid="$1"
    local publish_dir="$publish_root/$rid"

    echo "Publishing $rid..."
    dotnet publish "$desktop_project" \
        -c Release \
        -r "$rid" \
        --self-contained true \
        --output "$publish_dir" \
        -p:PublishSingleFile=true \
        -v "$verbosity"
}

package_linux() {
    local arch="$1"
    local rid="linux-$arch"
    local publish_dir="$publish_root/$rid"
    local package_dir="$package_root/Companion-linux-$arch"
    local archive_path="$package_root/Companion-linux-$arch.zip"

    publish_desktop "$rid"

    rm -rf "$package_dir"
    mkdir -p "$package_dir"
    cp -R "$publish_dir"/. "$package_dir"/

    if [ -f "$package_dir/Companion.Desktop" ]; then
        mv "$package_dir/Companion.Desktop" "$package_dir/Companion.DesktopApp"
        chmod +x "$package_dir/Companion.DesktopApp"
    fi

    archive_directory "$archive_path" "$package_dir"
    echo "Created $archive_path"
}

package_windows() {
    local arch="$1"
    local rid="win-$arch"
    local package_dir="$package_root/Companion-windows-$arch"
    local archive_path="$package_root/Companion-windows-$arch.zip"

    publish_desktop "$rid"

    rm -rf "$package_dir"
    mkdir -p "$package_dir"
    cp -R "$publish_root/$rid"/. "$package_dir"/

    archive_directory "$archive_path" "$package_dir"
    echo "Created $archive_path"
}

create_macos_app_bundle() {
    local publish_dir="$1"
    local app_dir="$2"

    rm -rf "$app_dir"
    mkdir -p "$app_dir/Contents/MacOS" "$app_dir/Contents/Resources"

    cp -R "$publish_dir"/. "$app_dir/Contents/MacOS"/
    cp "Companion/Assets/Icons/OpenIPC.icns" "$app_dir/Contents/Resources/Companion.icns"

    if [ -f "$app_dir/Contents/MacOS/Companion.Desktop" ]; then
        chmod +x "$app_dir/Contents/MacOS/Companion.Desktop"
    fi

    cat > "$app_dir/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>Companion</string>
    <key>CFBundleDisplayName</key>
    <string>Companion</string>
    <key>CFBundleExecutable</key>
    <string>Companion.Desktop</string>
    <key>CFBundleIdentifier</key>
    <string>com.openipc.Companion</string>
    <key>CFBundleVersion</key>
    <string>1.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>LSMinimumSystemVersion</key>
    <string>13.0</string>
    <key>CFBundleIconFile</key>
    <string>Companion.icns</string>
</dict>
</plist>
EOF
}

package_macos() {
    local rid="osx-arm64"
    local publish_dir="$publish_root/$rid"
    local package_dir="$package_root/Companion-macos-arm64"
    local app_dir="$package_dir/Companion.app"
    local archive_path="$package_root/Companion-macos-arm64.zip"

    publish_desktop "$rid"

    rm -rf "$package_dir"
    mkdir -p "$package_dir"
    create_macos_app_bundle "$publish_dir" "$app_dir"

    archive_directory "$archive_path" "$package_dir"
    echo "Created $archive_path"
}

parse_args() {
    if [ $# -eq 0 ]; then
        usage
        exit 1
    fi

    while [[ $# -gt 0 ]]; do
        case "$1" in
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
            --skip-tests)
                run_tests_flag=false
                ;;
            -v|--verbosity)
                shift
                if [ $# -eq 0 ]; then
                    echo "Missing verbosity value."
                    exit 1
                fi
                verbosity="$1"
                ;;
            -h|--help)
                usage
                exit 0
                ;;
            *)
                echo "Unknown option: $1"
                usage
                exit 1
                ;;
        esac
        shift
    done
}

main() {
    ensure_command dotnet
    ensure_command zip

    parse_args "$@"
    clean_outputs
    run_tests

    if [ "$build_all" = true ] || [ "$build_linux" = true ]; then
        package_linux "arm64"
        package_linux "x64"
    fi

    if [ "$build_all" = true ] || [ "$build_windows" = true ]; then
        package_windows "arm64"
        package_windows "x64"
    fi

    if [ "$build_all" = true ] || [ "$build_macos" = true ]; then
        package_macos
    fi

    echo
    echo "Desktop publish outputs: $publish_root"
    echo "Packaged artifacts: $package_root"
}

main "$@"
