# AI Agent Guide

Use this as the short checklist when working in TotalCall.

## Hard Rules

- Do not expose secrets.
- Do not commit or print service-role keys.
- Do not put service-role keys in `wwwroot` config.
- Do not edit production data unless explicitly instructed.
- Do not run production migrations, production syncs, deploys, tags, or GitHub releases without explicit confirmation.
- Preserve privacy boundaries.

## Architecture Rules

- Main app: `src/TotalCall.Client`.
- Shared domain/scoring is linked through `src/TotalCall.Core`.
- Tests: `tests/TotalCall.Tests`.
- Supabase migrations/config: `supabase`.
- Sync/import tool: `tools/sync/TotalCall.Sync`.
- Scripts: `scripts`.

Frontend:

- Use plain CSS in `src/TotalCall.Client/wwwroot/css`.
- Keep pages thin.
- Prefer `Components/UI` primitives.
- UI primitives must not reference domain models.
- Domain components may reference domain models but must not contain scoring or persistence logic.
- Modules emit `PredictionAnswer` via callbacks and must not save directly.
- Use `PredictionModuleRenderer` for module mapping.
- Missing answers are allowed.

## Supabase And Privacy Rules

- Frontend may use only Supabase URL and publishable key.
- Authenticated writes use the user's access token.
- Service-role access belongs only in sync/import tooling, scripts, GitHub Actions secrets, or local dev scenario tooling.
- `prediction_submissions.answers_json` is private.
- Public endpoints must not expose raw `answers_json`.
- Public endpoints must not expose email or user_id.
- Public board RPCs must return sanitized picks only.
- `score_snapshots` is private; public leaderboard reads curated RPC projections.

## Scoring Rules

- Frontend must not compute official scoring for public leaderboard state.
- Official scoring snapshots are recomputed by the sync/import tool.
- Current rules version is `placement-v2`.
- If scoring changes, update tests, update `ScoreSnapshotBuilder.RulesVersion`, rerun importer, and update docs.

## Local Testing

Prefer local Supabase and dev scenarios:

```bash
supabase start
./scripts/dev-scenarios.sh all-states
./scripts/dev.sh
```

Build/test:

```bash
./scripts/restore.sh
./scripts/build.sh
./scripts/test.sh
```

For sync-tool-only changes:

```bash
dotnet build tools/sync/TotalCall.Sync/TotalCall.Sync.csproj --no-restore
```

## Migrations And Fixtures

- Migrations are schema/security contracts.
- Do not put dev fixtures in production migrations.
- Use dev scenarios for local seeded product states.
- Keep RLS and grants explicit.
- Add migration tests when changing security-sensitive SQL.

## Documentation

When adding or changing features:

- update relevant docs in `docs/`,
- update tests when logic/security boundaries change,
- update release notes/checklists when release behavior changes.
