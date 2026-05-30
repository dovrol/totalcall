-- Expose athlete-history source metadata and calculate analytics per source.

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
  ar.dots_points, ar.goodlift_points, ar.wilks_points,
  ds.code             as source_code
from public.athlete_results ar
join public.athletes a on a.id = ar.athlete_id
join public.data_sources ds on ds.id = ar.source_id;

grant select on public.athlete_history_view to anon, authenticated;

drop function if exists public.get_athlete_analytics(text);

create function public.get_athlete_analytics(
  p_athlete_slug text,
  p_source text default 'openipf'
)
returns table (
  athlete_slug text,
  starts_count int,
  best_total_kg numeric,
  last_total_kg numeric,
  last3_avg_total_kg numeric,
  last5_avg_total_kg numeric,
  total_trend_kg numeric,
  best_squat_kg numeric,
  best_bench_kg numeric,
  best_deadlift_kg numeric,
  best_dots_points numeric,
  best_goodlift_points numeric,
  squat_success_rate numeric,
  squat_successful_attempts int,
  squat_counted_attempts int,
  bench_success_rate numeric,
  bench_successful_attempts int,
  bench_counted_attempts int,
  deadlift_success_rate numeric,
  deadlift_successful_attempts int,
  deadlift_counted_attempts int,
  overall_success_rate numeric,
  overall_successful_attempts int,
  overall_counted_attempts int,
  third_attempt_success_rate numeric,
  third_attempt_successful_attempts int,
  third_attempt_counted_attempts int
)
language sql
stable
security invoker
set search_path = public
as $$
  with results as (
    select ar.*
    from public.athlete_results ar
    join public.athletes a on a.id = ar.athlete_id
    join public.data_sources ds on ds.id = ar.source_id
    where a.slug = p_athlete_slug
      and ds.code = p_source
  ),
  total_values as (
    select
      r.total_kg,
      row_number() over (
        order by r.meet_date desc, r.imported_at desc, r.id desc
      ) as total_rank
    from results r
    where r.total_kg is not null
      and r.total_kg > 0
      and (
        r.event is null
        or upper(regexp_replace(r.event, '[^a-zA-Z]', '', 'g')) = 'SBD'
      )
  ),
  total_stats as (
    select
      (select tv.total_kg from total_values tv where tv.total_rank = 1) as last_total_kg,
      case
        when count(*) filter (where tv.total_rank <= 3) = 3
          then round(avg(tv.total_kg) filter (where tv.total_rank <= 3), 2)
        else null
      end as last3_avg_total_kg,
      case
        when count(*) filter (where tv.total_rank <= 5) = 5
          then round(avg(tv.total_kg) filter (where tv.total_rank <= 5), 2)
        else null
      end as last5_avg_total_kg,
      (select tv.total_kg from total_values tv where tv.total_rank = 1) -
      (select tv.total_kg from total_values tv where tv.total_rank = 2) as total_trend_kg
    from total_values tv
  ),
  attempts as (
    select attempt.lift, attempt.attempt_no, attempt.value_kg
    from results r
    cross join lateral (
      values
        ('squat', 1, r.squat1_kg),
        ('squat', 2, r.squat2_kg),
        ('squat', 3, r.squat3_kg),
        ('squat', 4, r.squat4_kg),
        ('bench', 1, r.bench1_kg),
        ('bench', 2, r.bench2_kg),
        ('bench', 3, r.bench3_kg),
        ('bench', 4, r.bench4_kg),
        ('deadlift', 1, r.deadlift1_kg),
        ('deadlift', 2, r.deadlift2_kg),
        ('deadlift', 3, r.deadlift3_kg),
        ('deadlift', 4, r.deadlift4_kg)
    ) as attempt(lift, attempt_no, value_kg)
    where attempt.value_kg is not null
      and attempt.value_kg <> 0
  ),
  attempt_counts as (
    select
      count(*) filter (where lift = 'squat' and value_kg > 0)::int as squat_successful_attempts,
      count(*) filter (where lift = 'squat')::int as squat_counted_attempts,
      count(*) filter (where lift = 'bench' and value_kg > 0)::int as bench_successful_attempts,
      count(*) filter (where lift = 'bench')::int as bench_counted_attempts,
      count(*) filter (where lift = 'deadlift' and value_kg > 0)::int as deadlift_successful_attempts,
      count(*) filter (where lift = 'deadlift')::int as deadlift_counted_attempts,
      count(*) filter (where value_kg > 0)::int as overall_successful_attempts,
      count(*)::int as overall_counted_attempts,
      count(*) filter (where attempt_no = 3 and value_kg > 0)::int as third_attempt_successful_attempts,
      count(*) filter (where attempt_no = 3)::int as third_attempt_counted_attempts
    from attempts
  ),
  summary as (
    select
      count(*)::int as starts_count,
      max(total_kg) filter (
        where total_kg > 0
          and (
            event is null
            or upper(regexp_replace(event, '[^a-zA-Z]', '', 'g')) = 'SBD'
          )
      ) as best_total_kg,
      max(best_squat_kg) filter (where best_squat_kg > 0) as best_squat_kg,
      max(best_bench_kg) filter (where best_bench_kg > 0) as best_bench_kg,
      max(best_deadlift_kg) filter (where best_deadlift_kg > 0) as best_deadlift_kg,
      max(dots_points) filter (where dots_points > 0) as best_dots_points,
      max(goodlift_points) filter (where goodlift_points > 0) as best_goodlift_points
    from results
  )
  select
    p_athlete_slug as athlete_slug,
    s.starts_count,
    s.best_total_kg,
    ts.last_total_kg,
    ts.last3_avg_total_kg,
    ts.last5_avg_total_kg,
    ts.total_trend_kg,
    s.best_squat_kg,
    s.best_bench_kg,
    s.best_deadlift_kg,
    s.best_dots_points,
    s.best_goodlift_points,
    round(100 * ac.squat_successful_attempts::numeric / nullif(ac.squat_counted_attempts, 0), 1) as squat_success_rate,
    ac.squat_successful_attempts,
    ac.squat_counted_attempts,
    round(100 * ac.bench_successful_attempts::numeric / nullif(ac.bench_counted_attempts, 0), 1) as bench_success_rate,
    ac.bench_successful_attempts,
    ac.bench_counted_attempts,
    round(100 * ac.deadlift_successful_attempts::numeric / nullif(ac.deadlift_counted_attempts, 0), 1) as deadlift_success_rate,
    ac.deadlift_successful_attempts,
    ac.deadlift_counted_attempts,
    round(100 * ac.overall_successful_attempts::numeric / nullif(ac.overall_counted_attempts, 0), 1) as overall_success_rate,
    ac.overall_successful_attempts,
    ac.overall_counted_attempts,
    round(100 * ac.third_attempt_successful_attempts::numeric / nullif(ac.third_attempt_counted_attempts, 0), 1) as third_attempt_success_rate,
    ac.third_attempt_successful_attempts,
    ac.third_attempt_counted_attempts
  from summary s
  cross join total_stats ts
  cross join attempt_counts ac
  where s.starts_count > 0;
$$;

comment on function public.get_athlete_analytics(text, text) is
  'Public frontend-safe source-specific athlete analytics. Attempt success ignores NULL and 0 values; positive attempts are successful and negative attempts are missed.';

revoke all on function public.get_athlete_analytics(text, text) from public;
grant execute on function public.get_athlete_analytics(text, text) to anon, authenticated;
