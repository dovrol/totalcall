#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
KEYCHAIN_ACCOUNT="${TOTALCALL_SUPABASE_KEYCHAIN_ACCOUNT:-local}"

SCENARIO="${1:-all-states}"
if [[ "$SCENARIO" == "-h" || "$SCENARIO" == "--help" ]]; then
  cat <<'EOF'
Usage:
  ./scripts/dev-scenarios.sh [scenario] [sync-tool-options]

Credentials:
  Loads SUPABASE_URL and SUPABASE_SECRET_KEY from macOS Keychain account
  "local" by default. Override with TOTALCALL_SUPABASE_KEYCHAIN_ACCOUNT.

Scenarios:
  all-states
  open
  open-with-submissions
  locked-no-results
  partial-results
  final-results
  empty
EOF
  exit 0
fi

if [[ $# -gt 0 ]]; then
  shift
fi

if ! command -v supabase >/dev/null 2>&1; then
  echo "[error] supabase CLI is required." >&2
  exit 1
fi

if [[ -z "${SUPABASE_URL:-}" || -z "${SUPABASE_SECRET_KEY:-}" ]]; then
  exec "${SCRIPT_DIR}/with-supabase-keychain.sh" \
    --account "$KEYCHAIN_ACCOUNT" \
    -- \
    "${SCRIPT_DIR}/dev-scenarios.sh" "$SCENARIO" "$@"
fi

if [[ -z "${SUPABASE_SECRET_KEY:-}" ]]; then
  echo "[error] SUPABASE_SECRET_KEY is required." >&2
  exit 1
fi

echo "[info] Applying pending local migrations..."
supabase migration up --local

dotnet run \
  --project "${ROOT_DIR}/ops/cli/TotalCall.Cli/TotalCall.Cli.csproj" \
  --no-restore \
  -- scenario "$SCENARIO" \
  --local \
  "$@"
