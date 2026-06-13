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
./scripts/dev-scenarios.sh roster-update
./scripts/dev-scenarios.sh roster-update-locked
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

`dev-roster-update`
: Open competition with several withdrawn athletes and three submitted test users.
Use it to check the updates chip/drawer, withdrawn badge, affected-user warning,
unaffected submitted picks, fresh-user selection, and manual plus generated
competition timeline entries.

`dev-roster-update-locked`
: Locked competition with several withdrawn athletes, submitted test users, and final
results that exclude the withdrawn athletes. Use it to check locked/0-point copy
and that scoring still counts the remaining hits in the group, plus the timeline entries
for roster/results updates.

## Roster Update Click-Through Users

The scenario runner creates these deterministic local users:

| Email | Profile | Use case |
| --- | --- | --- |
| `dev-alice@totalcall.local` | Dev Alice Affected | Submitted picks include several withdrawn athletes in scored Top 3 slots. Use this for the affected chip, pinned warning, row warnings, and submit confirmation warning. |
| `dev-bruno@totalcall.local` | Dev Bruno Unaffected | Submitted picks intentionally move every withdrawn athlete outside scored slots. Use this for neutral roster-update history with no affected-user warning. |
| `dev-casey@totalcall.local` | Dev Casey Generated | Submitted picks are affected only by withdrawn athletes that have no manual `updates` entry, so the drawer should show generated roster updates from athlete metadata. |
| `dev-dana@totalcall.local` | Dev Dana Fresh | Account exists but has no submitted predictions. Use this to verify fresh predictions, default fills, and manual selection do not choose withdrawn athletes. |

For local auth, request a magic link for one of the addresses above and open it
from the local inbox at `http://127.0.0.1:54324`.

## What Gets Seeded

For each selected scenario, the runner resets only that known `dev-*` competition id, then creates:

- `competitions` metadata,
- one published `competition_versions` row cloned from `worlds-2026` with dev id/slug/status/deadline,
- deterministic Auth users and profiles for submitted scenarios,
- submitted `prediction_submissions`,
- official results for partial/final scenarios,
- score snapshots for partial/final scenarios.

`roster-update` and `roster-update-locked` seed six withdrawn athletes across
different categories. Some are covered by manual `roster_update` entries, while
others are intentionally metadata-only so the frontend generates timeline items
from `athlete.status = "withdrawn"`.

Seeded withdrawn athletes in the default `worlds-2026` source:

| Athlete | Category | Timeline source |
| --- | --- | --- |
| Chapon Tiffany | Women 47 kg | Manual multi-athlete roster update |
| Dekkers Pleun | Women 52 kg | Manual multi-athlete roster update |
| Lawrence Amanda | Women 84 kg | Manual multi-athlete roster update |
| Butters Bobbie | Women 57 kg | Generated from athlete metadata, `updated_at` only |
| Garcia Antoine | Men 59 kg | Generated from athlete metadata |
| Perkins Austin | Men 74 kg | Manual single-athlete roster update |

The runner does not touch `worlds-2026`.

## Competition Timeline

Competition configs may include an optional `updates` array. These entries are
published as part of the immutable competition version and appear on the
prediction screen timeline:

```json
{
  "updates": [
    {
      "id": "worlds-2026-roster-2026-06-12",
      "type": "roster_update",
      "occurred_at": "2026-06-12T12:00:00Z",
      "title": "Roster update: Amanda Lawrence has withdrawn.",
      "body": "Amanda Lawrence is no longer selectable for new picks.",
      "athlete_ids": ["amanda-lawrence"],
      "source": "federation"
    }
  ]
}
```

Supported `type` values are `general`, `roster_update`, `deadline_change`,
`results_update`, and `scoring_update`. If a withdrawn athlete has no matching
manual `roster_update` entry, the UI generates a timeline item from
`athlete.status = "withdrawn"` and the optional withdrawal metadata.

For the fuller QA matrix around roster updates, timeline drawer behavior,
privacy, scoring, and sync/versioning, see
[`COMPETITION_UPDATES_TEST_CASES.md`](COMPETITION_UPDATES_TEST_CASES.md).

## Safety

Guard rails:

- `scenario` requires `--local`,
- `SUPABASE_URL` must be loopback, such as `http://127.0.0.1:54321`,
- fake data is not stored in migrations,
- only known `dev-*` competition ids are reset.

## Adding a Scenario

Add a new `DevCompetitionScenario` entry in `tools/sync/TotalCall.Sync/DevScenarios/DevScenarioRunner.cs`.

Choose a new `dev-*` id, set lifecycle fields, submitted user count, and optional results mode. Keep dates deterministic; use fixed past/future timestamps instead of relying on the system clock.
