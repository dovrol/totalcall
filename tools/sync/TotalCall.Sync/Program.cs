using TotalCall.Sync.Athletes;
using TotalCall.Sync.Competitions;
using TotalCall.Sync.DevScenarios;
using TotalCall.Sync.Results;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    PrintHelp();
    return 0;
}

var command = args[0];
var rest = args[1..];

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    return command switch
    {
        "athletes" => await RunAthletesAsync(rest, cts.Token),
        "competition" => await RunCompetitionAsync(rest, cts.Token),
        "results" => await RunResultsAsync(rest, cts.Token),
        "scenario" => await RunScenarioAsync(rest, cts.Token),
        _ => UnknownCommand(command)
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[fatal] {ex}");
    return 99;
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"[error] Unknown command: {command}");
    PrintHelp();
    return 1;
}

// ---- scenario: local-only dev data for product states ----
static async Task<int> RunScenarioAsync(string[] args, CancellationToken ct)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("[error] scenario: <name> is required.");
        DevScenarioRunner.PrintScenarioList();
        return 1;
    }

    var scenarioName = args[0];
    string? baseCompetitionJson = null;
    var local = false;
    var triggeredBy = "dev-scenario";

    for (var i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--local":
                local = true; break;
            case "--base-competition-json":
                baseCompetitionJson = args[++i]; break;
            case "--triggered-by":
                triggeredBy = args[++i]; break;
            default:
                Console.Error.WriteLine($"[error] Unknown argument: {args[i]}");
                PrintHelp();
                return 1;
        }
    }

    var options = new DevScenarioOptions
    {
        ScenarioName = scenarioName,
        Local = local,
        BaseCompetitionJsonPath = baseCompetitionJson,
        SupabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL"),
        SupabaseSecretKey =
            Environment.GetEnvironmentVariable("SUPABASE_SECRET_KEY")
            ?? Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY"),
        TriggeredBy = triggeredBy
    };

    return await new DevScenarioRunner().RunAsync(options, ct);
}

// ---- athletes: import OpenPowerlifting/OpenIPF history (unchanged behaviour) ----
static async Task<int> RunAthletesAsync(string[] args, CancellationToken ct)
{
    string? competitionJson = null;
    string? source = "openipf";
    string? csvUrl = null;
    string? localCsv = null;
    var dryRun = false;
    var triggeredBy = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ? "github-actions" : "manual";

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--competition-json":
                competitionJson = args[++i]; break;
            case "--source":
                source = args[++i]; break;
            case "--csv-url":
                csvUrl = args[++i]; break;
            case "--local-csv":
                localCsv = args[++i]; break;
            case "--dry-run":
                // accept either "--dry-run" or "--dry-run true|false"
                if (i + 1 < args.Length && (args[i + 1] is "true" or "false"))
                {
                    dryRun = bool.Parse(args[++i]);
                }
                else
                {
                    dryRun = true;
                }
                break;
            case "--triggered-by":
                triggeredBy = args[++i]; break;
            default:
                Console.Error.WriteLine($"[error] Unknown argument: {args[i]}");
                PrintHelp();
                return 1;
        }
    }

    if (string.IsNullOrWhiteSpace(competitionJson))
    {
        Console.Error.WriteLine("[error] athletes: --competition-json is required.");
        return 1;
    }
    if (string.IsNullOrWhiteSpace(source))
    {
        Console.Error.WriteLine("[error] athletes: --source is required.");
        return 1;
    }

    var options = new AthleteImportOptions
    {
        CompetitionJsonPath = competitionJson!,
        Source = source!,
        CsvUrl = csvUrl,
        LocalCsvPath = localCsv,
        DryRun = dryRun,
        SupabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL"),
        SupabaseSecretKey = Environment.GetEnvironmentVariable("SUPABASE_SECRET_KEY"),
        TriggeredBy = triggeredBy
    };

    return await new AthleteImporter().RunAsync(options, ct);
}

// ---- competition: sync competition definition + versioned config to Supabase ----
static async Task<int> RunCompetitionAsync(string[] args, CancellationToken ct)
{
    string? competitionJson = null;
    var triggeredBy = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ? "github-actions" : "manual";

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--competition-json":
                competitionJson = args[++i]; break;
            case "--triggered-by":
                triggeredBy = args[++i]; break;
            default:
                Console.Error.WriteLine($"[error] Unknown argument: {args[i]}");
                PrintHelp();
                return 1;
        }
    }

    if (string.IsNullOrWhiteSpace(competitionJson))
    {
        Console.Error.WriteLine("[error] competition: --competition-json is required.");
        return 1;
    }

    var options = new CompetitionSyncOptions
    {
        CompetitionJsonPath = competitionJson!,
        SupabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL"),
        SupabaseSecretKey = Environment.GetEnvironmentVariable("SUPABASE_SECRET_KEY"),
        TriggeredBy = triggeredBy
    };

    return await new CompetitionDefinitionImporter().RunAsync(options, ct);
}

// ---- results: import official result groups and recalculate score snapshots ----
static async Task<int> RunResultsAsync(string[] args, CancellationToken ct)
{
    string? competitionId = null;
    string? resultsJson = null;
    var triggeredBy = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ? "github-actions" : "manual";

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--competition-id":
                competitionId = args[++i]; break;
            case "--results-json":
                resultsJson = args[++i]; break;
            case "--triggered-by":
                triggeredBy = args[++i]; break;
            default:
                Console.Error.WriteLine($"[error] Unknown argument: {args[i]}");
                PrintHelp();
                return 1;
        }
    }

    if (string.IsNullOrWhiteSpace(competitionId))
    {
        Console.Error.WriteLine("[error] results: --competition-id is required.");
        return 1;
    }

    if (string.IsNullOrWhiteSpace(resultsJson))
    {
        Console.Error.WriteLine("[error] results: --results-json is required.");
        return 1;
    }

    var options = new ResultsImportOptions
    {
        CompetitionId = competitionId!,
        ResultsJsonPath = resultsJson!,
        SupabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL"),
        SupabaseSecretKey = Environment.GetEnvironmentVariable("SUPABASE_SECRET_KEY"),
        TriggeredBy = triggeredBy
    };

    return await new OfficialResultsImporter().RunAsync(options, ct);
}

static void PrintHelp()
{
    Console.WriteLine("""
        TotalCall Supabase sync.

        Usage:
          TotalCall.Sync <command> [options]

        Commands:
          athletes      Import OpenPowerlifting/OpenIPF athlete history.
            --competition-json <path>            Path to the TotalCall competition JSON
                                                 (e.g. src/TotalCall.Client/wwwroot/data/competitions/worlds-2026.json)
            --source <openipf|openpowerlifting>  Data source code (must exist in data_sources). Default: openipf.
            [--csv-url <url>]                    Override CSV/ZIP download URL.
            [--local-csv <path>]                 Skip download; read CSV from a local file.
            [--dry-run]                          Scan and report matches; no DB writes.
            [--triggered-by <text>]              Label written to import_runs.triggered_by.

          competition   Sync competition definition + versioned config (JSONB) to Supabase.
            --competition-json <path>            Path to the TotalCall competition JSON.
            [--triggered-by <text>]              Reserved for audit metadata.

          results       Import official result groups and recalculate score snapshots.
            --competition-id <id>                Competition id, e.g. worlds-2026.
            --results-json <path>                Path to official results JSON.
            [--triggered-by <text>]              Label written into import metadata source fallback.

          scenario      Prepare local-only development states. Requires --local.
            <name>                               all-states, open-with-submissions,
                                                 locked-no-results, partial-results,
                                                 final-results or empty.
            --local                              Required guard; scenarios only run
                                                 against loopback Supabase URLs.
            [--base-competition-json <path>]     Source config to clone. Default:
                                                 src/TotalCall.Client/wwwroot/data/competitions/worlds-2026.json
            [--triggered-by <text>]              Label written into result import metadata.

        Environment variables:
          SUPABASE_URL          Supabase project REST URL, e.g. https://abcdefgh.supabase.co
          SUPABASE_SECRET_KEY   Supabase secret/service_role key. Required unless --dry-run (athletes only).
          SUPABASE_SERVICE_ROLE_KEY
                                Alternative service_role key name for scenario Auth Admin calls.

        Admin tables are protected by RLS + REVOKE — no Dashboard configuration needed.
        """);
}
