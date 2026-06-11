using System.Text.Json;
using TotalCall.Client.Domain.Competitions;

namespace TotalCall.Tests.Domain.Competitions;

public sealed class CompetitionDisciplineTests
{
    [Fact]
    public void EquipmentValues_Raw_ReturnsRawOnly()
    {
        Assert.Equal(["Raw"], CompetitionDisciplines.EquipmentValues(CompetitionDiscipline.Raw));
    }

    [Fact]
    public void EquipmentValues_Equipped_ReturnsEquippedKinds()
    {
        Assert.Equal(["Single-ply", "Multi-ply"], CompetitionDisciplines.EquipmentValues(CompetitionDiscipline.Equipped));
    }

    [Fact]
    public void EquipmentValues_Null_ReturnsEmptyMeaningNoFilter()
    {
        Assert.Empty(CompetitionDisciplines.EquipmentValues(null));
    }

    [Theory]
    [InlineData("\"raw\"", CompetitionDiscipline.Raw)]
    [InlineData("\"equipped\"", CompetitionDiscipline.Equipped)]
    public void Discipline_DeserializesFromConfigToken(string json, CompetitionDiscipline expected)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        Assert.Equal(expected, JsonSerializer.Deserialize<CompetitionDiscipline>(json, options));
    }
}
