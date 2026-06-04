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
        if (!authService.IsAuthenticated)
        {
            syncState.SetStatus(competitionId, PredictionSaveStatus.Local);
            return local;
        }

        try
        {
            var cloud = await cloudStore.GetAsync(competitionId, cancellationToken);
            var synchronized = await ReconcileAsync(local, cloud, cancellationToken);
            syncState.SetStatus(
                competitionId,
                synchronized is null
                    ? PredictionSaveStatus.Local
                    : PredictionSaveStatus.Cloud);
            return synchronized;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            SetCloudFailureStatus(competitionId);
            return local;
        }
    }

    public async Task SaveAsync(
        PredictionSet predictionSet,
        CancellationToken cancellationToken = default)
    {
        await localStore.SaveAsync(predictionSet, cancellationToken);
        syncState.SetStatus(predictionSet.CompetitionId, PredictionSaveStatus.Local);

        if (!authService.IsAuthenticated)
        {
            return;
        }

        try
        {
            await cloudStore.SaveDraftAsync(predictionSet, cancellationToken);
            syncState.SetStatus(predictionSet.CompetitionId, PredictionSaveStatus.Cloud);
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

                var localDrafts = await localStore.GetAllAsync(cancellationToken);
                foreach (var local in localDrafts)
                {
                    try
                    {
                        var cloud = await cloudStore.GetAsync(local.CompetitionId, cancellationToken);
                        await ReconcileAsync(local, cloud, cancellationToken);
                        syncState.SetStatus(local.CompetitionId, PredictionSaveStatus.Cloud);
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
        CancellationToken cancellationToken)
    {
        if (local is null)
        {
            if (cloud is not null)
            {
                await localStore.SaveAsync(cloud, cancellationToken);
            }

            return cloud;
        }

        if (cloud is null || local.SavedAt >= cloud.SavedAt)
        {
            await cloudStore.SaveDraftAsync(local, cancellationToken);
            return local;
        }

        await localStore.SaveAsync(cloud, cancellationToken);
        return cloud;
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
