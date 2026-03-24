#!/bin/zsh

set -euo pipefail

DEVICE_HOST="${OPENIPC_HOST:-192.168.1.10}"
DEVICE_USER="${OPENIPC_USER:-root}"
DEVICE_PASSWORD="${OPENIPC_PASSWORD:-12345}"
OUTPUT_ROOT="${1:-./diagnostics}"
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
OUTPUT_DIR="${OUTPUT_ROOT%/}/openipc-diagnose-${TIMESTAMP}"

mkdir -p "$OUTPUT_DIR"

SSH_BASE_ARGS=(
  -o StrictHostKeyChecking=no
  -o UserKnownHostsFile=/dev/null
  -o ConnectTimeout=5
  -o PreferredAuthentications=password
  -o PubkeyAuthentication=no
)

MAC_COMMANDS=(
  "date"
  "scutil --dns"
  "ifconfig"
  "route -n get default"
  "netstat -rn -f inet"
  "networksetup -listnetworkserviceorder"
  "networksetup -listallhardwareports"
)

REMOTE_SCRIPT='
set -eu

echo "===== date ====="
date

echo "===== uname -a ====="
uname -a

echo "===== ps -ef ====="
ps -ef

echo "===== ip addr ====="
ip addr

echo "===== ip route ====="
ip route

echo "===== netstat -rn ====="
netstat -rn 2>/dev/null || true

echo "===== find /etc -name *.service ====="
find /etc -name "*.service" 2>/dev/null || true

echo "===== /etc/mdns.d ====="
ls -la /etc/mdns.d 2>/dev/null || true

echo "===== service file contents ====="
for file in /etc/mdns.d/*.service; do
  if [ -f "$file" ]; then
    echo "--- $file ---"
    cat "$file"
  fi
done

echo "===== /etc/init.d/S50mdnsd ====="
cat /etc/init.d/S50mdnsd 2>/dev/null || true

echo "===== which mdnsd ====="
which mdnsd 2>/dev/null || true

echo "===== mdnsd strings ====="
strings "$(which mdnsd)" 2>/dev/null | grep -i service || true

echo "===== ps grep services ====="
ps -ef | grep -E "mdnsd|dropbear|udhcp|dnsmasq|majestic" | grep -v grep || true

echo "===== listening ports ====="
netstat -lntp 2>/dev/null || netstat -lnt 2>/dev/null || true

echo "===== /etc/udhcpd.conf ====="
cat /etc/udhcpd.conf 2>/dev/null || true

echo "===== grep 192.168.1.1 /etc ====="
grep -R "192\\.168\\.1\\.1" /etc 2>/dev/null || true

echo "===== grep router /etc ====="
grep -R "router" /etc 2>/dev/null || true

echo "===== logread tail ====="
logread 2>/dev/null | tail -n 300 || true
'

run_mac_command() {
  local label="$1"
  local command="$2"
  local output_file="$OUTPUT_DIR/$label.txt"

  {
    echo "\$ $command"
    echo
    eval "$command"
  } >"$output_file" 2>&1 || true
}

run_remote_command() {
  local output_file="$OUTPUT_DIR/openipc-remote.txt"

  if command -v sshpass >/dev/null 2>&1; then
    SSHPASS="$DEVICE_PASSWORD" sshpass -e ssh "${SSH_BASE_ARGS[@]}" \
      "${DEVICE_USER}@${DEVICE_HOST}" "$REMOTE_SCRIPT" >"$output_file" 2>&1
    return
  fi

  ssh "${SSH_BASE_ARGS[@]}" "${DEVICE_USER}@${DEVICE_HOST}" \
    "$REMOTE_SCRIPT" >"$output_file" 2>&1
}

echo "Writing diagnostics to $OUTPUT_DIR"

for command in "${MAC_COMMANDS[@]}"; do
  label="$(echo "$command" | tr ' /' '__' | tr -cd '[:alnum:]_.-')"
  run_mac_command "$label" "$command"
done

if ping -c 1 -W 1000 "$DEVICE_HOST" >/dev/null 2>&1; then
  echo "Device $DEVICE_HOST is reachable, collecting remote diagnostics"
  if ! run_remote_command; then
    echo "Remote diagnostics failed; see $OUTPUT_DIR/openipc-remote.txt"
  fi
else
  echo "Device $DEVICE_HOST is not reachable" | tee "$OUTPUT_DIR/openipc-remote.txt"
fi

cat >"$OUTPUT_DIR/README.txt" <<EOF
Run completed at: $(date)
Mac diagnostics:
$(printf ' - %s\n' "${MAC_COMMANDS[@]}")

Remote target:
 - host: $DEVICE_HOST
 - user: $DEVICE_USER

Primary files:
 - $OUTPUT_DIR/ifconfig.txt
 - $OUTPUT_DIR/route_-n_get_default.txt
 - $OUTPUT_DIR/netstat_-rn_-f_inet.txt
 - $OUTPUT_DIR/networksetup_-listnetworkserviceorder.txt
 - $OUTPUT_DIR/openipc-remote.txt
EOF

echo "Done. Review $OUTPUT_DIR"
