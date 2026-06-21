# TotalCall MCP

Local MCP server for AI-first TotalCall operations.

Run after building the solution:

```bash
dotnet run --project ops/mcp/TotalCall.Mcp/TotalCall.Mcp.csproj --no-build
```

Current tools are read-only or dry-run only:

- `totalcall_runtime_status`
- `totalcall_list_competition_files`
- `totalcall_validate_competition_config`
- `totalcall_dry_run_results_import`

The server reads JSON-RPC messages from stdin and writes only MCP messages to
stdout. Diagnostic logs go to stderr. Tool paths must stay inside the repo.

`totalcall_dry_run_results_import` may need `SUPABASE_URL` and
`SUPABASE_SECRET_KEY` because results validation compares the file against the
published Supabase competition config. It does not write data.
