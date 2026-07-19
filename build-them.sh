#!/bin/bash
set -euo pipefail

# Local developer packaging helper only.
# Use this to validate cross-platform build outputs before pushing changes.
# GitHub Actions remains the source of truth for CI builds, release artifacts, and published releases.

desktop_project="Companion.Desktop/Companion.Desktop.csproj"
tests_project="Companion.Tests/Companion.Tests.csproj"
app_name="Companion"
output_root="build"
publish_root="$output_root/publish"
package_root="$output_root/packages"
verbosity="minimal"
run_tests_flag=true
release_version=""

build_all=false
build_macos=false
build_windows=false
build_linux=false

usage() {
    cat <<EOF
Usage: $0 [all|macos|windows|linux ...] [-v verbosity] [--skip-tests]

Builds desktop publish outputs for local developer testing and packages them to match GitHub Actions as closely as possible.
GitHub Actions is still the source of truth for official build and release artifacts.

Examples:
  $0
  $0 linux
  $0 windows macos -v normal
  $0 all --skip-tests
EOF
}

ensure_command() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo "Missing required command: $1"
        exit 1
    fi
}

derive_release_version() {
    local head_tag
    head_tag="$(git tag --points-at HEAD | grep -E '^(release-v|v)' | head -n 1 || true)"

    if [[ -n "$head_tag" ]]; then
        if [[ "$head_tag" == release-v* ]]; then
            release_version="${head_tag#release-v}"
        elif [[ "$head_tag" == v* ]]; then
            release_version="${head_tag#v}"
        fi
    fi

    if [[ -z "$release_version" ]]; then
        release_version="0.0.0-dev+$(git rev-parse --short HEAD)"
    fi
}

clean_outputs() {
    echo "Cleaning previous desktop outputs..."
    rm -rf "$output_root"
    mkdir -p "$publish_root" "$package_root"
    dotnet clean "$desktop_project" >/dev/null
}

restore_dependencies() {
    echo "Restoring desktop project..."
    dotnet restore "$desktop_project" -v "$verbosity"
}

restore_for_publish() {
    local rid="$1"
    local os_family="$2"

    echo "Restoring desktop project for $rid..."
    if [[ "$os_family" == "windows" ]]; then
        dotnet restore "$desktop_project" \
            -r "$rid" \
            -p:PublishReadyToRun=true \
            -v "$verbosity"
    else
        dotnet restore "$desktop_project" \
            -r "$rid" \
            -v "$verbosity"
    fi
}

build_desktop() {
    echo "Building desktop project..."
    dotnet build "$desktop_project" \
        --configuration Release \
        --no-restore \
        -v "$verbosity"
}

run_tests() {
    if [[ "$run_tests_flag" != true ]]; then
        echo "Skipping tests."
        return
    fi

    echo "Running desktop test suite..."
    dotnet test "$tests_project" \
        --configuration Release \
        --logger "trx;LogFileName=TestResults.xml" \
        -v "$verbosity"
}

write_version_file() {
    local publish_dir="$1"
    mkdir -p "$publish_dir"
    printf 'v%s\n' "$release_version" > "$publish_dir/VERSION"
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

publish_linux() {
    local arch="$1"
    local rid="linux-$arch"
    local publish_dir="$publish_root/$rid"
    local package_dir="$package_root/Companion-linux-$arch"
    local archive_path="$package_root/Companion-linux-$arch.zip"

    echo "Publishing $rid..."
    rm -rf "$publish_dir" "$package_dir"
    restore_for_publish "$rid" "linux"

    dotnet publish "$desktop_project" \
        -c Release \
        -r "$rid" \
        --self-contained true \
        --output "$publish_dir" \
        --no-restore \
        -p:PublishSingleFile=true \
        -v "$verbosity"

    write_version_file "$publish_dir"

    mkdir -p "$package_dir"
    cp -R "$publish_dir"/. "$package_dir"/

    if [[ -f "$package_dir/Companion.Desktop" ]]; then
        mv "$package_dir/Companion.Desktop" "$package_dir/Companion.DesktopApp"
        chmod +x "$package_dir/Companion.DesktopApp"
    fi

    archive_directory "$archive_path" "$package_dir"
    echo "Created $archive_path"
}

publish_windows() {
    local arch="$1"
    local rid="win-$arch"
    local publish_dir="$publish_root/$rid"
    local package_dir="$package_root/Companion-windows-$arch"
    local archive_path="$package_root/Companion-windows-$arch.zip"

    echo "Publishing $rid..."
    rm -rf "$publish_dir" "$package_dir"
    restore_for_publish "$rid" "windows"

    dotnet publish "$desktop_project" \
        -c Release \
        -r "$rid" \
        --self-contained true \
        --output "$publish_dir" \
        --no-restore \
        -p:PublishSingleFile=false \
        -p:PublishReadyToRun=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -v "$verbosity"

    write_version_file "$publish_dir"

    mkdir -p "$package_dir"
    cp -R "$publish_dir"/. "$package_dir"/

    archive_directory "$archive_path" "$package_dir"
    echo "Created $archive_path"
}

create_macos_app_bundle() {
    local publish_dir="$1"
    local app_dir="$2"

    rm -rf "$app_dir"
    mkdir -p "$app_dir/Contents/MacOS" "$app_dir/Contents/Resources"

    if command -v ditto >/dev/null 2>&1; then
        ditto "$publish_dir" "$app_dir/Contents/MacOS"
    else
        cp -R "$publish_dir"/. "$app_dir/Contents/MacOS"/
    fi
    [[ -f "Companion/Assets/Icons/OpenIPC.icns" ]] && cp "Companion/Assets/Icons/OpenIPC.icns" "$app_dir/Contents/Resources/OpenIPC.icns"

    if [[ -f "$app_dir/Contents/MacOS/Companion.Desktop" ]]; then
        chmod +x "$app_dir/Contents/MacOS/Companion.Desktop"
    fi

    cat > "$app_dir/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>${app_name}</string>
    <key>CFBundleDisplayName</key>
    <string>${app_name}</string>
    <key>CFBundleExecutable</key>
    <string>Companion.Desktop</string>
    <key>CFBundleIdentifier</key>
    <string>org.openipc.Companion</string>
    <key>CFBundleVersion</key>
    <string>${release_version}</string>
    <key>CFBundleShortVersionString</key>
    <string>${release_version}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>LSMinimumSystemVersion</key>
    <string>13.0</string>
    <key>CFBundleIconFile</key>
    <string>OpenIPC.icns</string>
</dict>
</plist>
EOF
}

package_macos_dmg() {
    local package_dir="$1"
    local dmg_path="$2"
    local app_dir="$package_dir/Companion.app"

    rm -f "$dmg_path"
    rm -rf "$package_dir/dmg_build"
    mkdir -p "$package_dir/dmg_build"

    if command -v ditto >/dev/null 2>&1; then
        ditto "$app_dir" "$package_dir/dmg_build/Companion.app"
    else
        cp -R "$app_dir" "$package_dir/dmg_build/Companion.app"
    fi
    ln -s /Applications "$package_dir/dmg_build/Applications"

    hdiutil create \
        -volname "Companion" \
        -srcfolder "$package_dir/dmg_build" \
        -ov \
        -format UDZO \
        -fs HFS+ \
        "$dmg_path" >/dev/null

    rm -rf "$package_dir/dmg_build"
}

publish_macos() {
    local arch="$1"
    local rid="osx-$arch"
    local publish_dir="$publish_root/$rid"
    local package_dir="$package_root/Companion-macos-$arch"
    local app_dir="$package_dir/Companion.app"
    local dmg_path="$package_root/Companion-macos-$arch.dmg"
    local zip_path="$package_root/Companion-macos-$arch.zip"

    echo "Publishing $rid..."
    rm -rf "$publish_dir" "$package_dir"
    rm -f "$dmg_path" "$zip_path"
    restore_for_publish "$rid" "macos"

    dotnet publish "$desktop_project" \
        -c Release \
        -r "$rid" \
        --self-contained true \
        --output "$publish_dir" \
        --no-restore \
        -p:PublishSingleFile=false \
        -p:PublishReadyToRun=true \
        -p:UseAppHost=true \
        -v "$verbosity"

    write_version_file "$publish_dir"
    mkdir -p "$package_dir"
    create_macos_app_bundle "$publish_dir" "$app_dir"

    if command -v hdiutil >/dev/null 2>&1; then
        package_macos_dmg "$package_dir" "$dmg_path"
        echo "Created $dmg_path"
    else
        archive_directory "$zip_path" "$package_dir"
        echo "Created $zip_path"
        echo "hdiutil not available; packaged macOS app as zip instead of dmg."
    fi
}

parse_args() {
    if [[ $# -eq 0 ]]; then
        build_all=true
        return
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
                if [[ $# -eq 0 ]]; then
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

    if [[ "$build_all" != true && "$build_macos" != true && "$build_windows" != true && "$build_linux" != true ]]; then
        build_all=true
    fi
}

main() {
    ensure_command dotnet
    ensure_command zip
    ensure_command git

    parse_args "$@"
    derive_release_version
    clean_outputs
    restore_dependencies
    build_desktop
    run_tests

    if [[ "$build_all" = true || "$build_linux" = true ]]; then
        publish_linux "x64"
        publish_linux "arm64"
    fi

    if [[ "$build_all" = true || "$build_windows" = true ]]; then
        publish_windows "x64"
        publish_windows "arm64"
    fi

    if [[ "$build_all" = true || "$build_macos" = true ]]; then
        publish_macos "x64"
        publish_macos "arm64"
    fi

    echo
    echo "Release version: v$release_version"
    echo "Desktop publish outputs: $publish_root"
    echo "Packaged artifacts: $package_root"
}

main "$@"
