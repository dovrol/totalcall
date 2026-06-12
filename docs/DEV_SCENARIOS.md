# Dev Scenarios v1

Dev scenarios seed local Supabase with deterministic `dev-*` competitions for UI and backend checks. They are local-only and do not add fake data through production migrations.

## Run All States

Start local Supabase, then run:

```bash
./scripts/dev-scenarios.sh all-states
```

Then start the app:

```bash
./scripts/dev.sh 5010
```

Open `http://localhost:5010` and use the `dev-*` competitions.

## Single Scenarios

```bash
./scripts/dev-scenarios.sh open
./scripts/dev-scenarios.sh open-with-submissions
./scripts/dev-scenarios.sh locked-no-results
./scripts/dev-scenarios.sh partial-results
./scripts/dev-scenarios.sh final-results
./scripts/dev-scenarios.sh empty
```

The wrapper applies pending local migrations, reads the local Supabase URL and secret key from `supabase status -o env`, and runs:

```bash
dotnet run --project tools/sync/TotalCall.Sync/TotalCall.Sync.csproj -- scenario all-states --local
```

The `--local` flag is required. The runner also refuses non-loopback Supabase URLs.

## Seeded Competitions

`dev-open`
: Open competition, prediction lock far in the future, no submitted users. Use it to test normal prediction entry.

`dev-open-with-submissions`
: Open competition with three submitted test users and no results. Standings shows submitted participants, not scores.

`dev-locked-no-results`
: Locked competition with three submitted test users and no results. Standings stays in pre-scoring participants mode.

`dev-partial-results`
: Locked competition with three submitted test users and final results for 3 required groups. Standings shows a live partial leaderboard, e.g. `3/16`.

`dev-final-results`
: Completed competition with three submitted test users and final results for all required groups. Standings shows a final leaderboard.

`dev-empty`
: Open competition without submitted users. Standings shows the empty state.

## What Gets Seeded

For each selected scenario, the runner resets only that known `dev-*` competition id, then creates:

- `competitions` metadata,
- one published `competition_versions` row cloned from `worlds-2026` with dev id/slug/status/deadline,
- deterministic Auth users and profiles for submitted scenarios,
- submitted `prediction_submissions`,
- official results for partial/final scenarios,
- score snapshots for partial/final scenarios.

The runner does not touch `worlds-2026`.

## Safety

Guard rails:

- `scenario` requires `--local`,
- `SUPABASE_URL` must be loopback, such as `http://127.0.0.1:54321`,
- fake data is not stored in migrations,
- only known `dev-*` competition ids are reset.

## Adding a Scenario

Add a new `DevCompetitionScenario` entry in `tools/sync/TotalCall.Sync/DevScenarios/DevScenarioRunner.cs`.

Choose a new `dev-*` id, set lifecycle fields, submitted user count, and optional results mode. Keep dates deterministic; use fixed past/future timestamps instead of relying on the system clock.
