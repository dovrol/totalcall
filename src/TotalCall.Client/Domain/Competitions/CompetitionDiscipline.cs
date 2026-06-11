using System.Text.Json.Serialization;

namespace TotalCall.Client.Domain.Competitions;

[JsonConverter(typeof(JsonStringEnumConverter<CompetitionDiscipline>))]
public enum CompetitionDiscipline
{
    [JsonStringEnumMemberName("raw")]
    Raw = 0,

    [JsonStringEnumMemberName("equipped")]
    Equipped = 1
}

public static class CompetitionDisciplines
{
    // OpenPowerlifting/OpenIPF equipment values allowed for each discipline. This is
    // the single source of truth that maps a competition's discipline to the equipment
    // filter applied to athlete history and analytics. IPF classic is strictly "Raw"
    // (knee wraps excluded); equipped covers single-ply and multi-ply.
    private static readonly string[] RawEquipment = ["Raw"];
    private static readonly string[] EquippedEquipment = ["Single-ply", "Multi-ply"];

    // Empty result means "no equipment filter" (include every result) — used when a
    // competition does not declare a discipline, preserving the previous behaviour.
    public static IReadOnlyList<string> EquipmentValues(CompetitionDiscipline? discipline) => discipline switch
    {
        CompetitionDiscipline.Raw => RawEquipment,
        CompetitionDiscipline.Equipped => EquippedEquipment,
        _ => []
    };
}
