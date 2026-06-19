# Architecture

TotalCall is a static Blazor WebAssembly app backed by Supabase. The browser renders and edits predictions, while Supabase stores private user data and exposes curated public projections. Official scoring is computed by the sync/import tool and persisted as backend snapshots; the frontend renders those snapshots and does not compute official leaderboard points.

## High-Level Shape

```text
Browser (Blazor WASM)
  -> bundled JSON fallback in wwwroot/data
  -> Supabase Auth REST API for Magic Link + PKCE
  -> Supabase PostgREST/RPC with publishable key and user access tokens

Supabase
  -> public competition config and athlete-history projections
  -> private profiles and prediction_submissions
  -> private official_results, official_result_groups, score_snapshots
  -> public curated RPCs for participants, leaderboard, public boards, and own score

Sync/import tool
  -> service-role PostgREST writes
  -> competition config sync
  -> athlete-history import from OpenIPF/OpenPowerlifting
  -> official results import
  -> score_snapshots recomputation
  -> local-only dev scenarios

GitHub Actions
  -> tag/workflow-dispatch frontend deploy to GitHub Pages
  -> scheduled/manual data sync workflow
```

## Projects

- `src/TotalCall.Client` is the Blazor WebAssembly frontend.
- `src/TotalCall.Core` contains shared domain models and scoring code used by the public app, tests, and the sync tool.
- `src/TotalCall.Operations` contains server/CLI-side operational code that can use service-role credentials. It currently owns the Supabase REST wrapper, competition config hashing, competition config publish workflow, official results import, and score snapshot recomputation.
- `tests/TotalCall.Tests` contains xUnit tests for domain logic, validation, storage, migrations, scoring, and sync helpers.
- `tools/sync/TotalCall.Sync` is a .NET console wrapper for Supabase imports, scoring recomputation, and local scenarios.
- `supabase/migrations` is the source of truth for database schema, RPCs, RLS, and grants.
- `scripts` contains thin local wrappers.

Current target framework is `net10.0`; `global.json` pins SDK `10.0.300` with `latestFeature` roll-forward.

## Frontend

The frontend is a Blazor WebAssembly app with plain CSS:

- App bootstrap and dependency injection live in `src/TotalCall.Client/Program.cs`.
- Pages live in `src/TotalCall.Client/Pages`.
- UI primitives live in `src/TotalCall.Client/Components/UI`.
- Prediction shell, modules, Top-N workspace, results views, sharing, and timeline components live in `src/TotalCall.Client/Components/Predictions`.
- Domain models live under `src/TotalCall.Core/Domain`.
- Reusable application services live under `src/TotalCall.Client/Application`.
- Infrastructure adapters live under `src/TotalCall.Client/Infrastructure`.
- Prediction storage abstractions live under `src/TotalCall.Client/Storage`.
- Scoring code lives under `src/TotalCall.Core/Scoring`.

Pages should stay thin. Domain components may reference domain models, but scoring and persistence should stay outside Razor components.

## Competition Loading

Competition loading is config-driven through `ICompetitionProvider`.

- `SupabaseCompetitionProvider` reads `published_competitions` and `get_published_competition`.
- `JsonCompetitionProvider` reads bundled files from `wwwroot/data/competitions`.
- `CompositeCompetitionProvider` tries Supabase first and falls back to bundled JSON when the remote source is unavailable or has no published row.

The published competition config is stored in Supabase as JSONB in `competition_versions.config`. The JSON files under `src/TotalCall.Client/wwwroot/data/competitions` remain a development/import source and runtime fallback.

Supabase lifecycle fields override the JSON snapshot when loaded:

- `status`
- `prediction_open_at`
- `prediction_lock_at`
- published version

## Prediction UI

Prediction rendering is module-driven:

- `CompetitionPredictionPage.razor` loads the competition and the current `PredictionSet`.
- `PredictionShell.razor` provides the generic prediction layout for normal modules.
- `TopNWorkspace.razor` provides the specialized current Top-N board experience.
- `PredictionModuleRenderer.razor` maps `PredictionGroup.Type` or question type to module components.
- Modules emit `PredictionAnswer` through callbacks and do not save directly.

Missing answers are allowed. Validation tracks module completion states, but the app does not force every module to be complete before saving.

## Persistence

`IPredictionStore` is implemented by `SynchronizedPredictionStore`:

- Anonymous users store drafts in localStorage through `LocalStoragePredictionStore`.
- Signed-in users keep a local copy and synchronize a private cloud row through `SupabasePredictionStore`.
- Submit requires authentication and calls the `submit_prediction` RPC.
- Submitted status is stored locally after a successful backend submit.
- When synchronization fails, the UI blocks submit until cloud sync is retried.

Supabase enforces the prediction window with the `prediction_submissions_enforce_window` trigger. The frontend has client-side lock UX, but the backend is the authoritative save/submit cutoff.

## Supabase Role

Supabase provides:

- Magic Link authentication.
- Private owner-only `profiles`.
- Private owner-only `prediction_submissions`.
- Public competition config projections.
- Public athlete-history data and athlete analytics RPCs.
- Private official result tables.
- Private score snapshots.
- Public standings, participant list, and public board RPCs.

The frontend uses only `Supabase:Url` and `Supabase:PublishableKey`. Authenticated requests add the user's access token. Service-role keys are used only by CLI/server-side operations, scripts, GitHub Actions secrets, and local dev scenario tooling.

## Sync And Import Tool

`tools/sync/TotalCall.Sync` exposes these subcommands and delegates shared operational code to `TotalCall.Operations`:

- `competition` syncs competition metadata and immutable versioned JSON config, then publishes the version.
- `athletes` imports OpenIPF/OpenPowerlifting history for athletes referenced by the competition JSON.
- `results` imports official result groups and recalculates `score_snapshots`.
- `scenario` seeds local-only product states and users for UI testing.

`./scripts/sync-supabase.sh` is the production-oriented wrapper. It syncs the competition first, then athlete history for one or both sources, then imports matching official results files from `tools/sync/data/results` when results mode is `auto`.

## Scoring Ownership

Official scoring is not computed by the browser for public leaderboards.

- Rule implementation lives in shared C# scoring classes.
- `OfficialResultsImporter` loads official result groups and submitted predictions using the service-role key.
- `ScoreSnapshotBuilder` computes rows with `rules_version = "placement-v2"`.
- Results are written to private `score_snapshots`.
- The frontend reads public projections or the current user's own snapshot through RPCs.

The frontend may render a score breakdown returned by Supabase, but it must not become the source of truth for official points.

## GitHub Actions And Release Flow

`.github/workflows/deploy-github-pages.yml` deploys the frontend to GitHub Pages on tags matching `v*` or manual dispatch. It:

- sets up the SDK from `global.json`,
- publishes the Blazor app in Release,
- derives `InformationalVersion` from the tag or a dev SHA,
- rewrites base href for GitHub Pages,
- injects `ANALYTICS_SNIPPET` if configured,
- uploads and deploys the Pages artifact.

`.github/workflows/sync-data.yml` runs manually or on a weekly schedule. It restores and runs `./scripts/sync-supabase.sh` with Supabase secrets from GitHub Actions.

## Backend Vs Frontend Calculations

Frontend renders:

- competition list and prediction UI,
- draft and submitted private board state,
- participant list and standings from safe RPCs,
- public boards from safe RPCs,
- own score breakdown from `get_my_score`,
- athlete-history analytics from public RPCs,
- local validation and completion status.

Backend/sync tool owns:

- deadline/status enforcement for cloud saves and submit,
- competition version stamping on submissions,
- official results import,
- official score snapshot generation,
- public leaderboard ordering,
- public board reveal constraints,
- RLS and privacy boundaries.
