-- Competition definitions in Supabase: metadata + lifecycle + versioned config (JSONB).
-- The frontend loads the published config from here; the JSON in the repo stays a
-- dev/import source and runtime fallback. The backend enforces the prediction window
-- (deadline/status) via a trigger on prediction_submissions, so neither the deadline
-- nor the config version is trusted from the client.

-- ============================================================
-- competition_status enum (mirrors Domain/Competitions/CompetitionStatus.cs)
-- ============================================================
do $$ begin
  create type public.competition_status as enum ('upcoming', 'locked', 'completed', 'archived');
exception when duplicate_object then null; end $$;

-- ============================================================
-- competitions: metadata + lifecycle + pointer to the published version
-- ============================================================
create table public.competitions (
  id                   text primary key check (length(trim(id)) > 0),
  slug                 text not null unique check (length(trim(slug)) > 0),
  name                 text not null,
  federation           text,
  status               public.competition_status not null default 'upcoming',
  start_date           timestamptz,
  end_date             timestamptz,
  prediction_open_at   timestamptz,
  prediction_lock_at   timestamptz,
  published_version_id uuid,
  summary              jsonb check (summary is null or jsonb_typeof(summary) = 'object'),
  created_at           timestamptz not null default now(),
  updated_at           timestamptz not null default now()
);

create trigger competitions_set_updated_at
  before update on public.competitions
  for each row execute function public.tg_set_updated_at();

comment on column public.competitions.summary is
  'Small public snapshot for the competition list (mirrors the legacy index.json entry).';
comment on column public.competitions.prediction_lock_at is
  'Authoritative submission deadline. Enforced by the prediction window trigger.';

-- ============================================================
-- competition_versions: versioned published config as JSONB
-- ============================================================
create table public.competition_versions (
  id             uuid primary key default gen_random_uuid(),
  competition_id text not null references public.competitions(id) on delete cascade,
  version        text not null check (length(trim(version)) > 0),
  config         jsonb not null check (jsonb_typeof(config) = 'object'),
  created_at     timestamptz not null default now(),
  published_at   timestamptz,
  unique (competition_id, version)
);

create index competition_versions_competition_idx
  on public.competition_versions (competition_id);

comment on column public.competition_versions.config is
  'Full competition config (same shape as the legacy {slug}.json) deserialized by the client.';

-- Resolve the circular reference: a competition points at its live config version.
alter table public.competitions
  add constraint competitions_published_version_fk
  foreign key (published_version_id)
  references public.competition_versions(id)
  on delete set null;

-- ============================================================
-- prediction_submissions: link a submission to the config version it was made on
-- ============================================================
alter table public.prediction_submissions
  add column competition_version_id uuid
  references public.competition_versions(id) on delete set null;

comment on column public.prediction_submissions.competition_version_id is
  'Config version the submission was made on. Stamped server-side by the prediction '
  'window trigger; never trusted from the client.';

-- ============================================================
-- Backend deadline/status enforcement (defense in depth).
-- Fires for draft upserts AND submit_prediction. Stamps competition_version_id
-- authoritatively from the competition's published version. Real end-user writes
-- (auth.uid() is not null) are blocked once the window is closed; service_role /
-- admin writes (auth.uid() is null) bypass the lock. Unknown competitions degrade
-- to the previous unguarded behaviour so local/legacy data keeps working until synced.
-- ============================================================
create or replace function public.enforce_prediction_window()
returns trigger
language plpgsql
security invoker
set search_path = public
as $$
declare
  v_competition public.competitions%rowtype;
begin
  select * into v_competition
  from public.competitions
  where id = new.competition_id;

  if not found then
    return new;
  end if;

  -- The config version is always set by the server, never by the client.
  new.competition_version_id := v_competition.published_version_id;

  if auth.uid() is not null then
    if v_competition.status in ('locked', 'completed', 'archived') then
      raise exception 'Predictions for % are locked (status %).',
        v_competition.id, v_competition.status
        using errcode = '42501';
    end if;

    if v_competition.prediction_lock_at is not null
       and now() > v_competition.prediction_lock_at then
      raise exception 'Predictions for % closed at %.',
        v_competition.id, v_competition.prediction_lock_at
        using errcode = '42501';
    end if;

    if v_competition.prediction_open_at is not null
       and now() < v_competition.prediction_open_at then
      raise exception 'Predictions for % open at %.',
        v_competition.id, v_competition.prediction_open_at
        using errcode = '42501';
    end if;
  end if;

  return new;
end;
$$;

create trigger prediction_submissions_enforce_window
  before insert or update on public.prediction_submissions
  for each row execute function public.enforce_prediction_window();

-- ============================================================
-- RLS + grants.
-- competitions / competition_versions hold non-PII public config: public read,
-- writes via service_role (the sync tool) / admin only.
-- ============================================================
alter table public.competitions enable row level security;
alter table public.competition_versions enable row level security;

create policy "competitions public read"
  on public.competitions
  for select
  to anon, authenticated
  using (true);

create policy "competition versions public read"
  on public.competition_versions
  for select
  to anon, authenticated
  using (true);

revoke all on public.competitions from public, anon, authenticated;
revoke all on public.competition_versions from public, anon, authenticated;
grant select on public.competitions to anon, authenticated;
grant select on public.competition_versions to anon, authenticated;
grant all on public.competitions to service_role;
grant all on public.competition_versions to service_role;

-- ============================================================
-- Public projection for the competition list (small; excludes the heavy config).
-- ============================================================
create view public.published_competitions
with (security_invoker = true)
as
  select
    c.id,
    c.slug,
    c.name,
    c.status,
    c.start_date,
    c.end_date,
    c.prediction_open_at,
    c.prediction_lock_at,
    c.summary,
    v.version
  from public.competitions c
  left join public.competition_versions v
    on v.id = c.published_version_id;

grant select on public.published_competitions to anon, authenticated;

-- ============================================================
-- Public RPC for a single competition's published config.
-- ============================================================
create or replace function public.get_published_competition(p_slug text)
returns table (
  config             jsonb,
  status             public.competition_status,
  prediction_open_at timestamptz,
  prediction_lock_at timestamptz,
  version            text
)
language sql
security definer
stable
set search_path = public
as $$
  select
    v.config,
    c.status,
    c.prediction_open_at,
    c.prediction_lock_at,
    v.version
  from public.competitions c
  join public.competition_versions v
    on v.id = c.published_version_id
  where c.slug = p_slug;
$$;

revoke all on function public.get_published_competition(text) from public;
grant execute on function public.get_published_competition(text) to anon, authenticated;
