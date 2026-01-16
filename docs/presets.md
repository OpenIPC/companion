# Presets

Presets are being redesigned. Expect changes to format and behavior.

## 1. Folder Structure

Each preset is a self-contained folder under the `presets/` directory. It includes:

- **`preset-config.yaml`**: Defines preset metadata, attributes, modified files and updated data.
- **YAML configuration files** (e.g., `wfb.yaml`, `majestic.yaml`).
- **Conf configuration files** (e.g., `wfb.conf`, `telemetry.conf`).
- **Optional `sensor/` folder**: Not supported yet

**Example Structure**:
```
presets/
├── high_power_fpv/
│   ├── preset-config.yaml
│   ├── sensor/
│       └── milos-sensor.bin
```

---

## 2. Preset definition (`preset-config.yaml`)

The `preset-config.yaml` file defines:

- **Metadata**: `name`, `author`, `description`, and `category`.
- **Optional Sensor**: Specifies a binary file (e.g., `milos-sensor.bin`) to be transferred to the remote device.
- **Files**: Specifies files and their key-value modifications.

**Example**:
```yaml
name: "High Power FPV"
author: "OpenIPC"
description: "Optimized settings for high-power FPV."
sensor: "milos-sensor.bin"
files:
  wfb.yaml:
    wireless.txpower: "30"
    wireless.channel: "161"
  majestic.yaml:
    fpv.enabled: "true"
    system.logLevel: "info"
```

---

## 3. Preset loading
1) Submit a pull request: https://github.com/OpenIPC/fpv-presets
2) Local:
   - The application scans the `presets/` directory.
   - It parses each `preset-config.yaml` to create a `Preset` object.
   - File modifications are transformed into a bindable `ObservableCollection<FileModification>` for the UI.
