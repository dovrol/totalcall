# Supabase

Supabase is the runtime backend for authentication, private prediction storage, competition configuration, athlete-history data, official results, score snapshots, public standings, and public boards.

## Local Supabase

Local configuration lives in `supabase/config.toml`.

Default local URLs:

- API: `http://127.0.0.1:54321`
- Studio: `http://127.0.0.1:54323`
- Email inbox: `http://127.0.0.1:54324`
- Auth callback: `http://localhost:5010/auth/callback`
- Local MCP endpoint: `http://127.0.0.1:54321/mcp`

Common commands:

```bash
supabase start
supabase status
supabase db reset
supabase migration up --local
./scripts/dev-scenarios.sh all-states
```

## Frontend Configuration

The frontend may contain only public Supabase values:

- `Supabase:Url`
- `Supabase:PublishableKey`

Files:

- `src/TotalCall.Client/wwwroot/appsettings.json`
- `src/TotalCall.Client/wwwroot/appsettings.Development.json`

Never put a service-role key, database password, JWT signing key, or production secret in `wwwroot`.

## Migrations

Migrations live in `supabase/migrations`.

Major areas:

- Athlete data backend and public athlete analytics RPCs.
- Private profiles and cloud prediction saves.
- Submit flow and public participant projection.
- Profile display names.
- Competition definitions and immutable config versions.
- Competition definition hardening.
- Official results and private score snapshots.
- Current user's own score RPC.
- Public leaderboard and public board RPCs.

The migrations are schema and security contracts. Dev data belongs in dev scenarios or local seed data, not in production migrations.

## Public Vs Private Tables

Public readable data:

- `data_sources`
- `athletes`
- `source_meets`
- `athlete_results`
- `athlete_history_view`
- `competitions`
- published competition metadata through `published_competitions`
- published competition config through `get_published_competition`

Private owner-only data:

- `profiles`
- `prediction_submissions`

Private service-role data:

- `athlete_aliases`
- `athlete_external_ids`
- `import_runs`
- `import_errors`
- `athlete_name_resolution_queue`
- `official_results`
- `official_result_groups`
- `score_snapshots`
- `admin_operation_runs`

## Competition Config

Tables:

- `competitions` stores public lifecycle metadata and `published_version_id`.
- `competition_versions` stores immutable versioned config JSONB.

Public access:

- `published_competitions` returns list metadata only.
- `get_published_competition(p_slug text)` returns the currently published full config for one slug.

Important behavior:

- End-user prediction writes fail if the competition is unknown or has no published config version.
- The backend stamps `prediction_submissions.competition_version_id` from `competitions.published_version_id`.
- Existing competition version identity and config are immutable; create a new version when config changes.

## Prediction Submissions

`prediction_submissions` stores private user prediction snapshots.

Important columns:

- `id`
- `user_id`
- `competition_id`
- `competition_version_id`
- `status`
- `answers_json`
- `app_version`
- `schema_version`
- `created_at`
- `updated_at`
- `submitted_at`

RLS/grants:

- Authenticated users can select, insert, and update only their own rows.
- Anonymous users have no direct table access.
- Delete is not granted to users in the current version.
- Service role has full access for imports and maintenance.

`answers_json` is private and must never be exposed directly.

## Profiles

`profiles` stores private account profile rows keyed by Supabase Auth user id.

Important behavior:

- A trigger creates a profile for new Auth users.
- Display names are generated or validated.
- Display names are unique case-insensitively.
- Users can read/update only their own profile.
- Public projections may expose display name only.
- Email stays in Supabase Auth and is not public.

## Official Results

Tables:

- `official_results`
- `official_result_groups`

These are private service-role tables. They are written by `TotalCall.Sync results`.

`official_result_groups` stores group/question/category result payloads. Groups can be `pending` or `final`; only final groups are scored.

## Score Snapshots

`score_snapshots` stores private calculated leaderboard rows.

Written by:

- `OfficialResultsImporter`
- `ScoreSnapshotBuilder`

Read by:

- `get_competition_leaderboard`
- `get_my_score`
- `get_public_board`

Direct public select is revoked.

## RPCs Used By The Frontend

Competition config:

- `get_published_competition(p_slug text)`

Athlete data:

- `get_athlete_data_import_status(p_source text)`
- `get_athlete_analytics(...)`
- `get_attempt_benchmark(...)`

Prediction and standings:

- `submit_prediction(p_competition_id, p_answers_json, p_app_version, p_schema_version)`
- `get_competition_participants(p_competition_id)`
- `get_competition_leaderboard(p_competition_id)`
- `get_my_score(p_competition_id)`
- `get_public_board(p_competition_id, p_board_ref)`

## Participants And Leaderboard RPCs

`get_competition_participants` returns submitted participants only:

- competition id,
- display name,
- submitted time,
- submitted status.

It does not return user id, email, raw answers, or drafts.

`get_competition_leaderboard` returns scored public rows:

- position,
- board ref,
- display name,
- total points,
- scored group count,
- total group count,
- status,
- last calculated time.

It does not return raw answers. Public boards must call `get_public_board`.

## get_my_score

`get_my_score` is authenticated-only and scoped to `auth.uid()`.

It returns the calling user's own:

- rank,
- total points,
- scored/total progress,
- status,
- results hash,
- rules version,
- breakdown JSON,
- calculation time.

It does not expose other users' answers or account identifiers.

## get_public_board

`get_public_board` is public but narrow.

It returns a board only when:

- a score snapshot exists,
- the snapshot has `scored_groups_count > 0`,
- the linked submission is submitted,
- `submitted_at` is set,
- the competition lock time has passed.

It returns:

- display name,
- rank,
- points,
- progress,
- status,
- sanitized picks array,
- breakdown,
- calculation time.

It intentionally returns only the picks array from `answers_json`, never the full `PredictionSet`.

## RLS And Security Boundaries

Security boundaries are enforced by:

- RLS policies,
- revoked direct table privileges,
- security definer RPCs that return curated projections,
- frontend use of publishable key only,
- sync/import use of service-role key outside the browser.

Do not add public direct selects to private tables.

Do not expose:

- service-role keys,
- Supabase Auth tokens,
- email addresses,
- user IDs,
- raw `answers_json`,
- full `PredictionSet` metadata,
- local owner ids,
- import logs or errors,
- private score snapshot table rows.

## Importer Responsibilities

`TotalCall.Sync` with service-role credentials owns:

- syncing competition metadata and config versions,
- importing athlete history,
- importing official results,
- recalculating score snapshots,
- seeding local-only dev scenarios.

The importer must be re-run after:

- official results change,
- scoring rules change,
- competition config changes that affect submitted versions or scoring,
- public leaderboard needs recalculation.

## Service Role Vs Publishable Key

Publishable key:

- safe for frontend config,
- used by anonymous public reads and Supabase Auth,
- paired with user access token for owner-scoped writes.

Service-role key:

- bypasses RLS,
- used only by sync scripts, GitHub Actions secrets, or local dev scenario tooling,
- must never be committed,
- must never be sent to the browser.
