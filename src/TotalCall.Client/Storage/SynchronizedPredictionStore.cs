using TotalCall.Client.Application.Auth;
using TotalCall.Client.Domain.Predictions;
using TotalCall.Client.Infrastructure.Supabase;

namespace TotalCall.Client.Storage;

public sealed class SynchronizedPredictionStore(
    LocalStoragePredictionStore localStore,
    SupabasePredictionStore cloudStore,
    AuthService authService,
    PredictionSyncState syncState) : IPredictionStore, IDisposable
{
    private readonly SemaphoreSlim _syncAllGate = new(1, 1);
    private bool _initialized;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return Task.CompletedTask;
        }

        _initialized = true;
        authService.AuthStateChanged += OnAuthStateChanged;

        if (authService.IsAuthenticated)
        {
            _ = SyncAllLocalDraftsAsync(cancellationToken);
        }

        return Task.CompletedTask;
    }

    public async Task<PredictionSet?> GetAsync(
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        var local = await localStore.GetAsync(competitionId, cancellationToken);
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            if (local is not null && !IsAnonymous(local))
            {
                await localStore.DeleteAsync(competitionId, cancellationToken);
                local = null;
            }

            syncState.SetStatus(competitionId, PredictionSaveStatus.Local);
            return local;
        }

        try
        {
            var cloud = await cloudStore.GetAsync(competitionId, cancellationToken);
            var synchronized = await ReconcileAsync(local, cloud, currentUserId, cancellationToken);
            SetSynchronizedStatus(competitionId, synchronized);
            return synchronized;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            SetCloudFailureStatus(competitionId);
            return local is not null &&
                   (IsAnonymous(local) || IsOwnedBy(local, currentUserId))
                ? local
                : null;
        }
    }

    public async Task SaveAsync(
        PredictionSet predictionSet,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            if (IsAnonymous(predictionSet))
            {
                await localStore.SaveAsync(predictionSet, cancellationToken);
            }
            else
            {
                await localStore.DeleteAsync(predictionSet.CompetitionId, cancellationToken);
            }

            syncState.SetStatus(predictionSet.CompetitionId, PredictionSaveStatus.Local);
            return;
        }

        if (!IsOwnedBy(predictionSet, currentUserId))
        {
            await SaveUntrustedDraftAsync(predictionSet, currentUserId, cancellationToken);
            return;
        }

        var ownedPredictionSet = AssignOwner(predictionSet, currentUserId);
        await localStore.SaveAsync(ownedPredictionSet, cancellationToken);
        syncState.SetStatus(predictionSet.CompetitionId, PredictionSaveStatus.Local);

        try
        {
            var cloud = await cloudStore.GetAsync(predictionSet.CompetitionId, cancellationToken);
            var synchronized = await ReconcileAsync(
                ownedPredictionSet,
                cloud,
                currentUserId,
                cancellationToken);
            SetSynchronizedStatus(predictionSet.CompetitionId, synchronized);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            SetCloudFailureStatus(predictionSet.CompetitionId);
        }
    }

    public Task DeleteAsync(string competitionId, CancellationToken cancellationToken = default)
    {
        syncState.SetStatus(competitionId, PredictionSaveStatus.Local);
        return localStore.DeleteAsync(competitionId, cancellationToken);
    }

    public void Dispose()
    {
        if (_initialized)
        {
            authService.AuthStateChanged -= OnAuthStateChanged;
        }

        _syncAllGate.Dispose();
    }

    private void OnAuthStateChanged()
    {
        if (!authService.IsAuthenticated)
        {
            syncState.MarkAllLocal();
            return;
        }

        _ = SyncAllLocalDraftsAsync();
    }

    private async Task SyncAllLocalDraftsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _syncAllGate.WaitAsync(cancellationToken);
            try
            {
                if (!authService.IsAuthenticated)
                {
                    return;
                }

                var currentUserId = GetCurrentUserId();
                if (currentUserId is null)
                {
                    return;
                }

                var localDrafts = await localStore.GetAllAsync(cancellationToken);
                foreach (var local in localDrafts)
                {
                    try
                    {
                        var cloud = await cloudStore.GetAsync(local.CompetitionId, cancellationToken);
                        var synchronized = await ReconcileAsync(
                            local,
                            cloud,
                            currentUserId,
                            cancellationToken);
                        SetSynchronizedStatus(local.CompetitionId, synchronized);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        SetCloudFailureStatus(local.CompetitionId);
                    }
                }
            }
            finally
            {
                _syncAllGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // App shutdown or navigation cancelled a best-effort background synchronization.
        }
        catch
        {
            // A local-storage enumeration failure must not block app startup.
        }
    }

    private async Task<PredictionSet?> ReconcileAsync(
        PredictionSet? local,
        PredictionSet? cloud,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        // Last-write-wins is allowed only for snapshots owned by the current account.
        var ownedCloud = cloud is null
            ? null
            : AssignOwner(cloud, currentUserId);

        if (local is null)
        {
            if (ownedCloud is not null)
            {
                await localStore.SaveAsync(ownedCloud, cancellationToken);
            }

            return ownedCloud;
        }

        if (IsAnonymous(local))
        {
            if (ownedCloud is not null)
            {
                await localStore.SaveAsync(ownedCloud, cancellationToken);
                return ownedCloud;
            }

            var adoptedLocal = AssignOwner(local, currentUserId);
            await localStore.SaveAsync(adoptedLocal, cancellationToken);
            await cloudStore.SaveDraftAsync(adoptedLocal, cancellationToken);
            return adoptedLocal;
        }

        if (!IsOwnedBy(local, currentUserId))
        {
            if (ownedCloud is not null)
            {
                await localStore.SaveAsync(ownedCloud, cancellationToken);
                return ownedCloud;
            }

            await localStore.DeleteAsync(local.CompetitionId, cancellationToken);
            return null;
        }

        var ownedLocal = AssignOwner(local, currentUserId);
        if (ownedCloud is null)
        {
            await localStore.SaveAsync(ownedLocal, cancellationToken);
            await cloudStore.SaveDraftAsync(ownedLocal, cancellationToken);
            return ownedLocal;
        }

        var merged = MergeOwnedSnapshots(ownedLocal, ownedCloud, currentUserId);
        await localStore.SaveAsync(merged, cancellationToken);
        await cloudStore.SaveDraftAsync(merged, cancellationToken);
        return merged;
    }

    private async Task SaveUntrustedDraftAsync(
        PredictionSet predictionSet,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            var cloud = await cloudStore.GetAsync(predictionSet.CompetitionId, cancellationToken);
            var synchronized = await ReconcileAsync(
                predictionSet,
                cloud,
                currentUserId,
                cancellationToken);
            SetSynchronizedStatus(predictionSet.CompetitionId, synchronized);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            if (IsAnonymous(predictionSet))
            {
                await localStore.SaveAsync(
                    predictionSet with { LocalUserId = null },
                    cancellationToken);
            }

            SetCloudFailureStatus(predictionSet.CompetitionId);
        }
    }

    private string? GetCurrentUserId()
    {
        var userId = authService.CurrentUser?.Id;
        return string.IsNullOrWhiteSpace(userId)
            ? null
            : userId.Trim();
    }

    private static PredictionSet AssignOwner(PredictionSet predictionSet, string userId)
    {
        return predictionSet with { LocalUserId = userId };
    }

    private static bool IsAnonymous(PredictionSet predictionSet)
    {
        return string.IsNullOrWhiteSpace(predictionSet.LocalUserId);
    }

    private static bool IsOwnedBy(PredictionSet predictionSet, string userId)
    {
        return string.Equals(
            predictionSet.LocalUserId?.Trim(),
            userId,
            StringComparison.OrdinalIgnoreCase);
    }

    private static PredictionSet MergeOwnedSnapshots(
        PredictionSet local,
        PredictionSet cloud,
        string userId)
    {
        if (!string.Equals(
                local.CompetitionConfigVersion,
                cloud.CompetitionConfigVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            return AssignOwner(
                local.SavedAt >= cloud.SavedAt ? local : cloud,
                userId);
        }

        var answers = new Dictionary<string, PredictionAnswer>(StringComparer.OrdinalIgnoreCase);

        foreach (var answer in cloud.Answers)
        {
            answers[answer.QuestionId] = answer;
        }

        foreach (var answer in local.Answers)
        {
            if (!answers.TryGetValue(answer.QuestionId, out var existing) ||
                answer.UpdatedAt >= existing.UpdatedAt)
            {
                answers[answer.QuestionId] = answer;
            }
        }

        var newestSnapshot = local.SavedAt >= cloud.SavedAt ? local : cloud;

        return newestSnapshot with
        {
            LocalUserId = userId,
            SchemaVersion = Math.Max(local.SchemaVersion, cloud.SchemaVersion),
            SavedAt = local.SavedAt >= cloud.SavedAt ? local.SavedAt : cloud.SavedAt,
            Answers = answers.Values
                .OrderBy(answer => answer.GroupId)
                .ThenBy(answer => answer.QuestionId)
                .ToArray()
        };
    }

    private void SetSynchronizedStatus(string competitionId, PredictionSet? predictionSet)
    {
        syncState.SetStatus(
            competitionId,
            predictionSet is null
                ? PredictionSaveStatus.Local
                : PredictionSaveStatus.Cloud);
    }

    private void SetCloudFailureStatus(string competitionId)
    {
        syncState.SetStatus(
            competitionId,
            authService.IsAuthenticated
                ? PredictionSaveStatus.SynchronizationFailed
                : PredictionSaveStatus.Local);
    }
}
