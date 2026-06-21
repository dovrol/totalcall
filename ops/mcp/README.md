# TotalCall MCP

Local MCP server for AI-first TotalCall operations.

Run after building the solution:

```bash
./scripts/with-supabase-keychain.sh --account local -- dotnet run --project ops/mcp/TotalCall.Mcp/TotalCall.Mcp.csproj --no-build
```

Use `--account production` only for intentional production operations.

Current tools:

- `totalcall_runtime_status`
- `totalcall_list_competition_files`
- `totalcall_validate_competition_config`
- `totalcall_dry_run_results_import`
- `totalcall_import_results`

The server reads JSON-RPC messages from stdin and writes only MCP messages to
stdout. Diagnostic logs go to stderr. Tool paths must stay inside the repo.

`totalcall_dry_run_results_import` needs Supabase operations credentials because
results validation compares the file against the published Supabase competition
config. It does not write data.

`totalcall_import_results` writes official result groups and recalculates score
snapshots. It requires Supabase operations credentials and an explicit
`confirmation` argument equal to `import <competitionId>`.
