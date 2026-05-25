using TotalCall.Client.Domain.Predictions;

namespace TotalCall.Client.Application.Services.Export;

public sealed class PredictionModuleExporterRegistry
{
    private readonly Dictionary<string, IPredictionModuleExporter> exportersByType;
    private readonly IPredictionModuleExporter defaultExporter;

    public PredictionModuleExporterRegistry(IEnumerable<IPredictionModuleExporter> exporters)
    {
        var list = exporters.ToArray();

        defaultExporter = list.FirstOrDefault(exporter => exporter.ModuleType == DefaultPredictionModuleExporter.Wildcard)
            ?? throw new InvalidOperationException(
                $"No default ({DefaultPredictionModuleExporter.Wildcard}) prediction module exporter registered.");

        exportersByType = list
            .Where(exporter => exporter.ModuleType != DefaultPredictionModuleExporter.Wildcard)
            .ToDictionary(
                exporter => PredictionModuleType.Normalize(exporter.ModuleType),
                StringComparer.OrdinalIgnoreCase);
    }

    public IPredictionModuleExporter Resolve(string moduleType)
    {
        var normalized = PredictionModuleType.Normalize(moduleType);
        if (!string.IsNullOrEmpty(normalized) &&
            exportersByType.TryGetValue(normalized, out var exporter))
        {
            return exporter;
        }

        return defaultExporter;
    }
}
