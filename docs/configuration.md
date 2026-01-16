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
