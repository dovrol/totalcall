-- Aggregate attempt-success benchmark across a cohort of athletes in one request.
-- Replaces the client-side per-athlete analytics fan-out previously used to build
-- the category/sex/field attempt benchmark (one request instead of N).

create or replace function public.get_attempt_benchmark(
  p_athlete_slugs text[],
  p_source text default 'openipf'
)
returns table (
  athlete_count int,
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
    where a.slug = any(p_athlete_slugs)
      and ds.code = p_source
  ),
  cohort as (
    select count(distinct athlete_id)::int as athlete_count
    from results
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
  )
  select
    c.athlete_count,
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
  from cohort c
  cross join attempt_counts ac;
$$;

comment on function public.get_attempt_benchmark(text[], text) is
  'Public frontend-safe attempt-success aggregate across a cohort of athlete slugs for a source. Powers the category/sex/field benchmark in one request. Attempt success ignores NULL and 0 values; positive attempts are successful and negative attempts are missed.';

revoke all on function public.get_attempt_benchmark(text[], text) from public;
grant execute on function public.get_attempt_benchmark(text[], text) to anon, authenticated;
