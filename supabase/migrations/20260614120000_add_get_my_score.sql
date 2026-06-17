-- Results Mode: expose the *caller's own* score snapshot to the client.
-- score_snapshots is a private service-role table (see 20260611130000). The
-- public leaderboard RPC only returns rank/points. The results board needs the
-- signed-in user's own per-category breakdown, so this RPC returns exactly one
-- row scoped to auth.uid() (the caller's own data: athleteIds only, no email /
-- user_id / other users' answers). Rank mirrors get_competition_leaderboard().

create or replace function public.get_my_score(p_competition_id text)
returns table (
  "rank"               integer,
  total_points         numeric,
  scored_groups_count  integer,
  total_groups_count   integer,
  status               text,
  results_hash         text,
  rules_version        text,
  breakdown_json       jsonb,
  last_calculated_at   timestamptz
)
language sql
security definer
stable
set search_path = public
as $$
  with ranked as (
    select
      ss.user_id,
      row_number() over (
        order by
          ss.total_points desc,
          lower(coalesce(
            nullif(trim(p.display_name), ''),
            public.powerlifting_display_name_candidate(ss.user_id::text, 0)
          )) asc,
          ss.calculated_at asc,
          ss.id asc
      )::integer as rank
    from public.score_snapshots ss
    left join public.profiles p on p.id = ss.user_id
    where ss.competition_id = p_competition_id
      and ss.scored_groups_count > 0
  )
  select
    r.rank,
    ss.total_points,
    ss.scored_groups_count,
    ss.total_groups_count,
    ss.status::text as status,
    ss.results_hash,
    ss.rules_version,
    ss.breakdown_json,
    ss.calculated_at as last_calculated_at
  from public.score_snapshots ss
  left join ranked r on r.user_id = ss.user_id
  where ss.competition_id = p_competition_id
    and ss.user_id = auth.uid();
$$;

comment on function public.get_my_score(text) is
  'Returns the calling user''s own score snapshot (incl. breakdown_json + computed rank) for one competition. Scoped to auth.uid().';

revoke all on function public.get_my_score(text) from public, anon, authenticated;
grant execute on function public.get_my_score(text) to authenticated;
