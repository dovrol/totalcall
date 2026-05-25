using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions.Export;
using TotalCall.Client.Domain.Predictions.Review;

namespace TotalCall.Client.Application.Services.Export;

public sealed record PredictionExportContext(
    Competition Competition,
    CompetitionReviewModel Review,
    ExportEnvelope Envelope);
