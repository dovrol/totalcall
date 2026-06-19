using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TotalCall.Client.Application.Auth;
using TotalCall.Client.Application.Services;
using TotalCall.Core.Domain.Competitions;
using TotalCall.Core.Domain.Predictions;
using TotalCall.Client.Infrastructure.Browser;
using TotalCall.Client.Infrastructure.Json;
using TotalCall.Client.Infrastructure.Supabase;
using TotalCall.Client.Infrastructure.Supabase.Auth;
using TotalCall.Client.Storage;

namespace TotalCall.Tests.Storage;

public sealed class SynchronizedPredictionStoreTests
{
    private const string CurrentUserId = "11111111-1111-1111-1111-111111111111";
    private const string OtherUserId = "22222222-2222-2222-2222-222222222222";

    [Fact]
    public async Task SaveAsync_WhenAnonymous_SavesAnonymousDraftOnlyLocally()
    {
        var js = new FakeJsRuntime();
        var handler = new RecordingHandler((_, _) =>
            throw new InvalidOperationException("Cloud should not be called for an anonymous user."));
        var auth = await CreateAuthAsync(js, authenticated: false);
        var state = new PredictionSyncState();

        using var store = CreateStore(js, handler, auth, state);
        await store.InitializeAsync();

        var predictionSet = CreatePredictionSet();
        await store.SaveAsync(predictionSet);

        var saved = await new LocalStoragePredictionStore(new BrowserLocalStorage(js))
            .GetAsync(predictionSet.CompetitionId);

        Assert.NotNull(saved);
        Assert.Null(saved.LocalUserId);
        Assert.Equal(0, handler.RequestCount);
        Assert.Equal(PredictionSaveStatus.Local, state.GetStatus(predictionSet.CompetitionId));
    }

    [Fact]
    public async Task SaveAsync_WhenAnonymous_DoesNotDeclassifyOwnedDraft()
    {
        var js = new FakeJsRuntime();
        var handler = new RecordingHandler((_, _) =>
            throw new InvalidOperationException("Cloud should not be called for an anonymous user."));
        var auth = await CreateAuthAsync(js, authenticated: false);
        var state = new PredictionSyncState();

        using var store = CreateStore(js, handler, auth, state);
        await store.InitializeAsync();

        var predictionSet = CreatePredictionSet(localUserId: CurrentUserId);
        await store.SaveAsync(predictionSet);

        var saved = await new LocalStoragePredictionStore(new BrowserLocalStorage(js))
            .GetAsync(predictionSet.CompetitionId);

        Assert.Equal(0, handler.RequestCount);
        Assert.Null(saved);
        Assert.Equal(PredictionSaveStatus.Local, state.GetStatus(predictionSet.CompetitionId));
    }

    [Fact]
    public async Task SaveAsync_WhenAuthenticated_SavesLocallyAndUpsertsCloudDraft()
    {
        var js = new FakeJsRuntime();
        var handler = new RecordingHandler((request, _) =>
            Task.FromResult(request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.Created)));
        var auth = await CreateAuthAsync(js, authenticated: true);
        var state = new PredictionSyncState();

        using var store = CreateStore(js, handler, auth, state);
        await store.InitializeAsync();

        var predictionSet = CreatePredictionSet(localUserId: CurrentUserId);
        await store.SaveAsync(predictionSet);

        var saved = await new LocalStoragePredictionStore(new BrowserLocalStorage(js))
            .GetAsync(predictionSet.CompetitionId);

        Assert.NotNull(saved);
        Assert.Equal(CurrentUserId, saved.LocalUserId);
        Assert.Equal(PredictionSaveStatus.Cloud, state.GetStatus(predictionSet.CompetitionId));

        Assert.Equal(2, handler.RequestCount);
        var request = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains("on_conflict=user_id,competition_id", request.Uri);
        Assert.DoesNotContain("\"status\":\"draft\"", request.Body);
        Assert.Contains("\"answers_json\":", request.Body);
        Assert.Contains($"\"localUserId\":\"{CurrentUserId}\"", request.Body);
    }

    [Fact]
    public async Task SaveAsync_WhenCloudFails_KeepsLocalDraftAndReportsFailure()
    {
        var js = new FakeJsRuntime();
        var handler = new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var auth = await CreateAuthAsync(js, authenticated: true);
        var state = new PredictionSyncState();

        using var store = CreateStore(js, handler, auth, state);
        await store.InitializeAsync();

        var predictionSet = CreatePredictionSet(localUserId: CurrentUserId);
        await store.SaveAsync(predictionSet);

        Assert.True(js.ContainsKey(LocalStorageKeys.Predictions(predictionSet.CompetitionId)));
        Assert.Equal(
            PredictionSaveStatus.SynchronizationFailed,
            state.GetStatus(predictionSet.CompetitionId));
    }

    [Fact]
    public async Task GetAsync_WhenLocalDraftIsMissing_RestoresCloudSnapshotLocally()
    {
        var cloudPredictionSet = CreatePredictionSet();
        var responseJson = JsonSerializer.Serialize(
            new[] { new { answers_json = cloudPredictionSet } },
            JsonDataOptions.SerializerOptions);

        var js = new FakeJsRuntime();
        var handler = new RecordingHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        });
        var auth = await CreateAuthAsync(js, authenticated: true);
        var state = new PredictionSyncState();

        using var store = CreateStore(js, handler, auth, state);
        await store.InitializeAsync();

        var restored = await store.GetAsync(cloudPredictionSet.CompetitionId);

        Assert.NotNull(restored);
        Assert.Equal(cloudPredictionSet.CompetitionId, restored.CompetitionId);
        Assert.Single(restored.Answers);
        Assert.True(js.ContainsKey(LocalStorageKeys.Predictions(cloudPredictionSet.CompetitionId)));
        Assert.Equal(PredictionSaveStatus.Cloud, state.GetStatus(cloudPredictionSet.CompetitionId));
    }

    [Fact]
    public async Task GetAsync_WhenAnonymous_HidesAndDeletesOwnedDraft()
    {
        var predictionSet = CreatePredictionSet(localUserId: OtherUserId);
        var js = new FakeJsRuntime();
        var localStore = new LocalStoragePredictionStore(new BrowserLocalStorage(js));
        await localStore.SaveAsync(predictionSet);
        var handler = new RecordingHandler((_, _) =>
            throw new InvalidOperationException("Cloud should not be called for an anonymous user."));
        var auth = await CreateAuthAsync(js, authenticated: false);
        var state = new PredictionSyncState();

        using var store = CreateStore(js, handler, auth, state);
        await store.InitializeAsync();

        var restored = await store.GetAsync(predictionSet.CompetitionId);

        Assert.Null(restored);
        Assert.Null(await localStore.GetAsync(predictionSet.CompetitionId));
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task InitializeAsync_WhenAuthenticated_SynchronizesExistingLocalDraft()
    {
        var predictionSet = CreatePredictionSet();
        var js = new FakeJsRuntime();
        var localStore = new LocalStoragePredictionStore(new BrowserLocalStorage(js));
        await localStore.SaveAsync(predictionSet);

        var cloudSaved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                });
            }

            cloudSaved.TrySetResult();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
        });
        var auth = await CreateAuthAsync(js, authenticated: true);
        var state = new PredictionSyncState();

        using var store = CreateStore(js, handler, auth, state);
        await store.InitializeAsync();
        await cloudSaved.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Contains(handler.Requests, request => request.Method == HttpMethod.Post);
        Assert.Equal(PredictionSaveStatus.Cloud, state.GetStatus(predictionSet.CompetitionId));
    }

    [Fact]
    public async Task InitializeAsync_WhenAnonymousLocalDraftConflictsWithCloud_CloudWinsWithoutUpsert()
    {
        var localPredictionSet = CreatePredictionSet(
            savedAt: DateTimeOffset.Parse("2026-06-04T11:00:00Z"),
            questionId: "local-answer");
        var cloudPredictionSet = CreatePredictionSet(
            savedAt: DateTimeOffset.Parse("2026-06-04T09:00:00Z"),
            questionId: "cloud-answer");
        var responseJson = JsonSerializer.Serialize(
            new[] { new { answers_json = cloudPredictionSet } },
            JsonDataOptions.SerializerOptions);

        var js = new FakeJsRuntime();
        var localStore = new LocalStoragePredictionStore(new BrowserLocalStorage(js));
        await localStore.SaveAsync(localPredictionSet);

        var handler = new RecordingHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        });
        var auth = await CreateAuthAsync(js, authenticated: true);
        var state = new PredictionSyncState();
        var synchronized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.Changed += competitionId =>
        {
            if (competitionId == localPredictionSet.CompetitionId &&
                state.GetStatus(competitionId) == PredictionSaveStatus.Cloud)
            {
                synchronized.TrySetResult();
            }
        };

        using var store = CreateStore(js, handler, auth, state);
        await store.InitializeAsync();
        await synchronized.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var saved = await localStore.GetAsync(localPredictionSet.CompetitionId);

        Assert.NotNull(saved);
        Assert.Equal(CurrentUserId, saved.LocalUserId);
        Assert.Equal("cloud-answer", Assert.Single(saved.Answers).QuestionId);
        Assert.DoesNotContain(handler.Requests, request => request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task GetAsync_WhenLocalDraftBelongsToDifferentUserAndCloudIsMissing_DiscardsLocalWithoutUpload()
    {
        var localPredictionSet = CreatePredictionSet(localUserId: OtherUserId);
        var js = new FakeJsRuntime();
        var localStore = new LocalStoragePredictionStore(new BrowserLocalStorage(js));
        await localStore.SaveAsync(localPredictionSet);

        var handler = new RecordingHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });
        });
        var auth = await CreateAuthAsync(js, authenticated: true);
        var state = new PredictionSyncState();

        using var store = CreateStore(js, handler, auth, state);
        await store.InitializeAsync();

        var restored = await store.GetAsync(localPredictionSet.CompetitionId);

        Assert.Null(restored);
        Assert.Null(await localStore.GetAsync(localPredictionSet.CompetitionId));
        Assert.DoesNotContain(handler.Requests, request => request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task GetAsync_WhenCurrentUsersLocalDraftIsNewer_MergesWithoutRemovingCloudAnswers()
    {
        var localPredictionSet = CreatePredictionSet(
            localUserId: CurrentUserId,
            savedAt: DateTimeOffset.Parse("2026-06-04T11:00:00Z"),
            questionId: "shared-answer",
            booleanValue: true);
        var cloudPredictionSet = CreatePredictionSet(
            savedAt: DateTimeOffset.Parse("2026-06-04T09:00:00Z"),
            questionId: "shared-answer",
            booleanValue: false);
        cloudPredictionSet = cloudPredictionSet with
        {
            Answers =
            [
                .. cloudPredictionSet.Answers,
                CreatePredictionSet(
                    savedAt: cloudPredictionSet.SavedAt,
                    questionId: "cloud-only").Answers.Single()
            ]
        };
        var responseJson = JsonSerializer.Serialize(
            new[] { new { answers_json = cloudPredictionSet } },
            JsonDataOptions.SerializerOptions);

        var js = new FakeJsRuntime();
        var localStore = new LocalStoragePredictionStore(new BrowserLocalStorage(js));
        await localStore.SaveAsync(localPredictionSet);

        var handler = new RecordingHandler((request, _) =>
        {
            return Task.FromResult(request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.Created));
        });
        var auth = await CreateAuthAsync(js, authenticated: true);
        var state = new PredictionSyncState();

        using var store = CreateStore(js, handler, auth, state);
        await store.InitializeAsync();

        var restored = await store.GetAsync(localPredictionSet.CompetitionId);

        Assert.NotNull(restored);
        Assert.Equal(2, restored.Answers.Count);
        Assert.True(restored.Answers.Single(answer => answer.QuestionId == "shared-answer").Value.BooleanValue);
        Assert.Contains(restored.Answers, answer => answer.QuestionId == "cloud-only");
        Assert.Contains(handler.Requests, request => request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task SaveAsync_WhenCurrentUsersCloudHasOtherAnswers_MergesBeforeUpsert()
    {
        var localPredictionSet = CreatePredictionSet(
            localUserId: CurrentUserId,
            savedAt: DateTimeOffset.Parse("2026-06-04T11:00:00Z"),
            questionId: "local-answer");
        var cloudPredictionSet = CreatePredictionSet(
            savedAt: DateTimeOffset.Parse("2026-06-04T09:00:00Z"),
            questionId: "cloud-answer");
        var responseJson = JsonSerializer.Serialize(
            new[] { new { answers_json = cloudPredictionSet } },
            JsonDataOptions.SerializerOptions);

        var js = new FakeJsRuntime();
        var handler = new RecordingHandler((request, _) =>
            Task.FromResult(request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.Created)));
        var auth = await CreateAuthAsync(js, authenticated: true);
        var state = new PredictionSyncState();

        using var store = CreateStore(js, handler, auth, state);
        await store.InitializeAsync();
        await store.SaveAsync(localPredictionSet);

        var saved = await new LocalStoragePredictionStore(new BrowserLocalStorage(js))
            .GetAsync(localPredictionSet.CompetitionId);
        var upsert = Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);

        Assert.NotNull(saved);
        Assert.Equal(2, saved.Answers.Count);
        Assert.Contains(saved.Answers, answer => answer.QuestionId == "local-answer");
        Assert.Contains(saved.Answers, answer => answer.QuestionId == "cloud-answer");
        Assert.Contains("\"questionId\":\"local-answer\"", upsert.Body);
        Assert.Contains("\"questionId\":\"cloud-answer\"", upsert.Body);
    }

    [Fact]
    public async Task SaveAsync_WhenAuthenticatedAnonymousDraftConflictsWithCloud_CloudWinsWithoutUpsert()
    {
        var localPredictionSet = CreatePredictionSet(
            savedAt: DateTimeOffset.Parse("2026-06-04T11:00:00Z"),
            questionId: "local-answer");
        var cloudPredictionSet = CreatePredictionSet(
            savedAt: DateTimeOffset.Parse("2026-06-04T09:00:00Z"),
            questionId: "cloud-answer");
        var responseJson = JsonSerializer.Serialize(
            new[] { new { answers_json = cloudPredictionSet } },
            JsonDataOptions.SerializerOptions);

        var js = new FakeJsRuntime();
        var handler = new RecordingHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        });
        var auth = await CreateAuthAsync(js, authenticated: true);
        var state = new PredictionSyncState();

        using var store = CreateStore(js, handler, auth, state);
        await store.InitializeAsync();
        await store.SaveAsync(localPredictionSet);

        var saved = await new LocalStoragePredictionStore(new BrowserLocalStorage(js))
            .GetAsync(localPredictionSet.CompetitionId);

        Assert.NotNull(saved);
        Assert.Equal(CurrentUserId, saved.LocalUserId);
        Assert.Equal("cloud-answer", Assert.Single(saved.Answers).QuestionId);
        Assert.DoesNotContain(handler.Requests, request => request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task SupabasePredictionStore_SubmitAsync_UsesServerTimestampAndReturnsSubmittedMetadata()
    {
        var submittedAt = DateTimeOffset.Parse("2026-06-05T12:15:00Z");
        var js = new FakeJsRuntime();
        var auth = await CreateAuthAsync(js, authenticated: true);
        var handler = new RecordingHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Contains("rest/v1/rpc/submit_prediction", request.Uri);
            Assert.Contains("\"p_competition_id\":\"worlds-2026\"", request.Body);
            Assert.Contains("\"p_answers_json\":", request.Body);
            Assert.DoesNotContain("p_submitted_at", request.Body);
            Assert.DoesNotContain("submitted_at", request.Body);
            Assert.DoesNotContain(submittedAt.ToString("O"), request.Body);
            Assert.DoesNotContain("\"status\":\"draft\"", request.Body);
            Assert.DoesNotContain("\"submissionStatus\":\"draft\"", request.Body);
            Assert.Contains("\"submissionStatus\":\"submitted\"", request.Body);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""[{"status":"submitted","submitted_at":"{{submittedAt:O}}"}]""",
                    Encoding.UTF8,
                    "application/json")
            });
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://supabase.test/") };
        var cloudStore = new SupabasePredictionStore(http, "publishable-key", auth);

        var result = await cloudStore.SubmitAsync(CreatePredictionSet(localUserId: CurrentUserId));

        Assert.Equal(PredictionSet.SubmittedSubmissionStatus, result.Status);
        Assert.Equal(submittedAt, result.SubmittedAt);
    }

    [Fact]
    public async Task SupabasePredictionStore_SubmitAsync_WhenAlreadySubmitted_DoesNotSendSubmittedAtAndKeepsReturnedValue()
    {
        var firstSubmittedAt = DateTimeOffset.Parse("2026-06-05T09:00:00Z");
        var js = new FakeJsRuntime();
        var auth = await CreateAuthAsync(js, authenticated: true);
        var handler = new RecordingHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.DoesNotContain("p_submitted_at", request.Body);
            Assert.DoesNotContain("submitted_at", request.Body);
            Assert.DoesNotContain(firstSubmittedAt.ToString("O"), request.Body);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""[{"status":"submitted","submitted_at":"{{firstSubmittedAt:O}}"}]""",
                    Encoding.UTF8,
                    "application/json")
            });
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://supabase.test/") };
        var cloudStore = new SupabasePredictionStore(http, "publishable-key", auth);
        var submitted = CreatePredictionSet(localUserId: CurrentUserId) with
        {
            SubmissionStatus = PredictionSet.SubmittedSubmissionStatus,
            SubmittedAt = firstSubmittedAt
        };

        var result = await cloudStore.SubmitAsync(submitted);

        Assert.Equal(firstSubmittedAt, result.SubmittedAt);
    }

    [Fact]
    public async Task SaveAsync_WhenAlreadySubmitted_DoesNotRevertStatusToDraft()
    {
        var submittedAt = DateTimeOffset.Parse("2026-06-05T10:00:00Z");
        var cloudPredictionSet = CreatePredictionSet(localUserId: CurrentUserId) with
        {
            SubmissionStatus = PredictionSet.SubmittedSubmissionStatus,
            SubmittedAt = submittedAt
        };
        var responseJson = JsonSerializer.Serialize(
            new[] { new { answers_json = cloudPredictionSet, status = "submitted", submitted_at = submittedAt } },
            JsonDataOptions.SerializerOptions);

        var js = new FakeJsRuntime();
        var handler = new RecordingHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                });
            }

            Assert.Contains("rest/v1/rpc/submit_prediction", request.Uri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""[{"status":"submitted","submitted_at":"{{submittedAt:O}}"}]""",
                    Encoding.UTF8,
                    "application/json")
            });
        });
        var auth = await CreateAuthAsync(js, authenticated: true);
        var state = new PredictionSyncState();
        using var store = CreateStore(js, handler, auth, state);
        await store.InitializeAsync();

        var editedSubmitted = CreatePredictionSet(
            localUserId: CurrentUserId,
            savedAt: DateTimeOffset.Parse("2026-06-05T11:00:00Z"),
            questionId: "edited-answer") with
        {
            SubmissionStatus = PredictionSet.SubmittedSubmissionStatus,
            SubmittedAt = submittedAt
        };

        await store.SaveAsync(editedSubmitted);

        var submitRequest = Assert.Single(
            handler.Requests,
            request => request.Method == HttpMethod.Post &&
                       request.Uri.Contains("rpc/submit_prediction", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("\"status\":\"draft\"", submitRequest.Body);
        Assert.Equal(PredictionSaveStatus.Submitted, state.GetStatus(editedSubmitted.CompetitionId));
    }

    [Fact]
    public async Task GetAsync_WhenAlreadySubmitted_DoesNotReSubmitDuringReconcile()
    {
        var submittedAt = DateTimeOffset.Parse("2026-06-05T10:00:00Z");
        var localPredictionSet = CreatePredictionSet(
            localUserId: CurrentUserId,
            savedAt: DateTimeOffset.Parse("2026-06-05T11:00:00Z"),
            questionId: "local-answer") with
        {
            SubmissionStatus = PredictionSet.SubmittedSubmissionStatus,
            SubmittedAt = submittedAt
        };
        var cloudPredictionSet = CreatePredictionSet(
            localUserId: CurrentUserId,
            savedAt: DateTimeOffset.Parse("2026-06-05T09:00:00Z"),
            questionId: "cloud-answer") with
        {
            SubmissionStatus = PredictionSet.SubmittedSubmissionStatus,
            SubmittedAt = submittedAt
        };
        var responseJson = JsonSerializer.Serialize(
            new[] { new { answers_json = cloudPredictionSet, status = "submitted", submitted_at = submittedAt } },
            JsonDataOptions.SerializerOptions);

        var js = new FakeJsRuntime();
        var localStore = new LocalStoragePredictionStore(new BrowserLocalStorage(js));
        await localStore.SaveAsync(localPredictionSet);

        var handler = new RecordingHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        });
        var auth = await CreateAuthAsync(js, authenticated: true);
        var state = new PredictionSyncState();
        using var store = CreateStore(js, handler, auth, state);

        var restored = await store.GetAsync(localPredictionSet.CompetitionId);

        Assert.NotNull(restored);
        Assert.True(restored.IsSubmitted);
        Assert.Equal(submittedAt, restored.SubmittedAt);
        Assert.DoesNotContain(handler.Requests, request =>
            request.Method == HttpMethod.Post &&
            request.Uri.Contains("rpc/submit_prediction", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(PredictionSaveStatus.Submitted, state.GetStatus(localPredictionSet.CompetitionId));
    }

    [Fact]
    public async Task SubmitAsync_WhenAnonymous_ThrowsWithoutCallingCloud()
    {
        var js = new FakeJsRuntime();
        var handler = new RecordingHandler((_, _) =>
            throw new InvalidOperationException("Cloud should not be called for a guest submit."));
        var auth = await CreateAuthAsync(js, authenticated: false);
        var state = new PredictionSyncState();
        using var store = CreateStore(js, handler, auth, state);
        await store.InitializeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SubmitAsync(CreatePredictionSet()));

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task SubmitAsync_WhenSyncFailed_BlocksSubmitUntilRetry()
    {
        var js = new FakeJsRuntime();
        var handler = new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var auth = await CreateAuthAsync(js, authenticated: true);
        var state = new PredictionSyncState();
        using var store = CreateStore(js, handler, auth, state);
        await store.InitializeAsync();
        var predictionSet = CreatePredictionSet(localUserId: CurrentUserId);

        await store.SaveAsync(predictionSet);
        var requestCountBeforeSubmit = handler.RequestCount;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SubmitAsync(predictionSet));

        Assert.Equal(PredictionSaveStatus.SynchronizationFailed, state.GetStatus(predictionSet.CompetitionId));
        Assert.Equal(requestCountBeforeSubmit, handler.RequestCount);
    }

    [Fact]
    public async Task GetParticipantsAsync_RequestsOnlyPublicFieldsAndFiltersDrafts()
    {
        var submittedAt = DateTimeOffset.Parse("2026-06-05T13:00:00Z");
        var js = new FakeJsRuntime();
        var auth = await CreateAuthAsync(js, authenticated: false);
        var handler = new RecordingHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Contains("rest/v1/rpc/get_competition_participants", request.Uri);
            Assert.Contains("\"p_competition_id\":\"worlds-2026\"", request.Body);
            Assert.DoesNotContain("answers_json", request.Body);
            Assert.DoesNotContain("user_id", request.Body);
            Assert.DoesNotContain("email", request.Body);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                    [
                      {
                        "competition_id":"worlds-2026",
                        "display_name":"Kuba",
                        "submitted_at":"{{submittedAt:O}}",
                        "status":"submitted"
                      },
                      {
                        "competition_id":"worlds-2026",
                        "display_name":"Draft user",
                        "submitted_at":null,
                        "status":"draft"
                      }
                    ]
                    """,
                    Encoding.UTF8,
                    "application/json")
            });
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://supabase.test/") };
        var cloudStore = new SupabasePredictionStore(http, "publishable-key", auth);

        var participants = await cloudStore.GetParticipantsAsync("worlds-2026");

        var participant = Assert.Single(participants);
        Assert.Equal("Kuba", participant.DisplayName);
        Assert.Equal(PredictionSet.SubmittedSubmissionStatus, participant.Status);
        Assert.Equal(submittedAt, participant.SubmittedAt);
    }

    [Fact]
    public async Task GetParticipantsAsync_WhenDisplayNameMissing_UsesSafeFallbackWithoutUserData()
    {
        var submittedAt = DateTimeOffset.Parse("2026-06-05T13:00:00Z");
        var js = new FakeJsRuntime();
        var auth = await CreateAuthAsync(js, authenticated: false);
        var handler = new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                    [
                      {
                        "competition_id":"worlds-2026",
                        "display_name":" ",
                        "submitted_at":"{{submittedAt:O}}",
                        "status":"submitted"
                      }
                    ]
                    """,
                    Encoding.UTF8,
                    "application/json")
            }));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://supabase.test/") };
        var cloudStore = new SupabasePredictionStore(http, "publishable-key", auth);

        var participant = Assert.Single(await cloudStore.GetParticipantsAsync("worlds-2026"));

        Assert.Equal("Lifter X", participant.DisplayName);
        Assert.DoesNotContain(CurrentUserId, participant.DisplayName);
        Assert.DoesNotContain("user@example.com", participant.DisplayName);
        Assert.False(participant.DisplayName.StartsWith("Uczestnik", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetLeaderboardAsync_RequestsPublicSnapshotsAndMapsScoreStatus()
    {
        var calculatedAt = DateTimeOffset.Parse("2026-06-07T18:30:00Z");
        var js = new FakeJsRuntime();
        var auth = await CreateAuthAsync(js, authenticated: false);
        var handler = new RecordingHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Contains("rest/v1/rpc/get_competition_leaderboard", request.Uri);
            Assert.Contains("\"p_competition_id\":\"worlds-2026\"", request.Body);
            Assert.DoesNotContain("answers_json", request.Body);
            Assert.DoesNotContain("user_id", request.Body);
            Assert.DoesNotContain("email", request.Body);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                    [
                      {
                        "position":1,
                        "board_ref":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                        "display_name":"Kuba",
                        "total_points":12.5,
                        "scored_groups_count":2,
                        "total_groups_count":4,
                        "status":"partial",
                        "last_calculated_at":"{{calculatedAt:O}}"
                      },
                      {
                        "position":2,
                        "board_ref":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                        "display_name":" ",
                        "total_points":8,
                        "scored_groups_count":4,
                        "total_groups_count":4,
                        "status":"final",
                        "last_calculated_at":"{{calculatedAt:O}}"
                      }
                    ]
                    """,
                    Encoding.UTF8,
                    "application/json")
            });
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://supabase.test/") };
        var cloudStore = new SupabasePredictionStore(http, "publishable-key", auth);

        var leaderboard = await cloudStore.GetLeaderboardAsync("worlds-2026");

        Assert.Collection(
            leaderboard,
            first =>
            {
                Assert.Equal(1, first.Position);
                Assert.Equal("Kuba", first.DisplayName);
                Assert.Equal(12.5m, first.TotalPoints);
                Assert.Equal(2, first.ScoredGroupsCount);
                Assert.Equal(4, first.TotalGroupsCount);
                Assert.Equal(PublicCompetitionLeaderboardEntry.PartialStatus, first.Status);
                Assert.Equal(calculatedAt, first.LastCalculatedAt);
                Assert.Equal("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", first.BoardRef);
            },
            second =>
            {
                Assert.Equal(2, second.Position);
                Assert.Equal("Lifter X", second.DisplayName);
                Assert.Equal(8m, second.TotalPoints);
                Assert.Equal(PublicCompetitionLeaderboardEntry.FinalStatus, second.Status);
                Assert.Equal("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", second.BoardRef);
            });
    }

    [Fact]
    public async Task GetPublicBoardAsync_RequestsPublicBoardAndRebuildsSubmittedPredictionSet()
    {
        var calculatedAt = DateTimeOffset.Parse("2026-06-07T18:30:00Z");
        var picksJson = JsonSerializer.Serialize(
            CreatePredictionSet().Answers,
            JsonDataOptions.SerializerOptions);
        var js = new FakeJsRuntime();
        var auth = await CreateAuthAsync(js, authenticated: false);
        var handler = new RecordingHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Contains("rest/v1/rpc/get_public_board", request.Uri);
            Assert.Contains("\"p_competition_id\":\"worlds-2026\"", request.Body);
            Assert.Contains("\"p_board_ref\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"", request.Body);
            Assert.DoesNotContain("answers_json", request.Body);
            Assert.DoesNotContain("user_id", request.Body);
            Assert.DoesNotContain("email", request.Body);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                    [
                      {
                        "display_name":"Kuba",
                        "rank":1,
                        "total_points":12,
                        "scored_groups_count":1,
                        "total_groups_count":3,
                        "status":"partial",
                        "picks_json":{{picksJson}},
                        "breakdown_json":{
                          "questionScores":[
                            {
                              "groupId":"women",
                              "questionId":"women-47",
                              "categoryId":"47",
                              "points":4,
                              "maxPoints":12,
                              "placement":3,
                              "placementMax":9,
                              "setBonus":1,
                              "orderBonus":0,
                              "explanation":"partial"
                            }
                          ]
                        },
                        "last_calculated_at":"{{calculatedAt:O}}"
                      }
                    ]
                    """,
                    Encoding.UTF8,
                    "application/json")
            });
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://supabase.test/") };
        var cloudStore = new SupabasePredictionStore(http, "publishable-key", auth);

        var board = await cloudStore.GetPublicBoardAsync(
            "worlds-2026",
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        Assert.NotNull(board);
        Assert.Equal("Kuba", board.DisplayName);
        Assert.Null(board.PredictionSet.LocalUserId);
        Assert.Equal(PredictionSet.SubmittedSubmissionStatus, board.PredictionSet.SubmissionStatus);
        Assert.Equal("worlds-2026", board.PredictionSet.CompetitionId);
        Assert.Equal(string.Empty, board.PredictionSet.CompetitionConfigVersion);
        Assert.Equal("women-47", Assert.Single(board.PredictionSet.Answers).QuestionId);
        Assert.Equal(1, board.Snapshot.Rank);
        Assert.Equal(12m, board.Snapshot.TotalPoints);
        Assert.Equal(PublicCompetitionLeaderboardEntry.PartialStatus, board.Snapshot.Status);
        Assert.Equal(calculatedAt, board.Snapshot.LastCalculatedAt);

        var category = Assert.Single(board.Snapshot.Categories);
        Assert.Equal("women", category.GroupId);
        Assert.Equal("women-47", category.QuestionId);
        Assert.Equal(4m, category.Points);
    }

    [Fact]
    public async Task GetPublicBoardAsync_WhenPicksAreNotArray_ReturnsNull()
    {
        var js = new FakeJsRuntime();
        var auth = await CreateAuthAsync(js, authenticated: false);
        var handler = new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    [
                      {
                        "display_name":"Kuba",
                        "rank":1,
                        "total_points":12,
                        "scored_groups_count":1,
                        "total_groups_count":3,
                        "status":"partial",
                        "picks_json":{"answers":[]},
                        "breakdown_json":{}
                      }
                    ]
                    """,
                    Encoding.UTF8,
                    "application/json")
            }));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://supabase.test/") };
        var cloudStore = new SupabasePredictionStore(http, "publishable-key", auth);

        var board = await cloudStore.GetPublicBoardAsync(
            "worlds-2026",
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        Assert.Null(board);
    }

    [Fact]
    public async Task GetMyScoreAsync_RequiresAuthenticatedUserAndMapsOwnBreakdown()
    {
        var calculatedAt = DateTimeOffset.Parse("2026-06-07T18:30:00Z");
        var js = new FakeJsRuntime();
        var auth = await CreateAuthAsync(js, authenticated: true);
        var handler = new RecordingHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Contains("rest/v1/rpc/get_my_score", request.Uri);
            Assert.Contains("\"p_competition_id\":\"worlds-2026\"", request.Body);
            Assert.DoesNotContain("answers_json", request.Body);
            Assert.DoesNotContain("user_id", request.Body);
            Assert.DoesNotContain("email", request.Body);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                    [
                      {
                        "rank":2,
                        "total_points":8,
                        "scored_groups_count":2,
                        "total_groups_count":3,
                        "status":"final",
                        "breakdown_json":{
                          "questionScores":[
                            {
                              "groupId":"men",
                              "questionId":"men-83",
                              "categoryId":"83",
                              "points":8,
                              "maxPoints":12,
                              "placement":6,
                              "placementMax":9,
                              "setBonus":1,
                              "orderBonus":1
                            }
                          ]
                        },
                        "last_calculated_at":"{{calculatedAt:O}}"
                      }
                    ]
                    """,
                    Encoding.UTF8,
                    "application/json")
            });
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://supabase.test/") };
        var cloudStore = new SupabasePredictionStore(http, "publishable-key", auth);

        var score = await cloudStore.GetMyScoreAsync("worlds-2026");

        Assert.NotNull(score);
        Assert.Equal(2, score.Rank);
        Assert.Equal(8m, score.TotalPoints);
        Assert.Equal(2, score.ScoredGroupsCount);
        Assert.Equal(3, score.TotalGroupsCount);
        Assert.Equal(PublicCompetitionLeaderboardEntry.FinalStatus, score.Status);
        Assert.Equal(calculatedAt, score.LastCalculatedAt);

        var category = Assert.Single(score.Categories);
        Assert.Equal("men", category.GroupId);
        Assert.Equal("men-83", category.QuestionId);
        Assert.Equal(8m, category.Points);
    }

    [Fact]
    public async Task GetMyScoreAsync_WhenAnonymous_ThrowsWithoutCallingCloud()
    {
        var js = new FakeJsRuntime();
        var auth = await CreateAuthAsync(js, authenticated: false);
        var handler = new RecordingHandler((_, _) =>
            throw new InvalidOperationException("Cloud should not be called for an anonymous score request."));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://supabase.test/") };
        var cloudStore = new SupabasePredictionStore(http, "publishable-key", auth);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cloudStore.GetMyScoreAsync("worlds-2026"));

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task PredictionService_WhenAuthenticatedAndNoDraft_CreatesDraftOwnedByCurrentUser()
    {
        var js = new FakeJsRuntime();
        var auth = await CreateAuthAsync(js, authenticated: true);
        var service = new PredictionService(new EmptyPredictionStore(), new AppInfoService(), auth);
        var competition = new Competition
        {
            Id = "worlds-2026",
            Slug = "worlds-2026",
            Name = "Worlds 2026",
            ConfigVersion = "1"
        };

        var predictionSet = await service.GetOrCreatePredictionSetAsync(competition);

        Assert.Equal(CurrentUserId, predictionSet.LocalUserId);
    }

    private static SynchronizedPredictionStore CreateStore(
        FakeJsRuntime js,
        RecordingHandler handler,
        AuthService auth,
        PredictionSyncState state)
    {
        var localStore = new LocalStoragePredictionStore(new BrowserLocalStorage(js));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://supabase.test/") };
        var cloudStore = new SupabasePredictionStore(http, "publishable-key", auth);

        return new SynchronizedPredictionStore(localStore, cloudStore, auth, state);
    }

    private static async Task<AuthService> CreateAuthAsync(FakeJsRuntime js, bool authenticated)
    {
        if (authenticated)
        {
            var session = new AuthSession
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
                User = new AuthUser
                {
                    Id = CurrentUserId,
                    Email = "cloud-save@totalcall.test"
                }
            };

            js.Set(
                LocalStorageKeys.AuthSession,
                JsonSerializer.Serialize(session, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }

        var browserStorage = new BrowserLocalStorage(js);
        var sessionStore = new SupabaseSessionStore(browserStorage);
        var unusedAuthHttp = new HttpClient(new RecordingHandler((_, _) =>
            throw new InvalidOperationException("A valid test session should not call Supabase Auth.")))
        {
            BaseAddress = new Uri("https://supabase.test/")
        };

        var auth = new AuthService(
            new SupabaseAuthClient(unusedAuthHttp, "publishable-key"),
            sessionStore,
            new TestNavigationManager());

        await auth.InitializeAsync();
        return auth;
    }

    private static PredictionSet CreatePredictionSet(
        string? localUserId = null,
        DateTimeOffset? savedAt = null,
        string questionId = "women-47",
        bool booleanValue = true)
    {
        var timestamp = savedAt ?? DateTimeOffset.Parse("2026-06-04T10:00:00Z");

        return new PredictionSet
        {
            CompetitionId = "worlds-2026",
            CompetitionConfigVersion = "1",
            LocalUserId = localUserId,
            AppVersion = "0.5.0-test",
            SchemaVersion = PredictionSet.StorageSchemaVersion,
            SavedAt = timestamp,
            Answers =
            [
                new PredictionAnswer
                {
                    GroupId = "women",
                    QuestionId = questionId,
                    QuestionType = PredictionQuestionType.YesNo,
                    Value = new PredictionAnswerValue { BooleanValue = booleanValue },
                    UpdatedAt = timestamp
                }
            ]
        };
    }

    private sealed class RecordingHandler(
        Func<CapturedRequest, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        public int RequestCount => Requests.Count;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var captured = new CapturedRequest(
                request.Method,
                request.RequestUri?.ToString() ?? string.Empty,
                request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken));

            Requests.Add(captured);
            return await handler(captured, cancellationToken);
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, string Uri, string Body);

    private sealed class EmptyPredictionStore : IPredictionStore
    {
        public Task<PredictionSet?> GetAsync(
            string competitionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PredictionSet?>(null);
        }

        public Task SaveAsync(
            PredictionSet predictionSet,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<PredictionSet> SubmitAsync(
            PredictionSet predictionSet,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(predictionSet);
        }

        public Task DeleteAsync(
            string competitionId,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeJsRuntime : IJSRuntime
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public bool ContainsKey(string key) => _values.ContainsKey(key);

        public void Set(string key, string value) => _values[key] = value;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            object? result = identifier switch
            {
                "localStorage.getItem" => Get(args),
                "localStorage.setItem" => Set(args),
                "localStorage.removeItem" => Remove(args),
                "totalCallActions.getLocalStorageKeys" => GetKeys(args),
                _ => throw new InvalidOperationException($"Unexpected JS interop call: {identifier}")
            };

            return ValueTask.FromResult(result is null ? default! : (TValue)result);
        }

        private string? Get(object?[]? args)
        {
            var key = (string)args![0]!;
            return _values.TryGetValue(key, out var value) ? value : null;
        }

        private object? Set(object?[]? args)
        {
            _values[(string)args![0]!] = (string)args[1]!;
            return null;
        }

        private object? Remove(object?[]? args)
        {
            _values.Remove((string)args![0]!);
            return null;
        }

        private string[] GetKeys(object?[]? args)
        {
            var prefix = (string)args![0]!;
            return _values.Keys
                .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
                .ToArray();
        }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize("https://totalcall.test/", "https://totalcall.test/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
        }
    }
}
