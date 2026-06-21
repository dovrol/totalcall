# Dev Scenarios

Dev scenarios seed local Supabase with predictable competitions, users, submissions, roster updates, official results, and score snapshots. They are meant for UI and product-state testing only.

> Transitional: scenario seeding currently runs through the `scenario` subcommand of `ops/cli/TotalCall.Cli` (via `./scripts/dev-scenarios.sh`). Local dev seeding is moving to a dedicated path outside the CLI under `ops/dev-seed`. See [../ops/dev-seed/README.md](../ops/dev-seed/README.md).

## Safety Guardrails

The scenario runner refuses to run unless:

- `--local` is passed.
- `SUPABASE_URL` is set.
- `SUPABASE_SECRET_KEY` or `SUPABASE_SERVICE_ROLE_KEY` is set.
- The Supabase URL is loopback, such as `http://127.0.0.1:54321`.

Do not run scenario seeding against production. The runner deletes and recreates selected `dev-*` competitions and related rows.

## Quick Start

```bash
supabase start
./scripts/dev-scenarios.sh all-states
./scripts/dev.sh
```

Open `http://localhost:5010` and look for `dev-*` competitions.

`./scripts/dev-scenarios.sh` applies pending local migrations before seeding.

## Available Scenarios

The scenario names are implemented in `DevScenarioRunner`.

| Scenario | Competition id | Purpose |
|---|---|---|
| `all-states` | all `dev-*` competitions | Seeds every scenario below. |
| `open` | `dev-open` | Open competition, no submissions, no results. |
| `open-with-submissions` | `dev-open-with-submissions` | Open competition with submitted users, no results. |
| `locked-no-results` | `dev-locked-no-results` | Locked competition with submissions, no imported results. |
| `partial-results` | `dev-partial-results` | Locked competition with partial official results and partial leaderboard. |
| `final-results` | `dev-final-results` | Completed competition with final results and final leaderboard. |
| `empty` | `dev-empty` | Open competition with no submissions and no roster withdrawal. |
| `roster-update` | `dev-roster-update` | Open competition with withdrawn athletes and affected submitted users. |
| `roster-update-locked` | `dev-roster-update-locked` | Locked competition with withdrawals, final results, and scoring. |

Direct competition ids such as `dev-final-results` also work as scenario arguments.

## Running A Single Scenario

```bash
./scripts/dev-scenarios.sh final-results
```

Pass sync-tool options after the scenario name:

```bash
./scripts/dev-scenarios.sh roster-update --base-competition-json src/TotalCall.Client/wwwroot/data/competitions/worlds-2026.json
```

## Local Supabase Reset

Use a reset when local data is stale or migrations changed:

```bash
supabase db reset
./scripts/dev-scenarios.sh all-states
```

If the local stack is not running:

```bash
supabase start
./scripts/dev-scenarios.sh all-states
```

## Seeded Users

The scenario runner creates or updates local Auth users with deterministic local-only email addresses and display names:

- Dev Alice Affected
- Dev Bruno Unaffected
- Dev Casey Generated
- Dev Dana Fresh

Use Supabase Studio or the local email inbox to inspect auth behavior. Do not use these seeded accounts in production.

## Testing UI States

Use `all-states` for broad UI review:

1. Start local Supabase.
2. Seed `all-states`.
3. Start the app with `./scripts/dev.sh`.
4. Open the home page.
5. Check the `dev-*` competitions.
6. Visit `/competitions/{slug}/predictions`.
7. Visit `/competitions/{slug}/standings`.
8. Open public board links from scenarios with leaderboard rows.

Useful checks:

- Open board is editable.
- Anonymous user sees local draft state and login-to-submit.
- Signed-in user sees private cloud draft state.
- Locked board is read-only.
- Locked no-results standings show submissions only.
- Partial results standings show live progress.
- Final results standings show final stage.
- Public board is read-only and shows sanitized picks.
- Roster-update scenarios show withdrawn athlete warnings.

## Login With Local Email Inbox

1. Run `supabase start`.
2. Open the app at `http://localhost:5010`.
3. Go to sign-in.
4. Request a Magic Link.
5. Open `http://127.0.0.1:54324`.
6. Open the latest email and follow the link.
7. Confirm the callback lands on `/auth/callback` and returns to the app.

Local redirect URLs are configured in `supabase/config.toml`.

## Scenario Internals

The runner:

- clones `worlds-2026.json` by default,
- changes id, slug, title, lifecycle dates, status, and config version,
- publishes a fresh `competition_versions` row,
- creates local Auth users and profiles,
- seeds submitted prediction rows for selected scenarios,
- marks selected scoreable athletes withdrawn for roster scenarios,
- adds explicit and generated competition updates,
- creates official result files in a temp path for results scenarios,
- runs the same official results importer used by normal sync,
- recalculates score snapshots.

## Current Limitations

- Scenarios are generated from the current base competition shape and mainly exercise Top-N placement scoring.
- They do not cover every generic prediction module type.
- They do not simulate production email delivery.
- They are destructive for selected `dev-*` competition ids in the local database.
