-- Submit flow and public participants list v1.
-- prediction_submissions remains private owner-only; public reads use the safe view below.

create or replace function public.submit_prediction(
  p_competition_id text,
  p_answers_json jsonb,
  p_app_version text,
  p_schema_version integer
)
returns table (
  status public.prediction_submission_status,
  submitted_at timestamptz
)
language plpgsql
security invoker
set search_path = public
as $$
begin
  if auth.uid() is null then
    raise exception 'submit_prediction requires an authenticated user'
      using errcode = '28000';
  end if;

  if length(trim(coalesce(p_competition_id, ''))) = 0 then
    raise exception 'competition_id is required'
      using errcode = '22023';
  end if;

  if p_answers_json is null or jsonb_typeof(p_answers_json) <> 'object' then
    raise exception 'answers_json must be a JSON object'
      using errcode = '22023';
  end if;

  if p_schema_version is null or p_schema_version <= 0 then
    raise exception 'schema_version must be positive'
      using errcode = '22023';
  end if;

  return query
  insert into public.prediction_submissions (
    user_id,
    competition_id,
    status,
    answers_json,
    app_version,
    schema_version,
    submitted_at
  )
  values (
    auth.uid(),
    trim(p_competition_id),
    'submitted',
    p_answers_json,
    p_app_version,
    p_schema_version,
    now()
  )
  on conflict (user_id, competition_id) do update
  set
    status = 'submitted',
    answers_json = excluded.answers_json,
    app_version = excluded.app_version,
    schema_version = excluded.schema_version,
    submitted_at = coalesce(
      public.prediction_submissions.submitted_at,
      now()
    )
  returning
    prediction_submissions.status,
    prediction_submissions.submitted_at;
end;
$$;

revoke all on function public.submit_prediction(text, jsonb, text, integer)
  from public, anon, authenticated;
grant execute on function public.submit_prediction(text, jsonb, text, integer)
  to authenticated;

create or replace view public.prediction_participants_public
with (security_barrier = true)
as
select
  ps.competition_id,
  coalesce(
    nullif(trim(p.display_name), ''),
    'ChalkyBenchGoblin' || right(regexp_replace(md5(ps.id::text), '[^0-9]', '', 'g') || '0000', 4)
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
