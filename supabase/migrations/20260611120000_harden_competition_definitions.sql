-- Critical hardening for Competitions DB v1.
-- End-user prediction writes must always resolve to a known competition and its
-- currently published config version. Version rows are immutable once created.

-- ============================================================
-- prediction_submissions: fail closed for unknown/unpublished competitions
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
    raise exception 'Competition % is not configured.',
      new.competition_id
      using errcode = '23503';
  end if;

  if v_competition.published_version_id is null then
    raise exception 'Competition % has no published config version.',
      v_competition.id
      using errcode = '23503';
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

-- ============================================================
-- competition_versions: immutable identity + config
-- ============================================================
create or replace function public.enforce_competition_version_immutability()
returns trigger
language plpgsql
security invoker
set search_path = public
as $$
begin
  if old.competition_id is distinct from new.competition_id
     or old.version is distinct from new.version then
    raise exception 'Competition version identity is immutable.'
      using errcode = '23000';
  end if;

  if old.config is distinct from new.config then
    raise exception 'Competition version config is immutable. Create a new configVersion instead.'
      using errcode = '23000';
  end if;

  return new;
end;
$$;

drop trigger if exists competition_versions_enforce_immutability
  on public.competition_versions;
create trigger competition_versions_enforce_immutability
  before update of competition_id, version, config on public.competition_versions
  for each row execute function public.enforce_competition_version_immutability();

-- ============================================================
-- RLS/grants: no public direct access to all config versions
-- ============================================================
drop policy if exists "competition versions public read"
  on public.competition_versions;

create policy "competition versions published metadata read"
  on public.competition_versions
  for select
  to anon, authenticated
  using (
    exists (
      select 1
      from public.competitions c
      where c.published_version_id = competition_versions.id
    )
  );

revoke all on public.competition_versions from public, anon, authenticated;

-- Keep published_competitions usable with security_invoker=true while denying
-- public reads of the heavy config JSON and unpublished versions.
grant select (id, competition_id, version, published_at)
  on public.competition_versions to anon, authenticated;
grant all on public.competition_versions to service_role;

-- The public list should only expose competitions with a published runtime
-- version. The full config remains available only through get_published_competition.
create or replace view public.published_competitions
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
  join public.competition_versions v
    on v.id = c.published_version_id;

grant select on public.published_competitions to anon, authenticated;

revoke all on function public.enforce_competition_version_immutability()
  from public, anon, authenticated;
