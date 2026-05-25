namespace TotalCall.Client.Application.Windows;

public sealed class AthleteHistoryWindowDescriptor : FloatingWindowDescriptor
{
    public string CompetitionId { get; }

    public string AthleteId { get; }

    public string AthleteName { get; }

    public string? CountryCode { get; }

    public string? CountryName { get; }

    public string? CategoryName { get; }

    public decimal? NominatedTotalKg { get; }

    public AthleteHistorySlotContext? SlotContext { get; set; }

    public AthleteHistoryWindowDescriptor(
        string competitionId,
        string athleteId,
        string athleteName,
        string? countryCode,
        string? countryName,
        string? categoryName,
        decimal? nominatedTotalKg,
        AthleteHistorySlotContext? slotContext,
        WindowPosition position,
        string title)
        : base(WindowKind, title, athleteName, position)
    {
        CompetitionId = competitionId;
        AthleteId = athleteId;
        AthleteName = athleteName;
        CountryCode = countryCode;
        CountryName = countryName;
        CategoryName = categoryName;
        NominatedTotalKg = nominatedTotalKg;
        SlotContext = slotContext;
    }

    public const string WindowKind = "athlete-history";
}
