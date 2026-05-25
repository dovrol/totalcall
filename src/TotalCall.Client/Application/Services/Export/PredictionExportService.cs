using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Localization;
using TotalCall.Client.Application.Localization;
using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;
using TotalCall.Client.Domain.Predictions.Export;
using TotalCall.Client.Domain.Predictions.Review;
using TotalCall.Client.Infrastructure.Json;

namespace TotalCall.Client.Application.Services.Export;

public sealed class PredictionExportService(
    PredictionService predictionService,
    PredictionModuleExporterRegistry registry,
    AppInfoService appInfo,
    IStringLocalizer<SharedResource> localizer)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonDataOptions.SerializerOptions)
    {
        WriteIndented = true
    };

    public ExportEnvelope BuildEnvelope(Competition competition, PredictionSet predictionSet)
    {
        return new ExportEnvelope
        {
            CompetitionId = competition.Id,
            CompetitionName = competition.Name,
            AppVersion = appInfo.AppVersion,
            SchemaVersion = PredictionSet.StorageSchemaVersion,
            ExportedAt = DateTimeOffset.UtcNow,
            SavedAt = predictionSet.SavedAt
        };
    }

    public string BuildPlainSummary(PredictionExportContext context)
    {
        var review = context.Review;
        var builder = new StringBuilder();
        builder.AppendLine(review.CompetitionName);
        builder.AppendLine($"{localizer["Predictions.LastSaved"]}: {review.SavedAt.ToLocalTime():d MMM yyyy, HH:mm}");
        builder.AppendLine($"{localizer["Review.Summary.Modules"]}: {review.Stats.CompletedModules}/{review.Stats.TotalModules}");
        builder.AppendLine($"{localizer["Review.Summary.Sections"]}: {review.Stats.CompletedSections}/{review.Stats.TotalSections}");

        foreach (var module in review.Modules)
        {
            var exporter = registry.Resolve(module.ModuleType);
            exporter.AppendSummary(builder, context, module);
        }

        return builder.ToString().TrimEnd();
    }

    public string BuildJson(PredictionExportContext context, PredictionSet predictionSet)
    {
        var payload = predictionService.CreateExportPayload(predictionSet);
        var document = new
        {
            exportedAt = context.Envelope.ExportedAt,
            appVersion = context.Envelope.AppVersion,
            schemaVersion = context.Envelope.SchemaVersion,
            competition = new
            {
                id = context.Envelope.CompetitionId,
                name = context.Envelope.CompetitionName
            },
            picks = payload
        };
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public IReadOnlyList<TableExportRow> BuildTableRows(PredictionExportContext context)
    {
        var rows = new List<TableExportRow>();
        foreach (var module in context.Review.Modules)
        {
            var exporter = registry.Resolve(module.ModuleType);
            rows.AddRange(exporter.ToTableRows(context, module));
        }
        return rows;
    }

    public string BuildCsv(PredictionExportContext context)
    {
        var rows = BuildTableRows(context);
        var builder = new StringBuilder();
        // UTF-8 BOM helps Excel detect encoding for non-ASCII athlete names.
        builder.Append('﻿');

        builder.Append("Competition,CompetitionId,AppVersion,SchemaVersion,ExportedAt,");
        builder.AppendLine("Module,Category,Rank,Athlete,Country,NominatedTotalKg,PredictedTotalKg,SquatKg,BenchKg,DeadliftKg,SectionStatus");

        var meta = string.Join(",",
            CsvEscape(context.Envelope.CompetitionName),
            CsvEscape(context.Envelope.CompetitionId),
            CsvEscape(context.Envelope.AppVersion),
            context.Envelope.SchemaVersion.ToString(CultureInfo.InvariantCulture),
            context.Envelope.ExportedAt.ToString("o", CultureInfo.InvariantCulture));

        foreach (var row in rows)
        {
            builder.Append(meta);
            builder.Append(',');
            builder.AppendLine(string.Join(",",
                CsvEscape(row.Module),
                CsvEscape(row.Category),
                FormatRank(row.Rank),
                CsvEscape(row.Athlete),
                CsvEscape(row.Country),
                FormatNumber(row.NominatedTotalKg),
                FormatNumber(row.PredictedTotalKg),
                FormatNumber(row.SquatKg),
                FormatNumber(row.BenchKg),
                FormatNumber(row.DeadliftKg),
                CsvEscape(row.SectionStatus)));
        }

        return builder.ToString();
    }

    public string BuildJsonFileName(Competition competition, ExportEnvelope envelope) =>
        BuildFileName(competition.Id, envelope.ExportedAt, "json");

    public string BuildCsvFileName(Competition competition, ExportEnvelope envelope) =>
        BuildFileName(competition.Id, envelope.ExportedAt, "csv");

    private static string BuildFileName(string competitionId, DateTimeOffset exportedAt, string extension)
    {
        var safeId = Sanitize(competitionId);
        var timestamp = exportedAt.ToLocalTime().ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        return $"totalcall-{safeId}-picks-{timestamp}.{extension}";
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "export";
        }
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '-').ToArray();
        return new string(chars).Trim('-');
    }

    private static string FormatNumber(decimal? value) =>
        value is null ? string.Empty : value.Value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatRank(int? rank) =>
        rank is null ? string.Empty : rank.Value.ToString(CultureInfo.InvariantCulture);

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuoting = value.IndexOfAny(['"', ',', '\n', '\r']) >= 0;
        if (!needsQuoting)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
