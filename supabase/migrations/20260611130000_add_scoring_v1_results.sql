-- Scoring v1 backend model.
-- Official results and score snapshots are private service-role tables. The
-- public app reads only get_competition_leaderboard(), which exposes a safe
-- projection without user_id, email, answers_json or drafts.

do $$ begin
  create type public.official_result_import_status as enum ('partial', 'final');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.official_result_group_status as enum ('pending', 'final');
exception when duplicate_object then null; end $$;

do $$ begin
  create type public.score_snapshot_status as enum ('partial', 'final');
exception when duplicate_object then null; end $$;

-- ============================================================
-- official_results: one mutable import header per competition.
-- ============================================================
create table public.official_results (
  id             uuid primary key default gen_random_uuid(),
  competition_id text not null references public.competitions(id) on delete cascade,
  status         public.official_result_import_status not null default 'partial',
  source         text,
  results_hash   text not null check (length(trim(results_hash)) > 0),
  imported_at    timestamptz not null default now(),
  updated_at     timestamptz not null default now(),
  unique (competition_id)
);

comment on table public.official_results is
  'Private service-role import header for official competition results.';
comment on column public.official_results.results_hash is
  'Canonical hash of the imported results file so the importer can detect unchanged payloads.';

create trigger official_results_set_updated_at
  before update on public.official_results
  for each row execute function public.tg_set_updated_at();

-- ============================================================
-- official_result_groups: one row per scoreable group/category/question.
-- Pending groups are not scored; final groups are included in snapshots.
-- ============================================================
create table public.official_result_groups (
  id                 uuid primary key default gen_random_uuid(),
  official_result_id uuid not null references public.official_results(id) on delete cascade,
  competition_id     text not null references public.competitions(id) on delete cascade,
  group_id           text not null check (length(trim(group_id)) > 0),
  question_id        text not null check (length(trim(question_id)) > 0),
  category_id        text,
  status             public.official_result_group_status not null default 'pending',
  result_json        jsonb not null check (jsonb_typeof(result_json) = 'object'),
  result_hash        text not null check (length(trim(result_hash)) > 0),
  imported_at        timestamptz not null default now(),
  updated_at         timestamptz not null default now(),
  finalized_at       timestamptz,
  unique (competition_id, group_id, question_id),
  check (
    (status = 'pending' and finalized_at is null)
    or
    (status = 'final' and finalized_at is not null)
  )
);

comment on table public.official_result_groups is
  'Private service-role official results per prediction group/question/category.';
comment on column public.official_result_groups.result_json is
  'Canonical result payload for the group. Scoring v1 expects placements[].';
comment on column public.official_result_groups.result_hash is
  'Canonical hash of result_json plus status for idempotent group imports.';

create index official_result_groups_competition_status_idx
  on public.official_result_groups (competition_id, status);

create trigger official_result_groups_set_updated_at
  before update on public.official_result_groups
  for each row execute function public.tg_set_updated_at();

-- ============================================================
-- score_snapshots: private calculated leaderboard rows.
-- They intentionally retain user_id/submission_id for backend idempotency only.
-- Public callers must use get_competition_leaderboard().
-- ============================================================
create table public.score_snapshots (
  id                       uuid primary key default gen_random_uuid(),
  competition_id           text not null references public.competitions(id) on delete cascade,
  competition_version_id   uuid not null references public.competition_versions(id) on delete restrict,
  prediction_submission_id uuid not null references public.prediction_submissions(id) on delete cascade,
  user_id                  uuid not null references auth.users(id) on delete cascade,
  total_points             numeric(10, 2) not null default 0,
  scored_groups_count      integer not null default 0 check (scored_groups_count >= 0),
  total_groups_count       integer not null default 0 check (total_groups_count >= 0),
  status                   public.score_snapshot_status not null default 'partial',
  results_hash             text not null check (length(trim(results_hash)) > 0),
  rules_version            text not null check (length(trim(rules_version)) > 0),
  breakdown_json           jsonb not null default '{}'::jsonb check (jsonb_typeof(breakdown_json) = 'object'),
  calculated_at            timestamptz not null default now(),
  unique (competition_id, user_id),
  unique (prediction_submission_id),
  check (scored_groups_count <= total_groups_count)
);

comment on table public.score_snapshots is
  'Private service-role scoring snapshots for submitted predictions. Public leaderboard reads a safe RPC projection.';
comment on column public.score_snapshots.breakdown_json is
  'Private scoring breakdown for future admin/debug use. Not exposed by public leaderboard.';

create index score_snapshots_public_order_idx
  on public.score_snapshots (competition_id, total_points desc, calculated_at desc);

-- ============================================================
-- RLS + grants.
-- ============================================================
alter table public.official_results enable row level security;
alter table public.official_result_groups enable row level security;
alter table public.score_snapshots enable row level security;

revoke all on public.official_results from public, anon, authenticated;
revoke all on public.official_result_groups from public, anon, authenticated;
revoke all on public.score_snapshots from public, anon, authenticated;

grant all on public.official_results to service_role;
grant all on public.official_result_groups to service_role;
grant all on public.score_snapshots to service_role;

-- ============================================================
-- Public leaderboard RPC.
-- ============================================================
create or replace function public.get_competition_leaderboard(p_competition_id text)
returns table (
  "position" integer,
  display_name text,
  total_points numeric,
  scored_groups_count integer,
  total_groups_count integer,
  status text,
  last_calculated_at timestamptz
)
language sql
security definer
stable
set search_path = public
as $$
  with safe_scores as (
    select
      ss.id as snapshot_id,
      coalesce(
        nullif(trim(p.display_name), ''),
        public.powerlifting_display_name_candidate(ss.user_id::text, 0)
      ) as display_name,
      ss.total_points,
      ss.scored_groups_count,
      ss.total_groups_count,
      ss.status::text as status,
      ss.calculated_at
    from public.score_snapshots ss
    left join public.profiles p on p.id = ss.user_id
    where ss.competition_id = p_competition_id
      and ss.scored_groups_count > 0
  )
  select
    row_number() over (
      order by
        total_points desc,
        lower(display_name) asc,
        calculated_at asc,
        snapshot_id asc
    )::integer as "position",
    display_name,
    total_points,
    scored_groups_count,
    total_groups_count,
    status,
    calculated_at as last_calculated_at
  from safe_scores
  order by 1;
$$;

comment on function public.get_competition_leaderboard(text) is
  'Public leaderboard built from score snapshots only. Does not expose email, user_id, answers_json or drafts.';

revoke all on function public.get_competition_leaderboard(text) from public, anon, authenticated;
grant execute on function public.get_competition_leaderboard(text) to anon, authenticated;
