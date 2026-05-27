using TotalCall.OplImporter;

if (args.Length == 0 || args.Contains("-h") || args.Contains("--help"))
{
    PrintHelp();
    return 0;
}

string? competitionJson = null;
string? source = "openipf";
string? csvUrl = null;
string? localCsv = null;
bool dryRun = false;
string triggeredBy = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ? "github-actions" : "manual";

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
    Console.Error.WriteLine("[error] --competition-json is required.");
    return 1;
}
if (string.IsNullOrWhiteSpace(source))
{
    Console.Error.WriteLine("[error] --source is required.");
    return 1;
}

var options = new ImporterOptions
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

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    return await new Importer().RunAsync(options, cts.Token);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[fatal] {ex}");
    return 99;
}

static void PrintHelp()
{
    Console.WriteLine("""
        TotalCall OpenPowerlifting/OpenIPF importer.

        Usage:
          TotalCall.OplImporter
            --competition-json <path>            Path to the TotalCall competition JSON
                                                 (e.g. src/TotalCall.Client/wwwroot/data/competitions/worlds-2026.json)
            --source <openipf|openpowerlifting>  Data source code (must exist in data_sources). Default: openipf.
            [--csv-url <url>]                    Override CSV/ZIP download URL.
            [--local-csv <path>]                 Skip download; read CSV from a local file.
            [--dry-run]                          Scan and report matches; no DB writes.
            [--triggered-by <text>]              Label written to import_runs.triggered_by.

        Environment variables:
          SUPABASE_URL          Supabase project REST URL, e.g. https://abcdefgh.supabase.co
          SUPABASE_SECRET_KEY   Supabase service_role key. Required unless --dry-run.

        Admin tables are protected by RLS + REVOKE — no Dashboard configuration needed.
        """);
}
