using System.Globalization;
using System.Text;
using Microsoft.Extensions.Localization;
using TotalCall.Client.Application.Localization;
using TotalCall.Client.Domain.Athletes;
using TotalCall.Client.Domain.Predictions;
using TotalCall.Client.Domain.Predictions.Export;
using TotalCall.Client.Domain.Predictions.Review;

namespace TotalCall.Client.Application.Services.Export;

public sealed class DefaultPredictionModuleExporter(IStringLocalizer<SharedResource> localizer)
    : IPredictionModuleExporter
{
    public const string Wildcard = "*";

    public string ModuleType => Wildcard;

    public IEnumerable<TableExportRow> ToTableRows(
        PredictionExportContext context,
        PredictionModuleReviewModel module)
    {
        foreach (var section in module.Sections)
        {
            var category = ResolveCategoryLabel(section);
            var statusLabel = StatusLabel(section.Status);

            if (section.Layout == ReviewSectionLayout.AthleteRanking)
            {
                if (section.Picks.Count == 0)
                {
                    yield return new TableExportRow
                    {
                        Module = module.Title,
                        Category = category,
                        SectionStatus = statusLabel
                    };
                    continue;
                }

                foreach (var pick in section.Picks.OrderBy(row => row.Position ?? int.MaxValue))
                {
                    if (!pick.HasAthlete)
                    {
                        continue;
                    }

                    var athlete = ResolveAthlete(context, pick.AthleteId);
                    yield return new TableExportRow
                    {
                        Module = module.Title,
                        Category = category,
                        Rank = pick.Position,
                        Athlete = pick.AthleteName,
                        Country = pick.CountryName ?? pick.CountryCode ?? string.Empty,
                        NominatedTotalKg = athlete?.SeedTotalKg,
                        PredictedTotalKg = pick.PredictedTotalKg,
                        SquatKg = pick.PredictedSquatKg,
                        BenchKg = pick.PredictedBenchKg,
                        DeadliftKg = pick.PredictedDeadliftKg,
                        SectionStatus = statusLabel
                    };
                }
            }
            else
            {
                yield return new TableExportRow
                {
                    Module = module.Title,
                    Category = category,
                    Athlete = section.SummaryText ?? string.Empty,
                    SectionStatus = statusLabel
                };
            }
        }
    }

    public void AppendSummary(
        StringBuilder builder,
        PredictionExportContext context,
        PredictionModuleReviewModel module)
    {
        builder.AppendLine();
        builder.AppendLine($"## {module.Title}");
        builder.AppendLine($"  {localizer["Review.Summary.SectionsCompleted"]}: {module.CompletedSections}/{module.TotalSections}");

        foreach (var section in module.Sections)
        {
            builder.AppendLine();
            var sectionHead = string.IsNullOrWhiteSpace(section.GroupLabel)
                ? section.Title
                : $"{section.GroupLabel} · {section.Title}";
            builder.AppendLine($"- {sectionHead} [{StatusLabel(section.Status)}]");

            if (section.Layout == ReviewSectionLayout.AthleteRanking)
            {
                if (section.Picks.Count == 0 || section.Picks.All(pick => !pick.HasAthlete))
                {
                    builder.AppendLine($"    {localizer["Review.Summary.NoPicks"]}");
                    continue;
                }

                foreach (var pick in section.Picks.OrderBy(row => row.Position ?? int.MaxValue))
                {
                    if (!pick.HasAthlete)
                    {
                        continue;
                    }

                    var position = pick.Position is null ? "-" : $"#{pick.Position}";
                    var country = string.IsNullOrWhiteSpace(pick.CountryCode) ? "" : $" ({pick.CountryCode})";
                    var total = pick.PredictedTotalKg is null ? "—" : $"{Format(pick.PredictedTotalKg.Value)} kg";
                    var lifts = FormatLifts(pick);
                    builder.AppendLine($"    {position} {pick.AthleteName}{country}  total={total}{lifts}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(section.SummaryText))
            {
                builder.AppendLine($"    {section.SummaryText}");
            }
        }
    }

    private static string ResolveCategoryLabel(ReviewSectionModel section)
    {
        if (string.IsNullOrWhiteSpace(section.GroupLabel))
        {
            return section.Title;
        }

        return $"{section.GroupLabel} · {section.Title}";
    }

    private static Athlete? ResolveAthlete(PredictionExportContext context, string athleteId)
    {
        if (string.IsNullOrWhiteSpace(athleteId))
        {
            return null;
        }

        return context.Competition.Athletes.FirstOrDefault(athlete =>
            string.Equals(athlete.Id, athleteId, StringComparison.OrdinalIgnoreCase));
    }

    private string StatusLabel(PredictionCompletionStatus status) => status switch
    {
        PredictionCompletionStatus.Complete => localizer["Predictions.Status.Complete"],
        PredictionCompletionStatus.InProgress => localizer["Predictions.Status.InProgress"],
        _ => localizer["Predictions.Status.NotStarted"]
    };

    private static string Format(decimal value) =>
        value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string FormatLifts(ReviewPickRowModel pick)
    {
        if (!pick.HasAnyLift)
        {
            return string.Empty;
        }

        var parts = new List<string>(3);
        if (pick.PredictedSquatKg is not null) parts.Add($"SQ {Format(pick.PredictedSquatKg.Value)}");
        if (pick.PredictedBenchKg is not null) parts.Add($"BP {Format(pick.PredictedBenchKg.Value)}");
        if (pick.PredictedDeadliftKg is not null) parts.Add($"DL {Format(pick.PredictedDeadliftKg.Value)}");
        return $"  {string.Join(" / ", parts)}";
    }
}
