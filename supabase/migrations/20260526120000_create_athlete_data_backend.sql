-- supabase/migrations/20260526120000_create_athlete_data_backend.sql
-- TotalCall - initial athlete data backend.
-- Provides: athletes registry, history of meet results, import tracking,
-- alias/external-id mapping (backend-only), and a read-only public API
-- surface for the Blazor WebAssembly frontend.
-- No user / prediction tables in this migration.

set check_function_bodies = off;

-- ============================================================
-- Schemas
-- ============================================================
create schema if not exists app;
comment on schema app is 'TotalCall internal admin/import tables. NOT exposed via PostgREST.';

-- ============================================================
-- Extensions
-- ============================================================
create extension if not exists "pgcrypto" with schema extensions;
create extension if not exists "pg_trgm"  with schema extensions;
create extension if not exists "unaccent" with schema extensions;

-- ============================================================
-- Helpers
-- ============================================================

-- Canonical name normalization. MUST be used both by SQL upserts and by the
-- importer (recommended: invoke this function on the SQL side so the
-- normalization rule lives in exactly one place).
-- Steps:
--   1. unaccent  -> strip diacritics
--   2. lower     -> case-fold
--   3. non-alphanumeric -> single space (handles punctuation, dots, hyphens)
--   4. collapse runs of whitespace
--   5. trim
create or replace function public.normalize_name(input text)
returns text
language sql
immutable
parallel safe
as $$
  select case
    when input is null then null
    else nullif(
      trim(
        regexp_replace(
          regexp_replace(
            lower(extensions.unaccent(input)),
            '[^a-z0-9]+', ' ', 'g'
          ),
          '\s+', ' ', 'g'
        )
      ),
      ''
    )
  end;
$$;

create or replace function public.tg_set_updated_at()
returns trigger
language plpgsql
as $$
begin
  new.updated_at = now();
  return new;
end;
$$;

-- ============================================================
-- Enums
-- ============================================================
do $$ begin
  create type public.athlete_sex as enum ('male', 'female', 'mx', 'unspecified');
exception when duplicate_object then null; end $$;

comment on type public.athlete_sex is
  'Athlete sex normalization. OpenPowerlifting mapping: M -> male, F -> female, Mx -> mx, empty/unknown -> unspecified.';

do $$ begin
  create type app.import_status as enum ('running', 'success', 'failed', 'partial');
exception when duplicate_object then null; end $$;

do $$ begin
  create type app.name_resolution_status as enum ('pending', 'resolved', 'ignored');
exception when duplicate_object then null; end $$;

-- ============================================================
-- Reference: data sources
-- ============================================================
create table if not exists public.data_sources (
  id          uuid primary key default gen_random_uuid(),
  code        text not null unique,
  name        text not null,
  url         text,
  attribution text,
  created_at  timestamptz not null default now()
);
comment on table public.data_sources is
  'Catalog of external data providers (openpowerlifting, openipf, manual).';

insert into public.data_sources (code, name, url, attribution) values
  ('openpowerlifting', 'OpenPowerlifting',
   'https://www.openpowerlifting.org',
   'This page uses data from the OpenPowerlifting project, https://www.openpowerlifting.org.'),
  ('openipf', 'OpenIPF',
   'https://www.openipf.org',
   'OpenIPF dataset (subset of OpenPowerlifting limited to IPF-sanctioned meets).'),
  ('manual', 'Manual entry', null, null)
on conflict (code) do nothing;

-- ============================================================
-- Athletes
-- ============================================================
create table if not exists public.athletes (
  id              uuid primary key default gen_random_uuid(),
  slug            text not null unique,
  display_name    text not null,
  normalized_name text generated always as (public.normalize_name(display_name)) stored,
  sex             public.athlete_sex not null default 'unspecified',
  country_code    text,
  country_name    text,
  primary_external_source text,
  primary_external_id     text,
  notes           text,
  created_at      timestamptz not null default now(),
  updated_at      timestamptz not null default now()
);

comment on column public.athletes.slug is
  'Public stable TotalCall identifier, kebab-case from display_name (e.g. "chapon-tiffany"). Must not encode mutable attributes like weight class.';

create index if not exists athletes_normalized_name_trgm_idx
  on public.athletes using gin (normalized_name extensions.gin_trgm_ops);
create index if not exists athletes_sex_idx          on public.athletes (sex);
create index if not exists athletes_country_code_idx on public.athletes (country_code);

create trigger athletes_set_updated_at
  before update on public.athletes
  for each row execute function public.tg_set_updated_at();

-- ============================================================
-- Source meets (= "meet" from OpenPowerlifting, distinct from
-- TotalCall "competition" which is a prediction event).
-- ============================================================
create table if not exists public.source_meets (
  id                  uuid primary key default gen_random_uuid(),
  source_id           uuid not null references public.data_sources(id),
  source_record_key   text not null,         -- stable natural key from source (e.g. OPL MeetPath)
  source_row_hash     text,                  -- content hash for change detection
  name                text not null,
  normalized_name     text generated always as (public.normalize_name(name)) stored,
  date                date not null,
  federation          text,
  parent_federation   text,
  country             text,
  state               text,
  town                text,
  tested              boolean,
  sanctioned          boolean,
  created_at          timestamptz not null default now(),
  updated_at          timestamptz not null default now(),
  unique (source_id, source_record_key)
);

create index if not exists source_meets_date_idx       on public.source_meets (date desc);
create index if not exists source_meets_federation_idx on public.source_meets (federation);
create index if not exists source_meets_normalized_name_idx
  on public.source_meets (normalized_name);

create trigger source_meets_set_updated_at
  before update on public.source_meets
  for each row execute function public.tg_set_updated_at();

comment on table public.source_meets is
  'Meets imported from external sources (OPL/OpenIPF). Distinct from TotalCall predictive competitions.';

-- ============================================================
-- Athlete results (fact table - one row per athlete per event per meet)
-- ============================================================
create table if not exists public.athlete_results (
  id              uuid primary key default gen_random_uuid(),
  athlete_id      uuid not null references public.athletes(id) on delete cascade,
  source_meet_id  uuid references public.source_meets(id) on delete set null,
  source_id       uuid not null references public.data_sources(id),

  -- Idempotency:
  --   source_record_key : stable natural key of the source row
  --                       (e.g. "<MeetPath>|<Name>|<Event>|<Equipment>")
  --   source_row_hash   : hash of the row contents, used only to detect "is this row dirty?"
  source_record_key text not null,
  source_row_hash   text,

  -- Denormalized meet info (fast filtering / sorting without join)
  meet_date   date not null,
  meet_name   text,
  federation  text,

  event           text,  -- SBD, S, B, D, SB, SD, BD
  equipment       text,  -- Raw, Wraps, Single-ply, Multi-ply, Straps, Unlimited
  division        text,
  age_class       text,
  birth_year_class text,
  age             numeric(4,1),
  bodyweight_kg   numeric(6,2),
  weight_class_kg text,

  squat1_kg numeric(6,2), squat2_kg numeric(6,2), squat3_kg numeric(6,2), squat4_kg numeric(6,2),
  best_squat_kg numeric(6,2),
  bench1_kg numeric(6,2), bench2_kg numeric(6,2), bench3_kg numeric(6,2), bench4_kg numeric(6,2),
  best_bench_kg numeric(6,2),
  deadlift1_kg numeric(6,2), deadlift2_kg numeric(6,2), deadlift3_kg numeric(6,2), deadlift4_kg numeric(6,2),
  best_deadlift_kg numeric(6,2),
  total_kg numeric(7,2),

  place         text,    -- "1", "DQ", "DD", "G", "NS"
  place_numeric int,     -- parsed when meaningful

  dots_points          numeric(7,2),
  wilks_points         numeric(7,2),
  glossbrenner_points  numeric(7,2),
  goodlift_points      numeric(7,2),

  tested boolean,

  import_run_id uuid,
  imported_at   timestamptz not null default now(),
  updated_at    timestamptz not null default now(),

  unique (source_id, source_record_key)
);

comment on column public.athlete_results.source_record_key is
  'Stable natural key of the source row (e.g. OPL "<MeetPath>|<Name>|<Event>|<Equipment>"). Used for idempotent upserts.';
comment on column public.athlete_results.source_row_hash is
  'Hash of the source row contents. Used to detect changes and skip no-op updates.';
comment on column public.athlete_results.squat1_kg is
  'OpenPowerlifting attempt convention: positive = good lift, negative = missed lift, NULL/0 = not attempted.';

create index if not exists athlete_results_athlete_date_idx
  on public.athlete_results (athlete_id, meet_date desc);
create index if not exists athlete_results_athlete_total_idx
  on public.athlete_results (athlete_id, total_kg desc nulls last);
create index if not exists athlete_results_source_meet_idx
  on public.athlete_results (source_meet_id);
create index if not exists athlete_results_event_equipment_idx
  on public.athlete_results (event, equipment);
create index if not exists athlete_results_row_hash_idx
  on public.athlete_results (source_row_hash);

create trigger athlete_results_set_updated_at
  before update on public.athlete_results
  for each row execute function public.tg_set_updated_at();

comment on table public.athlete_results is
  'One row per athlete per event per meet, sourced from external datasets (OPL/OpenIPF).';

-- ============================================================
-- Internal (app schema): aliases, external IDs, import runs/errors, name queue
-- ============================================================
create table if not exists app.athlete_aliases (
  id               uuid primary key default gen_random_uuid(),
  athlete_id       uuid not null references public.athletes(id) on delete cascade,
  alias_name       text not null,
  normalized_alias text generated always as (public.normalize_name(alias_name)) stored,
  source           text,
  confidence       numeric(4,3) check (confidence is null or (confidence >= 0 and confidence <= 1)),
  created_at       timestamptz not null default now(),
  unique (athlete_id, alias_name)
);
create index if not exists athlete_aliases_normalized_alias_idx
  on app.athlete_aliases (normalized_alias);
create index if not exists athlete_aliases_normalized_alias_trgm_idx
  on app.athlete_aliases using gin (normalized_alias extensions.gin_trgm_ops);
create index if not exists athlete_aliases_athlete_id_idx
  on app.athlete_aliases (athlete_id);

create table if not exists app.athlete_external_ids (
  id          uuid primary key default gen_random_uuid(),
  athlete_id  uuid not null references public.athletes(id) on delete cascade,
  source_id   uuid not null references public.data_sources(id),
  external_id text not null,
  created_at  timestamptz not null default now(),
  unique (source_id, external_id)
);
create index if not exists athlete_external_ids_athlete_id_idx
  on app.athlete_external_ids (athlete_id);

create table if not exists app.import_runs (
  id                     uuid primary key default gen_random_uuid(),
  source_id              uuid not null references public.data_sources(id),
  status                 app.import_status not null default 'running',
  started_at             timestamptz not null default now(),
  finished_at            timestamptz,
  source_url             text,
  source_dataset_version text,
  triggered_by           text,
  rows_processed int not null default 0,
  rows_inserted  int not null default 0,
  rows_updated   int not null default 0,
  rows_skipped   int not null default 0,
  rows_failed    int not null default 0,
  error_message  text,
  notes          jsonb
);
create index if not exists import_runs_started_at_idx    on app.import_runs (started_at desc);
create index if not exists import_runs_source_status_idx on app.import_runs (source_id, status);

alter table public.athlete_results
  add constraint athlete_results_import_run_fk
  foreign key (import_run_id) references app.import_runs(id) on delete set null;

create table if not exists app.import_errors (
  id            uuid primary key default gen_random_uuid(),
  run_id        uuid not null references app.import_runs(id) on delete cascade,
  row_index     int,
  error_code    text,
  error_message text,
  raw_row       jsonb,
  created_at    timestamptz not null default now()
);
create index if not exists import_errors_run_id_idx on app.import_errors (run_id);

create table if not exists app.athlete_name_resolution_queue (
  id                  uuid primary key default gen_random_uuid(),
  source_id           uuid references public.data_sources(id),
  source_external_id  text,
  source_display_name text not null,
  normalized_name     text generated always as (public.normalize_name(source_display_name)) stored,
  status              app.name_resolution_status not null default 'pending',
  candidates          jsonb,
  resolved_athlete_id uuid references public.athletes(id),
  created_at          timestamptz not null default now(),
  resolved_at         timestamptz
);
create index if not exists name_resolution_status_idx
  on app.athlete_name_resolution_queue (status);

-- ============================================================
-- Public view (frontend DTO surface). security_invoker = true so
-- the view respects RLS of the base tables when queried by anon.
-- ============================================================
create or replace view public.athlete_history_view
with (security_invoker = true) as
select
  ar.id              as result_id,
  a.slug             as athlete_slug,
  a.display_name     as athlete_display_name,
  a.sex              as athlete_sex,
  a.country_code     as athlete_country_code,
  ar.meet_date,
  ar.meet_name,
  ar.federation,
  ar.equipment,
  ar.division,
  ar.event,
  ar.bodyweight_kg,
  ar.weight_class_kg,
  ar.age_class,
  ar.squat1_kg, ar.squat2_kg, ar.squat3_kg, ar.squat4_kg, ar.best_squat_kg,
  ar.bench1_kg, ar.bench2_kg, ar.bench3_kg, ar.bench4_kg, ar.best_bench_kg,
  ar.deadlift1_kg, ar.deadlift2_kg, ar.deadlift3_kg, ar.deadlift4_kg, ar.best_deadlift_kg,
  ar.total_kg,
  ar.place, ar.place_numeric,
  ar.dots_points, ar.goodlift_points, ar.wilks_points
from public.athlete_results ar
join public.athletes a on a.id = ar.athlete_id;

comment on view public.athlete_history_view is
  'Flattened athlete result + meet info for frontend consumption. Runs with invoker privileges so base-table RLS applies.';

-- ============================================================
-- RLS
-- ============================================================
alter table public.data_sources    enable row level security;
alter table public.athletes        enable row level security;
alter table public.source_meets    enable row level security;
alter table public.athlete_results enable row level security;
alter table app.athlete_aliases                enable row level security;
alter table app.athlete_external_ids           enable row level security;
alter table app.import_runs                    enable row level security;
alter table app.import_errors                  enable row level security;
alter table app.athlete_name_resolution_queue  enable row level security;

create policy "data_sources public read"    on public.data_sources    for select to anon, authenticated using (true);
create policy "athletes public read"        on public.athletes        for select to anon, authenticated using (true);
create policy "source_meets public read"    on public.source_meets    for select to anon, authenticated using (true);
create policy "athlete_results public read" on public.athlete_results for select to anon, authenticated using (true);

-- No anon/authenticated policies on app.* => RLS denies them.
-- service_role bypasses RLS by Supabase default.

-- ============================================================
-- Grants
-- ============================================================
grant usage on schema public to anon, authenticated;
revoke all on schema app from public;
revoke all on schema app from anon, authenticated;
grant usage on schema app to service_role;

grant select on
  public.data_sources,
  public.athletes,
  public.source_meets,
  public.athlete_results,
  public.athlete_history_view
to anon, authenticated;

grant all on all tables    in schema app to service_role;
grant all on all sequences in schema app to service_role;
grant all on all routines  in schema app to service_role;

alter default privileges in schema app grant all on tables    to service_role;
alter default privileges in schema app grant all on sequences to service_role;
