namespace TotalCall.Client.Storage;

public sealed class PredictionSyncState
{
    private readonly Dictionary<string, PredictionSaveStatus> _statuses =
        new(StringComparer.OrdinalIgnoreCase);

    public event Action<string>? Changed;

    public PredictionSaveStatus GetStatus(string competitionId)
    {
        return _statuses.TryGetValue(competitionId, out var status)
            ? status
            : PredictionSaveStatus.Local;
    }

    internal void SetStatus(string competitionId, PredictionSaveStatus status)
    {
        if (_statuses.TryGetValue(competitionId, out var current) && current == status)
        {
            return;
        }

        _statuses[competitionId] = status;
        Changed?.Invoke(competitionId);
    }

    internal void MarkAllLocal()
    {
        foreach (var competitionId in _statuses.Keys.ToArray())
        {
            SetStatus(competitionId, PredictionSaveStatus.Local);
        }
    }
}
