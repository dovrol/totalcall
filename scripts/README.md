# Scripts

- `./scripts/dev.sh [port]` - runs the Blazor app locally, default port `5010`.
- `./scripts/restore.sh` - restores NuGet packages for `TotalCall.sln`.
- `./scripts/build.sh` - builds `TotalCall.sln` without restore.
- `./scripts/test.sh` - runs tests without build.
- `./scripts/clean.sh` - removes all `bin` and `obj` directories.
- `./scripts/ops.sh <command> [options]` - thin wrapper around the `ops/cli/TotalCall.Cli` operations CLI (e.g. `./scripts/ops.sh --help`).
- `./scripts/setup-supabase-keychain.sh [--account name]` - stores Supabase operations credentials in macOS Keychain.
- `./scripts/with-supabase-keychain.sh [--account name] -- <command>` - runs a command with Supabase operations credentials loaded from macOS Keychain.
- `./scripts/dev-scenarios.sh [scenario] [cli-options]` - applies local migrations and seeds local-only Supabase product states. Requires local Supabase.
- `./scripts/sync-supabase.sh [competition-json] [both|openipf|openpowerlifting] [auto|none|results-json]` - syncs competition config, athlete history, and optionally official results/scoring snapshots. Loads credentials from macOS Keychain account `production` by default.

See [../docs/LOCAL_DEVELOPMENT.md](../docs/LOCAL_DEVELOPMENT.md), [../docs/DEV_SCENARIOS.md](../docs/DEV_SCENARIOS.md), and [../docs/SUPABASE.md](../docs/SUPABASE.md).
