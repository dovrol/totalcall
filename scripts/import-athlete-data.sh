#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
IMPORTER_PROJECT="${ROOT_DIR}/tools/import-opl/TotalCall.OplImporter/TotalCall.OplImporter.csproj"

COMPETITION_JSON="${1:-src/TotalCall.Client/wwwroot/data/competitions/worlds-2026.json}"
SOURCE="${2:-both}"
TRIGGERED_BY="${TRIGGERED_BY:-local-script}"
DOTNET_CONFIGURATION="${DOTNET_CONFIGURATION:-Release}"

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  cat <<'EOF'
Usage:
  ./scripts/import-athlete-data.sh [competition-json] [both|openipf|openpowerlifting]

Environment:
  SUPABASE_URL
  SUPABASE_SECRET_KEY
  TRIGGERED_BY           Defaults to local-script.
  DOTNET_CONFIGURATION  Defaults to Release.
EOF
  exit 0
fi

if [[ "$COMPETITION_JSON" != /* ]]; then
  COMPETITION_JSON="${ROOT_DIR}/${COMPETITION_JSON}"
fi

if [[ ! -f "$COMPETITION_JSON" ]]; then
  echo "[error] Competition JSON not found: $COMPETITION_JSON" >&2
  exit 1
fi

if [[ -z "${SUPABASE_URL:-}" || -z "${SUPABASE_SECRET_KEY:-}" ]]; then
  echo "[error] SUPABASE_URL and SUPABASE_SECRET_KEY are required." >&2
  exit 1
fi

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

for DATA_SOURCE in "${SOURCES[@]}"; do
  dotnet run \
    --configuration "$DOTNET_CONFIGURATION" \
    --project "$IMPORTER_PROJECT" \
    -- \
    --competition-json "$COMPETITION_JSON" \
    --source "$DATA_SOURCE" \
    --triggered-by "$TRIGGERED_BY"
done
