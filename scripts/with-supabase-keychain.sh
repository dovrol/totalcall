#!/usr/bin/env bash
set -euo pipefail

ACCOUNT="${TOTALCALL_SUPABASE_KEYCHAIN_ACCOUNT:-production}"
URL_SERVICE="${TOTALCALL_SUPABASE_URL_SERVICE:-totalcall.supabase.url}"
KEY_SERVICE="${TOTALCALL_SUPABASE_SECRET_KEY_SERVICE:-totalcall.supabase.secret-key}"

usage() {
  cat <<'EOF'
Usage:
  ./scripts/with-supabase-keychain.sh [--account name] -- <command> [args...]

Runs a command with SUPABASE_URL and SUPABASE_SECRET_KEY loaded from macOS
Keychain for that process only.

Examples:
  ./scripts/with-supabase-keychain.sh -- ./scripts/ops.sh --help
  ./scripts/with-supabase-keychain.sh -- dotnet run --project ops/mcp/TotalCall.Mcp/TotalCall.Mcp.csproj --no-build

Defaults:
  account: production
  URL service: totalcall.supabase.url
  secret service: totalcall.supabase.secret-key

Environment overrides:
  TOTALCALL_SUPABASE_KEYCHAIN_ACCOUNT
  TOTALCALL_SUPABASE_URL_SERVICE
  TOTALCALL_SUPABASE_SECRET_KEY_SERVICE
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --account)
      ACCOUNT="${2:-}"
      shift 2
      ;;
    --)
      shift
      break
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      break
      ;;
  esac
done

if [[ $# -eq 0 ]]; then
  echo "[error] Command is required." >&2
  usage >&2
  exit 64
fi

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "[error] macOS Keychain is only available on Darwin/macOS." >&2
  exit 1
fi

if ! command -v security >/dev/null 2>&1; then
  echo "[error] macOS security CLI was not found." >&2
  exit 1
fi

if [[ -z "$ACCOUNT" ]]; then
  echo "[error] Keychain account cannot be empty." >&2
  exit 64
fi

supabase_url="$(security find-generic-password -a "$ACCOUNT" -s "$URL_SERVICE" -w 2>/dev/null || true)"
if [[ -z "$supabase_url" ]]; then
  echo "[error] Supabase URL not found in Keychain." >&2
  echo "        Run: ./scripts/setup-supabase-keychain.sh --account '$ACCOUNT'" >&2
  exit 1
fi

supabase_secret_key="$(security find-generic-password -a "$ACCOUNT" -s "$KEY_SERVICE" -w 2>/dev/null || true)"
if [[ -z "$supabase_secret_key" ]]; then
  echo "[error] Supabase secret key not found in Keychain." >&2
  echo "        Run: ./scripts/setup-supabase-keychain.sh --account '$ACCOUNT'" >&2
  exit 1
fi

export SUPABASE_URL="$supabase_url"
export SUPABASE_SECRET_KEY="$supabase_secret_key"
unset supabase_url supabase_secret_key

exec "$@"
