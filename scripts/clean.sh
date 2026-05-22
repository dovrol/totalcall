#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

find "$ROOT_DIR" -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +

echo "Usunieto wszystkie katalogi bin i obj."
