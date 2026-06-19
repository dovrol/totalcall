# Local Development

## Prerequisites

- .NET SDK compatible with `global.json` (`10.0.300` with latest feature roll-forward).
- Supabase CLI.
- A shell that can run the scripts in `scripts/`.

Optional:

- A browser with devtools for checking localStorage and network calls.

## Restore

```bash
./scripts/restore.sh
```

This runs:

```bash
dotnet restore TotalCall.sln
```

## Run Local Supabase

```bash
supabase start
supabase status
```

Local services:

- API: `http://127.0.0.1:54321`
- Studio: `http://127.0.0.1:54323`
- Email inbox: `http://127.0.0.1:54324`

Frontend development config points to the local Supabase API in `src/TotalCall.Client/wwwroot/appsettings.Development.json`.

## Reset Database

```bash
supabase db reset
```

Then seed scenarios if needed:

```bash
./scripts/dev-scenarios.sh all-states
```

## Run Migrations

Apply pending local migrations without a full reset:

```bash
supabase migration up --local
```

`./scripts/dev-scenarios.sh` runs this before seeding.

## Run Frontend

```bash
./scripts/dev.sh
```

Default URL:

```text
http://localhost:5010
```

Use a custom port:

```bash
./scripts/dev.sh 5020
```

If you use a custom port for auth flows, add matching redirect URLs to local Supabase config.

## Run Admin Host

```bash
dotnet run --project src/TotalCall.Admin.Host/TotalCall.Admin.Host.csproj
```

Default URL:

```text
http://localhost:5025
```

With local Supabase credentials:

```bash
SUPABASE_URL=http://127.0.0.1:54321 \
SUPABASE_SECRET_KEY=<local service-role key> \
dotnet run --project src/TotalCall.Admin.Host/TotalCall.Admin.Host.csproj
```

Use `supabase status` to inspect local credentials. Do not put service-role keys in appsettings files. The admin host keeps those values server-side and exposes only sanitized runtime status to the browser and `/healthz`.

The admin host currently includes:

- runtime credential status at `/`,
- local competition config validation at `/competitions`,
- confirmation-gated competition config publish at `/competitions`,
- confirmation-gated official results import at `/results`,
- sanitized JSON health status at `/healthz`.

## Build And Test

```bash
./scripts/build.sh
./scripts/test.sh
```

Current script behavior:

- `build.sh` runs `dotnet build TotalCall.sln --no-restore`.
- `test.sh` runs `dotnet test TotalCall.sln -m:1 /nr:false --no-build`.

Run restore/build before test after a clean checkout.

For sync-tool-only changes:

```bash
dotnet build tools/sync/TotalCall.Sync/TotalCall.Sync.csproj --no-restore
```

## Sync / Import

Production-oriented wrapper:

```bash
SUPABASE_URL=...
SUPABASE_SECRET_KEY=...
./scripts/sync-supabase.sh src/TotalCall.Client/wwwroot/data/competitions/worlds-2026.json both auto
```

Arguments:

- competition JSON path,
- source: `both`, `openipf`, or `openpowerlifting`,
- results: `auto`, `none`, or a results JSON path.

`auto` imports matching files from `tools/sync/data/results`.

Direct tool commands:

```bash
dotnet run --project tools/sync/TotalCall.Sync/TotalCall.Sync.csproj -- competition --competition-json <path>
dotnet run --project tools/sync/TotalCall.Sync/TotalCall.Sync.csproj -- athletes --competition-json <path> --source openipf
dotnet run --project tools/sync/TotalCall.Sync/TotalCall.Sync.csproj -- results --competition-id worlds-2026 --results-json <path>
dotnet run --project tools/sync/TotalCall.Sync/TotalCall.Sync.csproj -- scenario all-states --local
```

Only run production syncs with intentional environment variables. Do not put service keys in appsettings files.

## Local Auth / Email Inbox

Local auth callback:

```text
http://localhost:5010/auth/callback
```

Flow:

1. Start Supabase.
2. Start the app.
3. Request a Magic Link.
4. Open `http://127.0.0.1:54324`.
5. Follow the link from the local email.
6. Confirm the app returns to the requested URL.

## Common Troubleshooting

Supabase not configured:

- Confirm `supabase start` is running.
- Confirm the app loaded `appsettings.Development.json`.
- Check `Supabase:Url` and `Supabase:PublishableKey`.

Magic Link does not return:

- Check `supabase/config.toml` redirect URLs.
- Use `http://localhost:5010`, not a different port, unless config is updated.
- Check the local email inbox.

Cloud save fails:

- Confirm the user is signed in.
- Confirm migrations are applied.
- Check `prediction_submissions` RLS and grants.
- Check whether the competition is locked or unpublished.

Public leaderboard empty:

- Confirm there are submitted rows.
- Import official results.
- Confirm score snapshots exist.
- Confirm `scored_groups_count > 0`.

Public board not found:

- Confirm standings row has a `board_ref`.
- Confirm lock time has passed.
- Confirm the linked submission is `submitted`.
- Confirm score snapshot exists.

Stale local data:

```bash
supabase db reset
./scripts/dev-scenarios.sh all-states
```

Stale build artifacts:

```bash
./scripts/clean.sh
./scripts/restore.sh
./scripts/build.sh
```
