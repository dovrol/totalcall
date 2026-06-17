-- Public user boards: open another user's locked picks + results (read-only) from
-- standings. We expose a non-PII opaque board reference (the snapshot id) on the
-- leaderboard, and a get_public_board() RPC that returns that user's submitted picks
-- and results breakdown. No email / user_id / drafts are exposed; boards only exist
-- once a user is ranked (scored_groups_count > 0, i.e. after lock + import).

-- ============================================================
-- 1) Leaderboard gains board_ref (the snapshot id) so a row can be opened.
-- ============================================================
drop function if exists public.get_competition_leaderboard(text);

create or replace function public.get_competition_leaderboard(p_competition_id text)
returns table (
  "position"          integer,
  board_ref           uuid,
  display_name        text,
  total_points        numeric,
  scored_groups_count integer,
  total_groups_count  integer,
  status              text,
  last_calculated_at  timestamptz
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
      order by total_points desc, lower(display_name) asc, calculated_at asc, snapshot_id asc
    )::integer as "position",
    snapshot_id as board_ref,
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
  'Public leaderboard from score snapshots. board_ref is an opaque snapshot id used to open a read-only public board. No email/user_id/answers.';

revoke all on function public.get_competition_leaderboard(text) from public, anon, authenticated;
grant execute on function public.get_competition_leaderboard(text) to anon, authenticated;

-- ============================================================
-- 2) Public board for one ranked user: submitted picks + results breakdown.
-- Privacy/security contract (deliberately narrow):
--   * returns ONLY the picks array (picks_json = answers_json -> 'answers'),
--     never the full PredictionSet — no localUserId / app+schema version /
--     submissionStatus / savedAt, and no future PredictionSet field can leak;
--   * the submission is pinned to the snapshot's prediction_submission_id (the
--     exact row that was scored) and must be status='submitted' with submitted_at
--     set, so a draft can never be returned by any path;
--   * reveal/lock is explicit: the competition's prediction_lock_at must have passed
--     (on top of "a scored snapshot exists", which only happens post-import);
--   * never exposes email / user_id.
-- ============================================================
drop function if exists public.get_public_board(text, uuid);

create or replace function public.get_public_board(p_competition_id text, p_board_ref uuid)
returns table (
  display_name        text,
  "rank"              integer,
  total_points        numeric,
  scored_groups_count integer,
  total_groups_count  integer,
  status              text,
  picks_json          jsonb,
  breakdown_json      jsonb,
  last_calculated_at  timestamptz
)
language sql
security definer
stable
set search_path = public
as $$
  with ranked as (
    select
      ss.id,
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
    coalesce(
      nullif(trim(p.display_name), ''),
      public.powerlifting_display_name_candidate(ss.user_id::text, 0)
    ) as display_name,
    r.rank,
    ss.total_points,
    ss.scored_groups_count,
    ss.total_groups_count,
    ss.status::text as status,
    -- only the picks array, never the whole submission (no PII / metadata / future fields)
    coalesce(sub.answers_json -> 'answers', sub.answers_json -> 'Answers') as picks_json,
    ss.breakdown_json,
    ss.calculated_at as last_calculated_at
  from public.score_snapshots ss
  left join public.profiles p on p.id = ss.user_id
  left join ranked r on r.id = ss.id
  join public.prediction_submissions sub on sub.id = ss.prediction_submission_id
  join public.competitions c on c.id = ss.competition_id
  where ss.competition_id = p_competition_id
    and ss.id = p_board_ref
    and ss.scored_groups_count > 0
    and sub.status = 'submitted'
    and sub.submitted_at is not null
    and c.prediction_lock_at is not null
    and c.prediction_lock_at <= now();
$$;

comment on function public.get_public_board(text, uuid) is
  'Read-only public board for one ranked user after lock: display_name + submitted picks (answers only) + results breakdown. No email/user_id/localUserId/drafts/metadata.';

revoke all on function public.get_public_board(text, uuid) from public, anon, authenticated;
grant execute on function public.get_public_board(text, uuid) to anon, authenticated;
