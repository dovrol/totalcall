# TotalCall MCP

Local MCP server for AI-first TotalCall operations. Built on the official
`ModelContextProtocol` C# SDK (stdio transport); tools are declared with
`[McpServerTool]` and input schemas are generated from their parameters.

Run after building the solution:

```bash
./scripts/with-supabase-keychain.sh --account local -- dotnet run --project ops/mcp/TotalCall.Mcp/TotalCall.Mcp.csproj --no-build
```

Use `--account production` only for intentional production operations.

Current tools:

- `totalcall_runtime_status` — read-only
- `totalcall_list_competition_files` — read-only
- `totalcall_validate_competition_config` — read-only
- `totalcall_dry_run_results_import` — read-only / dry-run
- `totalcall_import_results` — write (guarded)
- `totalcall_sync_competition` — write (guarded)

The SDK writes only MCP JSON-RPC messages to stdout; diagnostic logs go to
stderr (`LogToStandardErrorThreshold`). Tool paths must stay inside the repo.

`totalcall_dry_run_results_import` needs Supabase operations credentials because
results validation compares the file against the published Supabase competition
config. It does not write data.

`totalcall_import_results` writes official result groups and recalculates score
snapshots. It requires Supabase operations credentials and an explicit
`confirmation` argument equal to `import <competitionId>`.

`totalcall_sync_competition` syncs a competition JSON file to Supabase
(`competitions` + `competition_versions`) and publishes the version. The
competition id is read from the file; it requires Supabase operations
credentials and an explicit `confirmation` argument equal to
`sync <competitionId>`. Typical AI flow: write the JSON into
`ops/data/competitions/`, `totalcall_validate_competition_config`, then
`totalcall_sync_competition`.
