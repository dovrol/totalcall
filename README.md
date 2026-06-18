# TotalCall

TotalCall is a Blazor WebAssembly prediction game for powerlifting fans. It is a fantasy/prediction product, not betting: there are no odds, stakes, payouts, or gambling mechanics.

The app is already used in production and is currently being documented for the v1.0.0 release. No v1.0.0 tag or GitHub release has been created yet.

## Stack

- Blazor WebAssembly on .NET 10.
- Plain CSS from `src/TotalCall.Client/wwwroot/css`.
- Supabase Auth for Magic Link sign-in.
- Supabase Postgres/PostgREST for competition config, private cloud saves, public standings, public boards, athlete history, and scoring snapshots.
- GitHub Pages for the static frontend.
- `tools/sync/TotalCall.Sync` for competition config sync, athlete-history import, official results import, scoring recomputation, and local dev scenarios.

## Quick Start

```bash
./scripts/restore.sh
supabase start
./scripts/dev-scenarios.sh all-states
./scripts/dev.sh
```

Then open `http://localhost:5010`.

Local Supabase tools:

- API: `http://127.0.0.1:54321`
- Studio: `http://127.0.0.1:54323`
- Local email inbox: `http://127.0.0.1:54324`

## Commands

```bash
./scripts/restore.sh
./scripts/build.sh
./scripts/test.sh
./scripts/dev.sh [port]
./scripts/dev-scenarios.sh [scenario]
./scripts/sync-supabase.sh [competition-json] [both|openipf|openpowerlifting] [auto|none|results-json]
```

For production syncs, set `SUPABASE_URL` and `SUPABASE_SECRET_KEY` manually. Never put service-role keys in frontend config or commit them.

## Documentation

Start with [docs/README.md](docs/README.md).

Key docs:

- [Architecture](docs/ARCHITECTURE.md)
- [Features](docs/FEATURES.md)
- [Product States](docs/PRODUCT_STATES.md)
- [Scoring](docs/SCORING.md)
- [Supabase](docs/SUPABASE.md)
- [Security and Privacy](docs/SECURITY_PRIVACY.md)
- [Local Development](docs/LOCAL_DEVELOPMENT.md)
- [Release](docs/RELEASE.md)
- [AI Agent Guide](docs/AI_AGENT_GUIDE.md)

## Privacy Note

Drafts and raw `answers_json` are private. Submitted picks can appear only through sanitized public board RPCs after lock and scoring, and public standings expose safe fields only: display name, rank, points, progress, status, and calculation time. Email, user ID, auth tokens, service keys, full draft metadata, and raw submission JSON must never be exposed publicly.
