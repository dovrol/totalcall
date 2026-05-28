-- Public, minimal import-status contract for the frontend.
-- Keeps public.import_runs hidden while exposing only source metadata and
-- the latest successful import timestamp for the selected source.

create or replace function public.get_athlete_data_import_status(p_source text default 'openipf')
returns table (
  source text,
  source_label text,
  last_successful_import_at timestamptz
)
language sql
stable
security definer
set search_path = public
as $$
  select
    ds.code as source,
    ds.name as source_label,
    max(ir.finished_at) as last_successful_import_at
  from public.data_sources ds
  left join public.import_runs ir
    on ir.source_id = ds.id
   and ir.status = 'success'
   and ir.finished_at is not null
  where ds.code = p_source
  group by ds.code, ds.name
  limit 1;
$$;

comment on function public.get_athlete_data_import_status(text) is
  'Public frontend-safe import metadata. Does not expose import_runs rows or counters.';

revoke all on function public.get_athlete_data_import_status(text) from public;
grant execute on function public.get_athlete_data_import_status(text) to anon, authenticated;
