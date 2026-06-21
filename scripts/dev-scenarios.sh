#!/usr/bin/env bash
set -euo pipefail

SCENARIO="${1:-all-states}"
if [[ "$SCENARIO" == "-h" || "$SCENARIO" == "--help" ]]; then
  cat <<'EOF'
Usage:
  ./scripts/dev-scenarios.sh [scenario] [sync-tool-options]

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

status_env="$(supabase status -o env 2>/dev/null || true)"
api_url="$(printf '%s\n' "$status_env" | sed -n 's/^API_URL="\([^"]*\)".*/\1/p')"
secret_key="$(printf '%s\n' "$status_env" | sed -n 's/^SECRET_KEY="\([^"]*\)".*/\1/p')"
service_role_key="$(printf '%s\n' "$status_env" | sed -n 's/^SERVICE_ROLE_KEY="\([^"]*\)".*/\1/p')"

export SUPABASE_URL="${SUPABASE_URL:-${api_url:-http://127.0.0.1:54321}}"
export SUPABASE_SECRET_KEY="${SUPABASE_SECRET_KEY:-${secret_key:-$service_role_key}}"

if [[ -z "${SUPABASE_SECRET_KEY:-}" ]]; then
  echo "[error] Could not resolve local Supabase secret key. Is supabase start running?" >&2
  exit 1
fi

echo "[info] Applying pending local migrations..."
supabase migration up --local

dotnet run \
  --project ops/cli/TotalCall.Cli/TotalCall.Cli.csproj \
  --no-restore \
  -- scenario "$SCENARIO" \
  --local \
  "$@"
