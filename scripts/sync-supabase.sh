#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
CLI_PROJECT="${ROOT_DIR}/ops/cli/TotalCall.Cli/TotalCall.Cli.csproj"
RESULTS_DIR="${ROOT_DIR}/ops/data/results"
KEYCHAIN_ACCOUNT="${TOTALCALL_SUPABASE_KEYCHAIN_ACCOUNT:-production}"

COMPETITION_JSON="${1:-src/TotalCall.Client/wwwroot/data/competitions/worlds-2026.json}"
SOURCE="${2:-both}"
RESULTS="${3:-auto}"
TRIGGERED_BY="${TRIGGERED_BY:-local-script}"
DOTNET_CONFIGURATION="${DOTNET_CONFIGURATION:-Release}"

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  cat <<'EOF'
Usage:
  ./scripts/sync-supabase.sh [competition-json] [both|openipf|openpowerlifting] [auto|none|results-json]

Syncs the competition definition (metadata + versioned config) and then the
athlete history for the requested source(s) into Supabase.
If matching official results JSON files exist under ops/data/results,
they are imported after athlete history by default.

Environment:
  SUPABASE_URL          Optional override. Otherwise read from macOS Keychain.
  SUPABASE_SECRET_KEY   Optional override. Otherwise read from macOS Keychain.
  TOTALCALL_SUPABASE_KEYCHAIN_ACCOUNT
                         Keychain account to use. Defaults to production.
  TRIGGERED_BY           Defaults to local-script.
  DOTNET_CONFIGURATION   Defaults to Release.
EOF
  exit 0
fi

if [[ "$COMPETITION_JSON" != /* ]]; then
  COMPETITION_JSON="${ROOT_DIR}/${COMPETITION_JSON}"
fi

COMPETITION_ID="$(basename "$COMPETITION_JSON" .json)"

if [[ ! -f "$COMPETITION_JSON" ]]; then
  echo "[error] Competition JSON not found: $COMPETITION_JSON" >&2
  exit 1
fi

if [[ -z "${SUPABASE_URL:-}" || -z "${SUPABASE_SECRET_KEY:-}" ]]; then
  exec "${SCRIPT_DIR}/with-supabase-keychain.sh" \
    --account "$KEYCHAIN_ACCOUNT" \
    -- \
    "${SCRIPT_DIR}/sync-supabase.sh" "$@"
fi

dotnet build "$CLI_PROJECT" \
  --configuration "$DOTNET_CONFIGURATION" \
  --no-restore \
  >/dev/null

case "$SOURCE" in
  both)
    SOURCES=(openipf openpowerlifting)
    ;;
  openipf|openpowerlifting)
    SOURCES=("$SOURCE")
    ;;
  *)
    echo "[error] Unknown source: $SOURCE" >&2
    echo "        Expected: both, openipf, or openpowerlifting." >&2
    exit 1
    ;;
esac

# Sync the competition definition first so it exists before athlete data and before
# any submission references it.
dotnet run \
  --configuration "$DOTNET_CONFIGURATION" \
  --project "$CLI_PROJECT" \
  --no-build \
  -- \
  competition \
  --competition-json "$COMPETITION_JSON" \
  --triggered-by "$TRIGGERED_BY"

for DATA_SOURCE in "${SOURCES[@]}"; do
  dotnet run \
    --configuration "$DOTNET_CONFIGURATION" \
    --project "$CLI_PROJECT" \
    --no-build \
    -- \
    athletes \
    --competition-json "$COMPETITION_JSON" \
    --source "$DATA_SOURCE" \
    --triggered-by "$TRIGGERED_BY"
done

import_results() {
  local results_json="$1"

  dotnet run \
    --configuration "$DOTNET_CONFIGURATION" \
    --project "$CLI_PROJECT" \
    --no-build \
    -- \
    results \
    --competition-id "$COMPETITION_ID" \
    --results-json "$results_json" \
    --triggered-by "$TRIGGERED_BY"
}

case "$RESULTS" in
  none)
    ;;
  auto)
    mapfile -t RESULTS_FILES < <(
      find "${RESULTS_DIR}" \
        -maxdepth 1 \
        -type f \
        -name "${COMPETITION_ID}-*.json" \
        2>/dev/null | sort
    )

    for RESULTS_JSON in "${RESULTS_FILES[@]}"; do
      import_results "$RESULTS_JSON"
    done
    ;;
  *)
    if [[ "$RESULTS" != /* ]]; then
      RESULTS="${ROOT_DIR}/${RESULTS}"
    fi

    if [[ ! -f "$RESULTS" ]]; then
      echo "[error] Results JSON not found: $RESULTS" >&2
      exit 1
    fi

    import_results "$RESULTS"
    ;;
esac
