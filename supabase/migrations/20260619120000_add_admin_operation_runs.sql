-- Admin operation audit trail for local/server-side operational workflows.
-- This is private service-role data. It is intentionally not exposed through
-- public RPCs or frontend publishable-key access.

create table if not exists public.admin_operation_runs (
  id             uuid primary key default gen_random_uuid(),
  operation_type text not null,
  status         text not null check (status in ('succeeded', 'failed', 'blocked')),
  target_type    text not null,
  target_id      text,
  started_at     timestamptz not null,
  finished_at    timestamptz not null,
  triggered_by   text,
  runtime_origin text,
  input_json     jsonb not null default '{}'::jsonb,
  result_json    jsonb not null default '{}'::jsonb,
  logs_json      jsonb not null default '[]'::jsonb,
  error_message  text,
  created_at     timestamptz not null default now()
);

create index if not exists admin_operation_runs_started_at_idx
  on public.admin_operation_runs (started_at desc);

create index if not exists admin_operation_runs_operation_status_idx
  on public.admin_operation_runs (operation_type, status, started_at desc);

create index if not exists admin_operation_runs_target_idx
  on public.admin_operation_runs (target_type, target_id, started_at desc);

comment on table public.admin_operation_runs is
  'ADMIN: server-side operation audit trail. Protected by RLS + REVOKE; invisible to anon/authenticated.';

alter table public.admin_operation_runs enable row level security;

revoke all on public.admin_operation_runs from public, anon, authenticated;
grant all on public.admin_operation_runs to service_role;
