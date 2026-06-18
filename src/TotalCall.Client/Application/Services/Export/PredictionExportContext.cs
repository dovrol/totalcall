using TotalCall.Core.Domain.Competitions;
using TotalCall.Core.Domain.Predictions.Export;
using TotalCall.Core.Domain.Predictions.Review;

namespace TotalCall.Client.Application.Services.Export;

public sealed record PredictionExportContext(
    Competition Competition,
    CompetitionReviewModel Review,
    ExportEnvelope Envelope);
