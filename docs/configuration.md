# Configuration and logging

## App settings

The app uses `appsettings.json` to configure update checks, logging, device hostname mappings, and preset repositories.

Default locations:

- macOS: `$HOME/Library/Application Support/Companion/appsettings.json`
- Windows: `%APPDATA%\Local\Companion\appsettings.json`
- Linux: `~/.config/appsettings.json`

## Logs

Default locations:

- Android: `/data/user/0/org.openipc.Companion/files/.config/companion.log`
- macOS: `$HOME/Library/Application Support/Companion/Logs`
- Windows: `%APPDATA%\Local\Companion\Logs`
- Linux: `~/.config/companion.log`

Serilog configuration reference: https://github.com/serilog/serilog/wiki/Configuration-Basics

## Firmware sources and build channels

The firmware screen reads its sources and build-channel dropdown from the
`FirmwareSources` array in `appsettings.json`. A source can use assets from a
GitHub release or `.tgz` files committed to a repository directory.

```json
"FirmwareSources": [
  {
    "Name": "OpenIPC Builder",
    "Track": "Stable",
    "SelectionMode": "Automatic",
    "Channels": [
      {
        "Name": "Latest",
        "Distribution": "GitHubRelease",
        "Owner": "OpenIPC",
        "Repository": "builder",
        "ReleaseTag": "latest",
        "FileExtension": ".tgz"
      }
    ]
  },
  {
    "Name": "Greg APFPV",
    "Track": "Experimental",
    "SelectionMode": "BySoc",
    "Channels": [
      {
        "Name": "Latest",
        "Distribution": "RepositoryContents",
        "Owner": "sickgreg",
        "Repository": "OpenIPC_sickgregFPV_apfpv",
        "Branch": "main",
        "Path": "",
        "FileExtension": ".tgz"
      }
    ]
  },
  {
    "Name": "Mabur",
    "Track": "Experimental",
    "SelectionMode": "BySoc",
    "Channels": [
      {
        "Name": "Latest",
        "Distribution": "GitHubReleaseList",
        "Owner": "gilankpam",
        "Repository": "openipc-builder",
        "ReleaseTag": "latest",
        "FileExtension": ".tgz"
      }
    ]
  }
]
```

`Track` controls source availability from Preferences. Stable shows only
stable sources on the Firmware tab. Experimental is an opt-in that shows both
stable and experimental sources; the Firmware tab shows a warning while an
experimental source is selected, because those builds are untested and can
brick the device. The earlier track name `Test` is still accepted and read as
`Experimental`. The Firmware tab does not change the track itself.
`SelectionMode` accepts `Automatic` or `BySoc`. `GitHubRelease` channels may
use `latest` or a specific release tag. `GitHubReleaseList` channels list the
repository's 30 most recent published releases in the build-channel dropdown,
newest first, labeled with the publish time and tag; drafts and prereleases
are hidden, and `(latest)` marks the most recently published one. Choosing the
newest entry follows future releases; choosing an older entry keeps that
release selected. If the list cannot be loaded, the channel uses its
`ReleaseTag` (default `latest`). Companion switches the earlier default Mabur
channel to `GitHubReleaseList` once; later edits to the file are kept.
`RepositoryContents` channels may set `Branch` and an optional
repository-relative `Path`. Restart Companion after editing the file.

## Known issue: device hostname mapping

If you see device hostname errors when connecting, update the `DeviceHostnameMapping` section in `appsettings.json`:

```json
"DeviceHostnameMapping": {
  "Camera": [
    "openipc-ssc338q",
    "openipc-ssc30kq"
  ],
  "Radxa": [
    "radxa",
    "raspberrypi"
  ],
  "NVR": [
    "openipc-hi3536dv100"
  ]
}
```
