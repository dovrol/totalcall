-- Private user profiles and Cloud Save v1 for prediction drafts.

do $$ begin
  create type public.prediction_submission_status as enum ('draft', 'submitted');
exception when duplicate_object then null; end $$;

create schema if not exists private;
revoke all on schema private from public, anon, authenticated;

-- ============================================================
-- profiles (private, owner-only)
-- ============================================================
create table public.profiles (
  id           uuid primary key default auth.uid() references auth.users(id) on delete cascade,
  display_name text,
  created_at   timestamptz not null default now(),
  updated_at   timestamptz not null default now()
);

create trigger profiles_set_updated_at
  before update on public.profiles
  for each row execute function public.tg_set_updated_at();

create or replace function private.handle_new_auth_user()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
  insert into public.profiles (id, display_name)
  values (
    new.id,
    coalesce(new.raw_user_meta_data ->> 'display_name', new.raw_user_meta_data ->> 'full_name')
  )
  on conflict (id) do nothing;

  return new;
end;
$$;

create trigger on_auth_user_created
  after insert on auth.users
  for each row execute function private.handle_new_auth_user();

-- Backfill profiles for users created before this migration.
insert into public.profiles (id, display_name)
select
  id,
  coalesce(raw_user_meta_data ->> 'display_name', raw_user_meta_data ->> 'full_name')
from auth.users
on conflict (id) do nothing;

-- ============================================================
-- prediction_submissions (private, owner-only)
-- ============================================================
create table public.prediction_submissions (
  id             uuid primary key default gen_random_uuid(),
  user_id        uuid not null default auth.uid() references auth.users(id) on delete cascade,
  competition_id text not null check (length(trim(competition_id)) > 0),
  status         public.prediction_submission_status not null default 'draft',
  answers_json   jsonb not null check (jsonb_typeof(answers_json) = 'object'),
  app_version    text not null,
  schema_version integer not null check (schema_version > 0),
  created_at     timestamptz not null default now(),
  updated_at     timestamptz not null default now(),
  submitted_at   timestamptz,
  unique (user_id, competition_id),
  check (
    (status = 'draft' and submitted_at is null)
    or
    (status = 'submitted' and submitted_at is not null)
  )
);

comment on column public.prediction_submissions.answers_json is
  'Private JSONB snapshot of the complete PredictionSet used to restore a local draft.';

create index prediction_submissions_user_updated_idx
  on public.prediction_submissions (user_id, updated_at desc);

create trigger prediction_submissions_set_updated_at
  before update on public.prediction_submissions
  for each row execute function public.tg_set_updated_at();

-- ============================================================
-- RLS
-- ============================================================
alter table public.profiles enable row level security;
alter table public.prediction_submissions enable row level security;

create policy "profiles owner read"
  on public.profiles
  for select
  to authenticated
  using ((select auth.uid()) = id);

create policy "profiles owner insert"
  on public.profiles
  for insert
  to authenticated
  with check ((select auth.uid()) = id);

create policy "profiles owner update"
  on public.profiles
  for update
  to authenticated
  using ((select auth.uid()) = id)
  with check ((select auth.uid()) = id);

create policy "prediction submissions owner read"
  on public.prediction_submissions
  for select
  to authenticated
  using ((select auth.uid()) = user_id);

create policy "prediction submissions owner insert"
  on public.prediction_submissions
  for insert
  to authenticated
  with check ((select auth.uid()) = user_id);

create policy "prediction submissions owner update"
  on public.prediction_submissions
  for update
  to authenticated
  using ((select auth.uid()) = user_id)
  with check ((select auth.uid()) = user_id);

-- No anon grants and no public-read policy. Delete is intentionally not granted in v1.
revoke all on public.profiles from public, anon, authenticated;
revoke all on public.prediction_submissions from public, anon, authenticated;
grant select, insert, update on public.profiles to authenticated;
grant select, insert, update on public.prediction_submissions to authenticated;

grant all on public.profiles to service_role;
grant all on public.prediction_submissions to service_role;

revoke all on function private.handle_new_auth_user() from public, anon, authenticated;
