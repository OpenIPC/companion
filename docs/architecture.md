# Project structure

## Projects

- Companion: shared code (models, services, view models, assets).
- Companion.Desktop: desktop UI host (Windows, macOS, Linux).
- Companion.Android: Android host and platform-specific assets.
- Companion.iOS: iOS host and platform-specific assets.
- Companion.Tests: unit and integration tests.

## Target platforms

| Project | Target platform(s) | Notes |
| --- | --- | --- |
| Companion | Shared | Referenced by platform hosts. |
| Companion.Desktop | Windows, macOS, Linux | Avalonia desktop host. |
| Companion.Android | Android | Uses `net8.0-android`. |
| Companion.iOS | iOS | Uses `net8.0-ios`. |
| Companion.Tests | All | Test harness. |
