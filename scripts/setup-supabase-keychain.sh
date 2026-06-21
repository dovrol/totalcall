#!/usr/bin/env bash
set -euo pipefail

ACCOUNT="${TOTALCALL_SUPABASE_KEYCHAIN_ACCOUNT:-production}"
URL_SERVICE="${TOTALCALL_SUPABASE_URL_SERVICE:-totalcall.supabase.url}"
KEY_SERVICE="${TOTALCALL_SUPABASE_SECRET_KEY_SERVICE:-totalcall.supabase.secret-key}"

usage() {
  cat <<'EOF'
Usage:
  ./scripts/setup-supabase-keychain.sh [--account name]

Stores TotalCall Supabase operations credentials in macOS Keychain.

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
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "[error] Unknown argument: $1" >&2
      usage >&2
      exit 64
      ;;
  esac
done

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

read -r -p "Supabase URL for '${ACCOUNT}': " supabase_url
if [[ -z "$supabase_url" ]]; then
  echo "[error] Supabase URL cannot be empty." >&2
  exit 64
fi

security add-generic-password \
  -U \
  -a "$ACCOUNT" \
  -s "$URL_SERVICE" \
  -w "$supabase_url" \
  >/dev/null
unset supabase_url

echo "Enter the Supabase secret/service-role key for '${ACCOUNT}' when Keychain prompts."
security add-generic-password \
  -U \
  -a "$ACCOUNT" \
  -s "$KEY_SERVICE" \
  -w \
  >/dev/null

echo "Saved TotalCall Supabase credentials in macOS Keychain for account '${ACCOUNT}'."
