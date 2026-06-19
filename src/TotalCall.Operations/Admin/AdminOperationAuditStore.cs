using System.Text.Json.Nodes;
using TotalCall.Operations.Supabase;

namespace TotalCall.Operations.Admin;

public static class AdminOperationType
{
    public const string CompetitionConfigPublish = "competition_config_publish";
    public const string OfficialResultsImport = "official_results_import";
}

public static class AdminOperationStatus
{
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Blocked = "blocked";
}

public sealed record AdminOperationAuditOptions
{
    public string? SupabaseUrl { get; init; }
    public string? SupabaseSecretKey { get; init; }
}

public sealed record AdminOperationAuditRecord(
    string? Id,
    string OperationType,
    string Status,
    string TargetType,
    string? TargetId,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string? TriggeredBy,
    string? RuntimeOrigin,
    JsonObject InputJson,
    JsonObject ResultJson,
    IReadOnlyList<OperationLogEntry> Logs,
    string? ErrorMessage);

public sealed class AdminOperationAuditStore
{
    public async Task<string> RecordAsync(
        AdminOperationAuditOptions options,
        AdminOperationAuditRecord record,
        CancellationToken ct)
    {
        var supabase = CreateClient(options);
        var returned = await supabase.InsertReturningAsync(
            "public",
            "admin_operation_runs",
            new JsonArray { ToInsertRow(record) },
            ct);

        return returned.OfType<JsonObject>().FirstOrDefault()?["id"]?.ToString()
            ?? throw new InvalidOperationException("admin_operation_runs insert returned no id.");
    }

    public async Task<IReadOnlyList<AdminOperationAuditRecord>> ListRecentAsync(
        AdminOperationAuditOptions options,
        int limit,
        CancellationToken ct)
    {
        var supabase = CreateClient(options);
        var safeLimit = Math.Clamp(limit, 1, 100);
        var rows = await supabase.GetAsync(
            "public",
            "admin_operation_runs",
            "select=id,operation_type,status,target_type,target_id,started_at,finished_at,triggered_by,runtime_origin,input_json,result_json,logs_json,error_message" +
            $"&order=started_at.desc&limit={safeLimit}",
            ct);

        return rows
            .OfType<JsonObject>()
            .Select(ParseRecord)
            .ToArray();
    }

    public static JsonObject ToInsertRow(AdminOperationAuditRecord record) => new()
    {
        ["operation_type"] = SanitizeText(record.OperationType),
        ["status"] = SanitizeText(record.Status),
        ["target_type"] = SanitizeText(record.TargetType),
        ["target_id"] = SanitizeText(record.TargetId),
        ["started_at"] = record.StartedAt.ToString("o"),
        ["finished_at"] = record.FinishedAt.ToString("o"),
        ["triggered_by"] = SanitizeText(record.TriggeredBy),
        ["runtime_origin"] = SanitizeText(record.RuntimeOrigin),
        ["input_json"] = SanitizeJson(record.InputJson),
        ["result_json"] = SanitizeJson(record.ResultJson),
        ["logs_json"] = LogsToJson(record.Logs),
        ["error_message"] = SanitizeText(record.ErrorMessage)
    };

    private static SupabaseRestClient CreateClient(AdminOperationAuditOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SupabaseUrl) ||
            string.IsNullOrWhiteSpace(options.SupabaseSecretKey))
        {
            throw new InvalidOperationException("SUPABASE_URL and SUPABASE_SECRET_KEY must be set.");
        }

        return new SupabaseRestClient(options.SupabaseUrl, options.SupabaseSecretKey);
    }

    private static AdminOperationAuditRecord ParseRecord(JsonObject row)
    {
        var logs = (row["logs_json"] as JsonArray)?
            .OfType<JsonObject>()
            .Select(log => new OperationLogEntry(
                log["level"]?.ToString() ?? "info",
                log["message"]?.ToString() ?? string.Empty))
            .ToArray() ?? [];

        return new AdminOperationAuditRecord(
            row["id"]?.ToString(),
            row["operation_type"]?.ToString() ?? string.Empty,
            row["status"]?.ToString() ?? string.Empty,
            row["target_type"]?.ToString() ?? string.Empty,
            row["target_id"]?.ToString(),
            ParseDateTimeOffset(row["started_at"]) ?? DateTimeOffset.MinValue,
            ParseDateTimeOffset(row["finished_at"]) ?? DateTimeOffset.MinValue,
            row["triggered_by"]?.ToString(),
            row["runtime_origin"]?.ToString(),
            row["input_json"] as JsonObject ?? new JsonObject(),
            row["result_json"] as JsonObject ?? new JsonObject(),
            logs,
            row["error_message"]?.ToString());
    }

    private static JsonArray LogsToJson(IReadOnlyList<OperationLogEntry> logs)
    {
        var result = new JsonArray();
        foreach (var log in logs)
        {
            result.Add(new JsonObject
            {
                ["level"] = SanitizeText(log.Level),
                ["message"] = SanitizeText(log.Message)
            });
        }

        return result;
    }

    private static JsonObject SanitizeJson(JsonObject source)
    {
        var result = new JsonObject();
        foreach (var (key, value) in source)
        {
            if (IsSensitiveKey(key))
            {
                result[key] = "[redacted]";
                continue;
            }

            result[key] = value?.DeepClone();
        }

        return result;
    }

    private static bool IsSensitiveKey(string key) =>
        key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("service_role", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("answers_json", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("email", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("user_id", StringComparison.OrdinalIgnoreCase);

    private static string? SanitizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var sanitized = value;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            sanitized = sanitized.Replace(home, "~", StringComparison.Ordinal);
        }

        sanitized = RedactEnvAssignment(sanitized, "SUPABASE_SECRET_KEY");
        sanitized = RedactEnvAssignment(sanitized, "SUPABASE_SERVICE_ROLE_KEY");
        return sanitized;
    }

    private static string RedactEnvAssignment(string value, string name)
    {
        var index = value.IndexOf($"{name}=", StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            var valueStart = index + name.Length + 1;
            var valueEnd = valueStart;
            while (valueEnd < value.Length && !char.IsWhiteSpace(value[valueEnd]))
            {
                valueEnd++;
            }

            value = value[..index] + $"{name}=[redacted]" + value[valueEnd..];
            index = value.IndexOf($"{name}=", index + name.Length + "[redacted]".Length + 1, StringComparison.OrdinalIgnoreCase);
        }

        return value;
    }

    private static DateTimeOffset? ParseDateTimeOffset(JsonNode? node)
    {
        var value = node?.ToString();
        return DateTimeOffset.TryParse(value, out var parsed)
            ? parsed
            : null;
    }
}
