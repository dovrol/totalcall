using System.Text.Json.Nodes;
using TotalCall.Operations;
using TotalCall.Operations.Admin;

namespace TotalCall.Tests.Tools;

public sealed class AdminOperationAuditStoreTests
{
    [Fact]
    public void ToInsertRow_redacts_sensitive_json_and_log_values()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var record = new AdminOperationAuditRecord(
            Id: null,
            AdminOperationType.CompetitionConfigPublish,
            AdminOperationStatus.Failed,
            "competition",
            "worlds-2026",
            DateTimeOffset.Parse("2026-06-19T10:00:00Z"),
            DateTimeOffset.Parse("2026-06-19T10:00:01Z"),
            "admin-host",
            "http://127.0.0.1:54321",
            new JsonObject
            {
                ["competition_id"] = "worlds-2026",
                ["supabaseSecretKey"] = "secret-value",
                ["answers_json"] = new JsonObject { ["raw"] = true },
                ["email"] = "person@example.com",
                ["user_id"] = "user-1"
            },
            new JsonObject
            {
                ["published_version_id"] = "version-1",
                ["service_role_key"] = "service-secret"
            },
            [
                OperationLogEntry.Error($"{home}/repo failed SUPABASE_SECRET_KEY=abc123 next"),
                OperationLogEntry.Info("safe")
            ],
            $"{home}/repo failed");

        var row = AdminOperationAuditStore.ToInsertRow(record);
        var input = Assert.IsType<JsonObject>(row["input_json"]);
        var result = Assert.IsType<JsonObject>(row["result_json"]);
        var logs = Assert.IsType<JsonArray>(row["logs_json"]);
        var firstLog = Assert.IsType<JsonObject>(logs[0]);

        Assert.Equal("[redacted]", input["supabaseSecretKey"]?.ToString());
        Assert.Equal("[redacted]", input["answers_json"]?.ToString());
        Assert.Equal("[redacted]", input["email"]?.ToString());
        Assert.Equal("[redacted]", input["user_id"]?.ToString());
        Assert.Equal("[redacted]", result["service_role_key"]?.ToString());

        var logMessage = firstLog["message"]?.ToString() ?? string.Empty;
        Assert.DoesNotContain(home, logMessage);
        Assert.DoesNotContain("abc123", logMessage);
        Assert.Contains("SUPABASE_SECRET_KEY=[redacted]", logMessage);
    }
}
