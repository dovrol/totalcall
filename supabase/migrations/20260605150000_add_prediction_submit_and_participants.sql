-- Submit flow and public participants list v1.
-- prediction_submissions remains private owner-only; the public participants
-- projection is exposed through public.get_competition_participants in the
-- display-names migration (a security definer function, not a view).

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
