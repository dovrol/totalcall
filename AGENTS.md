# TotalCall Agent Instructions

TotalCall is a Blazor WebAssembly fantasy/prediction game for powerlifting fans. It is not a betting product.

## Project Shape

- Main app: `src/TotalCall.Client`.
- Tests: `tests/TotalCall.Tests`.
- Supabase migrations/config: `supabase`.
- Athlete data importer: `tools/import-opl/TotalCall.OplImporter`.
- Utility scripts live in `scripts`.

## Frontend

- Use plain CSS from `src/TotalCall.Client/wwwroot/css`; Tailwind is not used.
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

## Athlete Data Import

- Import athlete history through `./scripts/import-athlete-data.sh`.
- The wrapper imports both `openipf` and `openpowerlifting` by default.
- For real imports, set `SUPABASE_URL` and `SUPABASE_SECRET_KEY` manually first.
- GitHub Actions uses `.github/workflows/import-opl.yml`, which calls the same wrapper.
- The importer has additional lower-level options, but the wrapper should stay thin.

## Verification

- Run `./scripts/build.sh` after app code changes.
- Run `./scripts/test.sh` when changing shared logic, persistence, scoring, validation, or importer behavior.
- For importer-only changes, `dotnet build tools/import-opl/TotalCall.OplImporter/TotalCall.OplImporter.csproj --no-restore` is usually enough.
