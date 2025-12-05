#!/bin/bash

# Exit on error
set -e

# Check if a preset name is provided
if [ -z "$1" ]; then
    echo "Usage: $0 \"Preset Name\""
    exit 1
fi

# Convert preset name to lowercase and replace spaces with underscores
PRESET_NAME="$1"
FOLDER_NAME=$(echo "$PRESET_NAME" | tr '[:upper:]' '[:lower:]' | tr ' ' '_')
PRESET_DIR="./$FOLDER_NAME"

# Create the preset directory
mkdir -p "$PRESET_DIR/sensor"

# Create the preset-config.yaml file
cat <<EOF > "$PRESET_DIR/preset-config.yaml"
name: "$PRESET_NAME"
author: "Your Name"
description: "Description of $PRESET_NAME."
category: "FPV"
sensor: ""  # Set sensor file if needed
files:
  wfb.yaml:
    wireless.txpower: "1"
    wireless.channel: "161"
  majestic.yaml:
    fpv.enabled: "false"
    system.logLevel: "debug"
EOF

# Create default configuration files
cat <<EOF > "$PRESET_DIR/wfb.yaml"
wireless:
  txpower: 1
  region: 00
  channel: 161
  mode: HT20
broadcast:
  index: 1
  fec_k: 8
  fec_n: 12
  link_id: 7669206
telemetry:
  index: 1
  router: msposd
  serial: /dev/ttyS2
  osd_fps: 20
  port_rx: 14551
  port_tx: 14555
EOF

cat <<EOF > "$PRESET_DIR/majestic.yaml"
fpv:
  enabled: false
system:
  logLevel: debug
video0:
  bitrate: 4096
records:
  enabled: false
EOF

# Create an empty sensor file as a placeholder
touch "$PRESET_DIR/sensor/.keep"

# Display success message
echo "✅ Preset '$PRESET_NAME' created successfully in '$PRESET_DIR'"
echo "Edit '$PRESET_DIR/preset-config.yaml' to configure the preset."
