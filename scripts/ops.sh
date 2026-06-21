#!/usr/bin/env bash
set -euo pipefail

# Thin wrapper around the TotalCall operations CLI.
# Run ./scripts/build.sh first after a clean checkout or code changes.
# Forwards all arguments, e.g.:
#   ./scripts/ops.sh --help
#   ./scripts/ops.sh competition --competition-json <path>
#   ./scripts/ops.sh results --competition-id worlds-2026 --results-json <path>

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CLI_PROJECT="${ROOT_DIR}/ops/cli/TotalCall.Cli/TotalCall.Cli.csproj"

dotnet run --project "$CLI_PROJECT" --no-build -- "$@"
