using System.Text;
using TotalCall.Client.Domain.Predictions.Export;
using TotalCall.Client.Domain.Predictions.Review;

namespace TotalCall.Client.Application.Services.Export;

public interface IPredictionModuleExporter
{
    string ModuleType { get; }

    IEnumerable<TableExportRow> ToTableRows(
        PredictionExportContext context,
        PredictionModuleReviewModel module);

    void AppendSummary(
        StringBuilder builder,
        PredictionExportContext context,
        PredictionModuleReviewModel module);
}
