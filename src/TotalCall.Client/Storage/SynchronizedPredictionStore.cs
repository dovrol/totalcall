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
            var synchronized = await ReconcileAsync(
                local,
                cloud,
                currentUserId,
                CloudWriteMode.DraftsOnly,
                cancellationToken);
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
        syncState.SetStatus(
            predictionSet.CompetitionId,
            ownedPredictionSet.IsSubmitted
                ? PredictionSaveStatus.Submitted
                : PredictionSaveStatus.Local);

        try
        {
            var cloud = await cloudStore.GetAsync(predictionSet.CompetitionId, cancellationToken);
            var synchronized = await ReconcileAsync(
                ownedPredictionSet,
                cloud,
                currentUserId,
                CloudWriteMode.All,
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

    public async Task<PredictionSet> SubmitAsync(
        PredictionSet predictionSet,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            throw new InvalidOperationException("Submitting predictions requires an authenticated user.");
        }

        if (!IsAnonymous(predictionSet) && !IsOwnedBy(predictionSet, currentUserId))
        {
            throw new InvalidOperationException("Cannot submit a prediction draft owned by another user.");
        }

        if (syncState.GetStatus(predictionSet.CompetitionId) == PredictionSaveStatus.SynchronizationFailed)
        {
            throw new InvalidOperationException("Synchronize the cloud draft before submitting predictions.");
        }

        var ownedPredictionSet = AssignOwner(predictionSet, currentUserId);
        var submissionCandidate = ownedPredictionSet with
        {
            SubmissionStatus = PredictionSet.SubmittedSubmissionStatus
        };
        var metadata = await cloudStore.SubmitAsync(submissionCandidate, cancellationToken);
        var submitted = submissionCandidate with
        {
            SubmissionStatus = NormalizeSubmissionStatus(metadata.Status, metadata.SubmittedAt),
            SubmittedAt = metadata.SubmittedAt
        };

        await localStore.SaveAsync(submitted, cancellationToken);
        syncState.SetStatus(submitted.CompetitionId, PredictionSaveStatus.Submitted);

        return submitted;
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
                            CloudWriteMode.DraftsOnly,
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
        CloudWriteMode cloudWriteMode,
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
            await SaveCloudSnapshotAsync(adoptedLocal, cloudWriteMode, cancellationToken);
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
            await SaveCloudSnapshotAsync(ownedLocal, cloudWriteMode, cancellationToken);
            return ownedLocal;
        }

        var merged = MergeOwnedSnapshots(ownedLocal, ownedCloud, currentUserId);
        await localStore.SaveAsync(merged, cancellationToken);
        await SaveCloudSnapshotAsync(merged, cloudWriteMode, cancellationToken);
        return merged;
    }

    private async Task SaveCloudSnapshotAsync(
        PredictionSet predictionSet,
        CloudWriteMode cloudWriteMode,
        CancellationToken cancellationToken)
    {
        if (predictionSet.IsSubmitted)
        {
            if (cloudWriteMode == CloudWriteMode.All)
            {
                await cloudStore.SubmitAsync(predictionSet, cancellationToken);
            }

            return;
        }

        await cloudStore.SaveDraftAsync(predictionSet, cancellationToken);
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
                CloudWriteMode.All,
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
            var newest = AssignOwner(
                local.SavedAt >= cloud.SavedAt ? local : cloud,
                userId);

            return ApplyMergedSubmissionMetadata(newest, local, cloud);
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

        var merged = newestSnapshot with
        {
            LocalUserId = userId,
            SchemaVersion = Math.Max(local.SchemaVersion, cloud.SchemaVersion),
            SavedAt = local.SavedAt >= cloud.SavedAt ? local.SavedAt : cloud.SavedAt,
            Answers = answers.Values
                .OrderBy(answer => answer.GroupId)
                .ThenBy(answer => answer.QuestionId)
                .ToArray()
        };

        return ApplyMergedSubmissionMetadata(merged, local, cloud);
    }

    private static PredictionSet ApplyMergedSubmissionMetadata(
        PredictionSet predictionSet,
        PredictionSet local,
        PredictionSet cloud)
    {
        if (!local.IsSubmitted && !cloud.IsSubmitted)
        {
            return predictionSet with
            {
                SubmissionStatus = PredictionSet.DraftSubmissionStatus,
                SubmittedAt = null
            };
        }

        var submittedAt = new[] { local.SubmittedAt, cloud.SubmittedAt }
            .Where(value => value is not null)
            .DefaultIfEmpty(null)
            .Min();

        return predictionSet with
        {
            SubmissionStatus = PredictionSet.SubmittedSubmissionStatus,
            SubmittedAt = submittedAt
        };
    }

    private static string NormalizeSubmissionStatus(string? status, DateTimeOffset? submittedAt)
    {
        return submittedAt is not null ||
               string.Equals(
                   status,
                   PredictionSet.SubmittedSubmissionStatus,
                   StringComparison.OrdinalIgnoreCase)
            ? PredictionSet.SubmittedSubmissionStatus
            : PredictionSet.DraftSubmissionStatus;
    }

    private void SetSynchronizedStatus(string competitionId, PredictionSet? predictionSet)
    {
        syncState.SetStatus(
            competitionId,
            predictionSet is null
                ? PredictionSaveStatus.Local
                : predictionSet.IsSubmitted
                    ? PredictionSaveStatus.Submitted
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

    private enum CloudWriteMode
    {
        DraftsOnly,
        All
    }
}
