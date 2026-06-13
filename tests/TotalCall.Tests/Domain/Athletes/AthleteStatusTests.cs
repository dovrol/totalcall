using System.Text.Json;
using TotalCall.Client.Domain.Athletes;
using TotalCall.Client.Infrastructure.Json;

namespace TotalCall.Tests.Domain.Athletes;

public sealed class AthleteStatusTests
{
    [Fact]
    public void Deserialize_defaults_missing_status_to_active()
    {
        var athlete = JsonSerializer.Deserialize<Athlete>(
            """
            {
              "id": "athlete-1",
              "displayName": "Athlete One"
            }
            """,
            JsonDataOptions.SerializerOptions);

        Assert.NotNull(athlete);
        Assert.Equal(AthleteStatus.Active, athlete.Status);
        Assert.False(athlete.IsWithdrawn);
    }

    [Fact]
    public void Deserialize_reads_withdrawn_status_and_optional_metadata()
    {
        var athlete = JsonSerializer.Deserialize<Athlete>(
            """
            {
              "id": "athlete-1",
              "displayName": "Athlete One",
              "status": "withdrawn",
              "withdrawn_at": "2026-06-12T12:00:00Z",
              "withdrawal_reason": "Injury",
              "withdrawal_source": "federation",
              "updated_at": "2026-06-12T12:30:00Z"
            }
            """,
            JsonDataOptions.SerializerOptions);

        Assert.NotNull(athlete);
        Assert.Equal(AthleteStatus.Withdrawn, athlete.Status);
        Assert.True(athlete.IsWithdrawn);
        Assert.Equal("Injury", athlete.WithdrawalReason);
        Assert.Equal("federation", athlete.WithdrawalSource);
        Assert.Equal(DateTimeOffset.Parse("2026-06-12T12:00:00Z"), athlete.WithdrawnAt);
        Assert.Equal(DateTimeOffset.Parse("2026-06-12T12:30:00Z"), athlete.UpdatedAt);
    }
}
