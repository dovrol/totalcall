-- Lightweight public display names for profiles.
-- profiles remains private owner-only; public reads still go through safe views only.

create or replace function public.powerlifting_display_name_candidate(
  p_seed text,
  p_attempt integer default 0
)
returns text
language plpgsql
immutable
set search_path = public
as $$
declare
  v_adjectives constant text[] := array[
    'Chalky',
    'Based',
    'Unhinged',
    'Sleepy',
    'Spicy',
    'Massive',
    'Tiny',
    'Greedy',
    'Patient',
    'Deloaded',
    'Peaked',
    'Caffeinated',
    'Overhyped',
    'Suspicious',
    'Technical',
    'Humbled',
    'Locked',
    'Swole',
    'Shaky',
    'Aggressive'
  ];
  v_short_adjectives constant text[] := array[
    'Chalky',
    'Based',
    'Sleepy',
    'Spicy',
    'Tiny',
    'Greedy',
    'Peaked',
    'Locked',
    'Swole',
    'Shaky'
  ];
  v_nouns constant text[] := array[
    'BenchGoblin',
    'SquatWizard',
    'DeadliftGremlin',
    'DepthPolice',
    'WhiteLightEnjoyer',
    'RedLightSurvivor',
    'ProteinProphet',
    'BarbellOracle',
    'AttemptGoblin',
    'PlatformNPC',
    'ChalkBandit',
    'RackHeightMerchant',
    'SumoApologist',
    'ConventionalEnjoyer',
    'GLPointsGoblin',
    'DotsDealer',
    'ThirdAttemptHero',
    'OpenersOnly',
    'PeakWeekPanic',
    'MeetDayMenace',
    'TotalHunter',
    'BarLoader',
    'SquatPlug',
    'BenchNPC',
    'DeadliftMerchant'
  ];
  v_hash bytea := decode(md5(coalesce(p_seed, '') || ':' || coalesce(p_attempt, 0)::text), 'hex');
  v_adjective text;
  v_noun text;
  v_suffix text;
  v_candidate text;
begin
  v_adjective := v_adjectives[(get_byte(v_hash, 0) % array_length(v_adjectives, 1)) + 1];
  v_noun := v_nouns[(get_byte(v_hash, 1) % array_length(v_nouns, 1)) + 1];
  v_suffix := lpad(((get_byte(v_hash, 2) * 256 + get_byte(v_hash, 3)) % 10000)::text, 4, '0');
  v_candidate := v_adjective || v_noun || v_suffix;

  if length(v_candidate) > 32 then
    v_adjective := v_short_adjectives[(get_byte(v_hash, 4) % array_length(v_short_adjectives, 1)) + 1];
    v_candidate := v_adjective || v_noun || v_suffix;
  end if;

  return v_candidate;
end;
$$;

create or replace function private.default_profile_display_name(p_user_id uuid)
returns text
language plpgsql
set search_path = public, private
as $$
declare
  v_attempt integer;
  v_candidate text;
begin
  for v_attempt in 0..127 loop
    v_candidate := public.powerlifting_display_name_candidate(p_user_id::text, v_attempt);

    if not exists (
      select 1
      from public.profiles
      where lower(display_name) = lower(v_candidate)
        and id <> p_user_id
    ) then
      return v_candidate;
    end if;
  end loop;

  return public.powerlifting_display_name_candidate(p_user_id::text || ':fallback', 0);
end;
$$;

create or replace function private.validate_profile_display_name(p_display_name text)
returns text
language plpgsql
stable
set search_path = public
as $$
declare
  v_display_name text := btrim(coalesce(p_display_name, ''));
begin
  if length(v_display_name) = 0 then
    raise exception 'display_name is required'
      using errcode = '22023';
  end if;

  if length(v_display_name) > 32 then
    raise exception 'display_name must be at most 32 characters'
      using errcode = '22023';
  end if;

  if v_display_name !~ '^[A-Za-z0-9 ._-]+$' then
    raise exception 'display_name contains unsupported characters'
      using errcode = '22023';
  end if;

  return v_display_name;
end;
$$;

create or replace function private.profile_display_name_from_metadata(
  p_raw_user_meta_data jsonb,
  p_user_id uuid
)
returns text
language plpgsql
stable
set search_path = public, private
as $$
declare
  v_candidate text;
begin
  v_candidate := nullif(btrim(coalesce(p_raw_user_meta_data ->> 'display_name', '')), '');

  if v_candidate is null then
    v_candidate := nullif(btrim(coalesce(p_raw_user_meta_data ->> 'full_name', '')), '');
  end if;

  if v_candidate is null then
    return private.default_profile_display_name(p_user_id);
  end if;

  return private.validate_profile_display_name(v_candidate);
end;
$$;

create or replace function private.handle_new_auth_user()
returns trigger
language plpgsql
security definer
set search_path = public, private
as $$
begin
  insert into public.profiles (id, display_name)
  values (
    new.id,
    private.profile_display_name_from_metadata(new.raw_user_meta_data, new.id)
  )
  on conflict (id) do nothing;

  return new;
end;
$$;

create or replace function private.tg_normalize_profile_display_name()
returns trigger
language plpgsql
security definer
set search_path = public, private
as $$
begin
  if new.display_name is null or length(btrim(new.display_name)) = 0 then
    new.display_name := private.default_profile_display_name(new.id);
  else
    new.display_name := private.validate_profile_display_name(new.display_name);
  end if;

  return new;
end;
$$;

drop trigger if exists profiles_normalize_display_name on public.profiles;
create trigger profiles_normalize_display_name
  before insert or update of display_name on public.profiles
  for each row execute function private.tg_normalize_profile_display_name();

-- Backfill profiles created before default nick generation or with invalid public names.
update public.profiles
set display_name = private.default_profile_display_name(id)
where display_name is null
  or length(btrim(display_name)) = 0
  or length(btrim(display_name)) > 32
  or btrim(display_name) !~ '^[A-Za-z0-9 ._-]+$';

update public.profiles
set display_name = private.validate_profile_display_name(display_name)
where display_name <> btrim(display_name);

-- Resolve existing case-insensitive duplicates before adding the unique index.
with duplicate_profiles as (
  select
    id,
    row_number() over (
      partition by lower(display_name)
      order by created_at, id
    ) as duplicate_position
  from public.profiles
  where length(btrim(display_name)) > 0
)
update public.profiles p
set display_name = private.default_profile_display_name(p.id)
from duplicate_profiles d
where p.id = d.id
  and d.duplicate_position > 1;

alter table public.profiles
  alter column display_name set not null;

alter table public.profiles
  drop constraint if exists profiles_display_name_valid;

alter table public.profiles
  add constraint profiles_display_name_valid
  check (
    display_name = btrim(display_name)
    and length(display_name) between 1 and 32
    and display_name ~ '^[A-Za-z0-9 ._-]+$'
  );

drop index if exists public.profiles_display_name_ci_unique_idx;
create unique index profiles_display_name_ci_unique_idx
  on public.profiles (lower(display_name))
  where length(btrim(display_name)) > 0;

create or replace view public.prediction_participants_public
with (security_barrier = true)
as
select
  ps.competition_id,
  coalesce(
    nullif(trim(p.display_name), ''),
    public.powerlifting_display_name_candidate(ps.user_id::text, 0)
  ) as display_name,
  ps.submitted_at,
  ps.status::text as status
from public.prediction_submissions ps
left join public.profiles p on p.id = ps.user_id
where ps.status = 'submitted'
  and ps.submitted_at is not null;

comment on view public.prediction_participants_public is
  'Public participant list for submitted predictions only. Does not expose user_id, email or answers_json.';

revoke all on public.prediction_participants_public from public, anon, authenticated;
grant select on public.prediction_participants_public to anon, authenticated;

revoke all on function private.default_profile_display_name(uuid) from public, anon, authenticated;
revoke all on function private.validate_profile_display_name(text) from public, anon, authenticated;
revoke all on function private.profile_display_name_from_metadata(jsonb, uuid) from public, anon, authenticated;
revoke all on function private.tg_normalize_profile_display_name() from public, anon, authenticated;
revoke all on function private.handle_new_auth_user() from public, anon, authenticated;
revoke all on function public.powerlifting_display_name_candidate(text, integer) from public, anon, authenticated;
grant execute on function public.powerlifting_display_name_candidate(text, integer) to anon, authenticated;

-- Keep profiles owner-only. Do not grant public select on public.profiles.
revoke all on public.profiles from public, anon;
grant select, insert, update on public.profiles to authenticated;
