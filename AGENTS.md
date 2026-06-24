# TotalCall Agent Instructions

TotalCall is a Blazor WebAssembly fantasy/prediction game for powerlifting fans. It is not a betting product.

## Project Shape

- Main app: `src/TotalCall.Client`.
- Tests: `tests/TotalCall.Tests`.
- Supabase migrations/config: `supabase`.
- Shared server/CLI-side operational workflows: `src/TotalCall.Operations`.
- Operations live under `ops`: the domain CLI (`ops/cli/TotalCall.Cli`), the
  agent-facing MCP server (`ops/mcp/TotalCall.Mcp`), the planned local dev-seed
  path (`ops/dev-seed`), and operational data (`ops/data/results`).
- Utility scripts live in `scripts`.

## Frontend

- Use plain CSS from `src/TotalCall.Client/wwwroot/css`.
- Keep pages thin and compose them from Blazor components.
- Prefer existing primitives in `Components/UI` before adding new styling patterns.
- UI primitives must not reference domain models.
- Domain components may reference domain models but must not contain scoring or persistence logic.
- Keep scoring outside Razor components.
- Use `ICompetitionProvider` for competition loading and `IPredictionStore` for prediction persistence.
- Use `PredictionModuleRenderer` to map `PredictionGroup.Type` to module components.
- Modules emit changes via `EventCallback<PredictionAnswer>` and must not save directly.
- Missing answers are allowed; do not force users to complete every module.
- Avoid betting/casino visual language.
- Do not hardcode competition-specific UI paths or layouts; use competition JSON and existing abstractions.

## Local Development

- Local app URL is `http://localhost:5010`.
- Local Supabase config is in `supabase/config.toml`.
- Blazor development config is `src/TotalCall.Client/wwwroot/appsettings.Development.json`.
- Frontend config may contain only public Supabase values: URL and publishable key.
- Never commit Supabase secret/service-role keys.
- Local Supabase API is `http://127.0.0.1:54321`.
- Supabase Studio is `http://127.0.0.1:54323`.
- Local email/Magic Link inbox is `http://127.0.0.1:54324`.
- Auth callback route is `/auth/callback`; local redirect URLs are configured in `supabase/config.toml`.
- Local Supabase MCP is `http://127.0.0.1:54321/mcp`.
- Claude Code uses `.mcp.json`. It exposes `totalcall-ops-local`,
  `totalcall-ops-production`, and `supabase-local`. Build the solution before
  starting TotalCall MCP servers, because they run with `dotnet run --no-build`.
- Zed uses `.zed/settings.json`. It defines `totalcall-ops-local`,
  `totalcall-ops-production`, and the local Supabase MCP endpoint. Use the
  production server only for intentional production operations.

## Supabase Sync

- Sync everything through `./scripts/sync-supabase.sh`: it first syncs the competition
  definition (`competition` subcommand), then athlete history per source.
- The wrapper imports both `openipf` and `openpowerlifting` by default.
- For real syncs, use macOS Keychain via `./scripts/setup-supabase-keychain.sh --account production`
  and `./scripts/with-supabase-keychain.sh --account production -- <command>`.
- GitHub Actions uses `.github/workflows/sync-data.yml`, which calls the same wrapper.
- `ops/cli/TotalCall.Cli` exposes `athletes`, `competition`, `results`, and
  `scenario` subcommands; keep their responsibilities separate. The CLI should
  stay thin when code belongs in `src/TotalCall.Operations`. `scenario` is
  transitional: local dev seeding is moving to `ops/dev-seed` (see its README).
- The competition definition lives in Supabase (`competitions` + `competition_versions`);
  the JSON in `ops/data/competitions` stays an operations/import source only.
  Do not add browser-delivered competition JSON fallbacks.
- Service-role keys must stay in CLI/server-side operations. Never pass them to
  Blazor WebAssembly or other browser-delivered code.

## Verification

- Run `./scripts/build.sh` after app code changes.
- Run `./scripts/test.sh` when changing shared logic, persistence, scoring, validation, or sync behavior.
- For CLI-only changes, `dotnet build ops/cli/TotalCall.Cli/TotalCall.Cli.csproj --no-restore` is usually enough.
