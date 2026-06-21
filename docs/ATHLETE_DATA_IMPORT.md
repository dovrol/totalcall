# Athlete data import (OpenIPF / OpenPowerlifting → Supabase)

This document describes how TotalCall imports athlete history from
OpenIPF / OpenPowerlifting into Supabase.

It complements:
- `supabase/migrations/20260527180000_create_athlete_data_backend.sql` — the database schema.
- `supabase/migrations/20260528110000_add_public_import_status_rpc.sql` — public import-status RPC for the frontend.
- `ops/cli/TotalCall.Cli` — the operations CLI (`athletes` subcommand imports athlete history).
- `.github/workflows/sync-data.yml` — the GitHub Actions workflow.

---

## 1. Why we do not use a separate `athlete-map.json`

The competition JSON (e.g.
`src/TotalCall.Client/wwwroot/data/competitions/worlds-2026.json`)
already lists every athlete that competes in a TotalCall predictive event.
That list is the **single source of truth** for who the app cares about.

A separate `athlete-map.json` would:

- duplicate the athlete list and drift over time;
- force two reviews when a single athlete changes;
- separate "this athlete competes here" from "this athlete maps to *that* OPL name"
  even though those two facts belong together.

Instead we extend each athlete entry in the competition JSON with an
`externalAthleteRefs` field that lists how that athlete is named (or identified)
in each external data source. The importer reads the same JSON the frontend reads.

## 2. How to add `externalAthleteRefs` to an athlete

Open the competition JSON and locate the athlete:

```json
{
  "id": "women-47-chapon-tiffany",
  "displayName": "Chapon Tiffany",
  "sex": "female",
  "countryName": "France",
  "weightCategoryId": "women-47",
  "seedTotalKg": 451.5,
  "countryCode": "FR",
  "externalAthleteRefs": [
    { "source": "openipf",          "name": "Tiffany Chapon" },
    { "source": "openpowerlifting", "name": "Tiffany Chapon" }
  ]
}
```

Rules:

| Field | Meaning |
|---|---|
| `source` | Data source code. Must match a row in `public.data_sources` (`openipf`, `openpowerlifting`, …). |
| `name` | Athlete's name as it appears in that source. |
| `externalId` *(optional)* | Stable identifier in that source (e.g. OPL `Username`). Preferred over `name` when available. |

You can have **multiple refs for the same source** — useful when the source
records different spellings or the name appears reversed:

```json
"externalAthleteRefs": [
  { "source": "openpowerlifting", "name": "Tiffany Chapon" },
  { "source": "openpowerlifting", "name": "Chapon Tiffany" }
]
```

If an athlete has **no `externalAthleteRefs` for the source being imported**,
the importer logs a warning and skips them.

> ⚠️ Never rely on `displayName` to match the source — TotalCall's
> `displayName` is `Lastname Firstname`; OpenPowerlifting uses `Firstname Lastname`.

## 3. How the importer reads the competition JSON

The athlete importer (`Athletes/AthleteImporter.cs`, the `athletes` subcommand):

1. Loads the competition JSON.
2. Filters athletes to those that have at least one `externalAthleteRefs[source = <requested>]`.
3. Builds two lookup indexes:
   - `normalize_name(externalAthleteRefs[].name)` → TotalCall slug
   - `externalAthleteRefs[].externalId` → TotalCall slug (when present)
4. Streams the source CSV and looks up each row's `Name` (and `Username` if available)
   against those indexes. Rows that don't match a roster athlete are dropped.

## 4. Name matching

### 4.1 Normalization (must stay in sync with SQL)

The .NET `NameNormalizer.Normalize` and the SQL `public.normalize_name(text)`
function apply the same five steps:

1. `unaccent` — strip diacritics (`é → e`, `ł → l`, `ñ → n`, …).
2. `lower` — case-fold.
3. Replace any non-alphanumeric run with a single space.
4. Collapse repeated whitespace.
5. Trim.

Examples (all collapse to the same key):

```
"Tiffany Chapon"   → "tiffany chapon"
"TIFFANY  CHAPON"  → "tiffany chapon"
"Tiffany-Chapon"   → "tiffany chapon"
"  Chapon, Tiffany " → "chapon tiffany"   ← different order, won't match unless added as a separate ref
```

> ⚠️ If you change the normalization rule in one place, change it in the other.
> Tests should pin this contract.

### 4.2 No automatic fuzzy matching

The importer **does not** fuzzy-match. A row matches an athlete only if its
normalized `Name` is an exact key in our explicit `externalAthleteRefs` list.

Future tooling can use `pg_trgm` similarity (already indexed in
`public.athletes.normalized_name` and `app.athlete_aliases.normalized_alias`)
to suggest candidates for `app.athlete_name_resolution_queue`, but a human still
approves them.

## 5. `source_record_key` vs `source_row_hash`

`athlete_results` and `source_meets` have two source columns:

| Column | Purpose |
|---|---|
| `source_record_key` | **Stable natural key** of the row in the source. Used as the unique key for idempotent upserts (`unique (source_id, source_record_key)`). |
| `source_row_hash` | **Content hash** (SHA-256). Used to detect "did anything change about this row?" |

Result key format:

```
{normalized_external_name}|{meet_date}|{normalized_meet_name}|{federation}|{event}|{equipment}|{division}
```

Meet key format:

```
{normalized_meet_name}|{date}|{federation}
```

Upsert pipeline per batch:

1. Fetch existing `(source_record_key, source_row_hash)` for the batch from Supabase.
2. Classify each row:
   - not in DB → **insert**
   - in DB, hash matches → **skip** (don't even send)
   - in DB, hash differs → **update**
3. Send the union of inserts + updates with PostgREST upsert.
4. Counters reflect inserted / updated / skipped accurately.

## 6. Running the import manually from GitHub Actions

1. Go to **Actions → "Sync competition + OpenIPF / OpenPowerlifting data"**.
2. Click **"Run workflow"**.
3. Fill in:
   - **competition** — `worlds-2026` by default.
   - **source** — `both` (default), `openipf`, or `openpowerlifting`
4. Click **Run workflow**.

The workflow also runs on a cron (Mondays 03:17 UTC).

### Required secrets

In repo **Settings → Secrets and variables → Actions**:

| Secret | Value |
|---|---|
| `SUPABASE_URL` | e.g. `https://abcdefgh.supabase.co` |
| `SUPABASE_SECRET_KEY` | Supabase `service_role` key (treat as production secret). |

The frontend (Blazor WASM) **never** sees `SUPABASE_SECRET_KEY` — it stays in
GitHub Secrets and is only read inside the workflow runner.

### Running locally

```bash
export SUPABASE_URL="https://<project>.supabase.co"
export SUPABASE_SECRET_KEY="<service_role key>"

# Syncs the competition definition, then both openipf and openpowerlifting by default.
./scripts/sync-supabase.sh

# Or sync a single source.
./scripts/sync-supabase.sh \
  src/TotalCall.Client/wwwroot/data/competitions/worlds-2026.json \
  openipf
```

## 7. Security model (no Dashboard setup required)

All tables live in the `public` schema. There is **nothing to configure in
the Supabase Dashboard** — the default `Exposed schemas = public` is exactly
what we want.

Access control is enforced by two cooperating layers, both defined in the
migration:

| Layer | Public-data tables (`athletes`, `athlete_results`, `source_meets`, `data_sources`, `athlete_history_view`) | Admin tables (`athlete_aliases`, `athlete_external_ids`, `import_runs`, `import_errors`, `athlete_name_resolution_queue`) |
|---|---|---|
| RLS | `enable row level security` + `SELECT` policy `using (true)` | `enable row level security` + **no policies** → all rows hidden |
| Grants | `GRANT SELECT ... TO anon, authenticated` | `REVOKE ALL ... FROM anon, authenticated` |
| service_role | bypasses RLS, has full DML | bypasses RLS, has full DML |

The importer writes via PostgREST using the `service_role` key. service_role
bypasses RLS, so it can read and write the admin tables despite the deny rules
above. The frontend uses the `anon` key and can only see the public-data tables.

### 7.1 Public import status for the UI

The frontend shows a small source/update line next to athlete history, for
example:

```
Źródło: OpenIPF · Ostatnio zaktualizowano: 27.05.2026, 03:17
```

It does **not** read `public.import_runs` directly. The table stays admin-only.
Instead, the frontend calls this stable, security-definer RPC through the
publishable key:

```http
GET /rest/v1/rpc/get_athlete_data_import_status?p_source=openipf
```

The RPC returns only:

| Field | Meaning |
|---|---|
| `source` | Source code, e.g. `openipf`. |
| `source_label` | Public display label, e.g. `OpenIPF`. |
| `last_successful_import_at` | Latest `finished_at` timestamp for a successful import of that source, or `null`. |

If the RPC fails or Supabase is not configured, athlete history still loads.
The UI falls back to a source-only label (`Źródło: OpenIPF`) and does not expose
errors to the user.

The Blazor app must use `Supabase:PublishableKey` only. Never put
`SUPABASE_SECRET_KEY` / the `service_role` key in `wwwroot/appsettings.json` or
any frontend-visible configuration.

### 7.2 Public athlete analytics for the UI

Athlete form analytics are calculated in Supabase, not from local JSON. The
frontend calls this public RPC through the publishable key:

```http
GET /rest/v1/rpc/get_athlete_analytics?p_athlete_slug=women-47-chapon-tiffany
```

The RPC returns one aggregate row for the athlete, including start count, best
and latest total, last-3/last-5 total averages, total trend, best lift/score
metrics, and attempt success rates.

Attempt success follows the OpenPowerlifting convention imported into
`athlete_results`: positive attempt value = made lift, negative value = missed
lift, and `NULL`/`0` = no attempt, excluded from the denominator.

## 8. Verifying data after an import

In the SQL editor:

```sql
-- Last 5 import runs
select id, source_id, status, started_at, finished_at,
       rows_processed, rows_inserted, rows_updated, rows_skipped, rows_failed
from public.import_runs
order by started_at desc
limit 5;

-- Athletes that we have in DB and history counts
select a.slug, a.display_name, count(r.id) as results
from public.athletes a
left join public.athlete_results r on r.athlete_id = a.id
group by a.id
order by results desc;

-- Tiffany Chapon's last 10 results
select meet_date, meet_name, federation, equipment,
       best_squat_kg, best_bench_kg, best_deadlift_kg, total_kg, place, dots_points, goodlift_points
from public.athlete_history_view
where athlete_slug = 'women-47-chapon-tiffany'
order by meet_date desc
limit 10;

-- Errors from the most recent run
select e.row_index, e.error_code, e.error_message
from public.import_errors e
join public.import_runs r on r.id = e.run_id
where r.id = (select id from public.import_runs order by started_at desc limit 1)
order by e.row_index;

-- Public status exposed to the frontend
select source, source_label, last_successful_import_at
from public.get_athlete_data_import_status('openipf');

-- Public analytics exposed to the frontend
select *
from public.get_athlete_analytics('women-47-chapon-tiffany');
```

## 9. Limitations of v1

- **No automatic fuzzy matching.** Every athlete must have explicit
  `externalAthleteRefs` to be imported. This is intentional — accidental
  matches would silently corrupt history data.
- **No deduplication of duplicate names across athletes.** If two roster
  athletes happen to share the same OPL `Name`, the importer maps both to
  whichever one was inserted into the lookup index first. Add `externalId`
  to disambiguate.
- **No retroactive cleanup.** If an athlete is removed from the roster, their
  history rows stay in `athlete_results`. Cleanup is a manual SQL task for now.
- **Single source per import run.** The workflow imports one source at a
  time; run twice if you want both `openipf` and `openpowerlifting`.
- **OPL `Username` is not in the bulk CSV.** Stable `externalId` mapping
  requires pulling `lifter-data` separately. v1 relies on names only.
- **Unmapped roster athletes** (no `externalAthleteRefs` for the requested
  source) are logged as warnings and skipped — they will not appear in
  history until refs are added.
- **No retry/queueing on transient REST failures.** A single batch upsert
  failure terminates the run and marks it `failed`.

## 10. Adding a new athlete to import

1. Make sure the athlete is in the competition JSON's `athletes[]` list.
2. Add `externalAthleteRefs` with the source(s) you want to import from.
3. Commit and merge.
4. Re-run the workflow manually, or wait for the cron.

## 11. Adding a new data source in the future

1. Insert a row into `public.data_sources` (or commit a migration that does so):
   ```sql
   insert into public.data_sources (code, name, url, attribution)
   values ('my-source', 'My Source', 'https://...', 'attribution text')
   on conflict (code) do nothing;
   ```
2. Add `--source my-source` support:
   - If the source has a known CSV/ZIP URL, add a case in `DefaultCsvUrl()`.
     Otherwise rely on `--csv-url`.
   - Map source-specific column names in `ParseOplRow()` if they differ
     from OPL conventions.
3. Add `externalAthleteRefs` entries with `"source": "my-source"` to the
   relevant athletes.
4. Run the importer.

The schema does not need to change — `data_sources`, `athlete_results`,
`source_meets`, `athlete_aliases`, and `athlete_external_ids` are all
source-agnostic.
