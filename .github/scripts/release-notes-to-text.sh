#!/usr/bin/env bash
# Converts markdown release notes to plain text for latest.json. The in-app update
# dialog shows release_notes as-is, so headings, emphasis, code marks, HTML comments,
# and long pull-request URLs would otherwise appear literally.
# Usage: release-notes-to-text.sh [file]   (reads stdin when no file is given)
set -euo pipefail

cat "${1:-/dev/stdin}" |
  sed -E \
    -e '/<!--.*-->/d' \
    -e 's/^#+[[:space:]]*//' \
    -e 's/\*\*//g' \
    -e 's/`//g' \
    -e 's/^([[:space:]]*)[*-][[:space:]]+/\1• /' \
    -e 's|https://github\.com/[^/]+/[^/]+/pull/([0-9]+)|#\1|g' \
    -e 's/[[:space:]]+$//' |
  cat -s |
  sed -e '/./,$!d'
