# Dev Seed (planned)

Local-only seeding of the development Supabase stack. This is the target home for
what is today the `scenario` subcommand of `ops/cli/TotalCall.Cli`.

Nothing here is wired up yet — this folder currently documents the intended
direction only.

## Direction

- Dev seeding becomes its own path **outside** the .NET CLI (for example a small
  `seed.mjs` Node entry point), so it can stay close to fixtures/SQL and iterate
  quickly without a build step.
- When seeding needs a real domain operation (competition sync, athlete import,
  results import, scoring recompute), it must **call** `ops/cli/TotalCall.Cli`
  rather than reimplementing that logic. The CLI stays the single owner of
  domain/persistence operations.
- Fixtures and any SQL helpers live alongside the seeder here
  (`scenarios/`, `sql/`) once they exist.

## Guardrails (non-negotiable)

- Seeding must refuse to run against anything that is not local Supabase: the
  target URL has to be loopback (`localhost` / `127.0.0.1`).
- Never seed production. No production URLs, no production service-role keys.
- Service-role / secret keys stay server-side and local-only; never expose them
  to browser-delivered code.

## Transitional state

Until this path lands, local product states are still seeded through the CLI:

```bash
./scripts/dev-scenarios.sh all-states
```

which calls `ops/cli/TotalCall.Cli scenario ... --local`. That `scenario`
subcommand is marked **transitional** in the CLI help and will move here.
See [../../docs/DEV_SCENARIOS.md](../../docs/DEV_SCENARIOS.md).
