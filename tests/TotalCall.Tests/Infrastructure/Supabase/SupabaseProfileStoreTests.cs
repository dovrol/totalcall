using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TotalCall.Client.Application.Auth;
using TotalCall.Client.Infrastructure.Browser;
using TotalCall.Client.Infrastructure.Supabase;
using TotalCall.Client.Infrastructure.Supabase.Auth;

namespace TotalCall.Tests.Infrastructure.Supabase;

public sealed class SupabaseProfileStoreTests
{
    private const string CurrentUserId = "11111111-1111-1111-1111-111111111111";
    private const string CurrentUserEmail = "profile@totalcall.test";

    [Fact]
    public async Task GetCurrentProfileAsync_WhenAuthenticated_GetsOwnerProfile()
    {
        var auth = await CreateAuthAsync(authenticated: true);
        var handler = new RecordingHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Contains("rest/v1/profiles", request.Uri);
            Assert.Contains($"id=eq.{CurrentUserId}", request.Uri);
            Assert.Contains("select=id,display_name", request.Uri);
            Assert.DoesNotContain("email", request.Uri);
            Assert.DoesNotContain("user_id", request.Uri);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""[{"id":"{{CurrentUserId}}","display_name":"Kuba"}]""",
                    Encoding.UTF8,
                    "application/json")
            });
        });
        var store = CreateStore(handler, auth);

        var profile = await store.GetCurrentProfileAsync();

        Assert.Equal(CurrentUserId, profile.Id);
        Assert.Equal("Kuba", profile.DisplayName);
    }

    [Fact]
    public async Task UpdateDisplayNameAsync_WhenAuthenticated_PatchesTrimmedDisplayName()
    {
        var auth = await CreateAuthAsync(authenticated: true);
        var handler = new RecordingHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Patch, request.Method);
            Assert.Contains("rest/v1/profiles", request.Uri);
            Assert.Contains($"id=eq.{CurrentUserId}", request.Uri);
            Assert.Contains("select=id,display_name", request.Uri);
            Assert.Contains("\"display_name\":\"Nowy Nick\"", request.Body);
            Assert.DoesNotContain("email", request.Body);
            Assert.DoesNotContain("user_id", request.Body);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""[{"id":"{{CurrentUserId}}","display_name":"Nowy Nick"}]""",
                    Encoding.UTF8,
                    "application/json")
            });
        });
        var store = CreateStore(handler, auth);

        var profile = await store.UpdateDisplayNameAsync("  Nowy Nick  ");

        Assert.Equal("Nowy Nick", profile.DisplayName);
    }

    [Fact]
    public async Task UpdateDisplayNameAsync_WhenDisplayNameIsTaken_ReturnsReadableError()
    {
        var auth = await CreateAuthAsync(authenticated: true);
        var handler = new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(
                    """{"message":"duplicate key value violates unique constraint \"profiles_display_name_ci_unique_idx\"}""",
                    Encoding.UTF8,
                    "application/json")
            }));
        var store = CreateStore(handler, auth);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.UpdateDisplayNameAsync("TakenNick"));

        Assert.Equal("This display name is already taken.", error.Message);
    }

    [Fact]
    public async Task GetCurrentProfileAsync_WhenAnonymous_ThrowsWithoutCallingSupabase()
    {
        var auth = await CreateAuthAsync(authenticated: false);
        var handler = new RecordingHandler((_, _) =>
            throw new InvalidOperationException("Supabase should not be called for anonymous profile access."));
        var store = CreateStore(handler, auth);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetCurrentProfileAsync());

        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("<b>Kuba</b>")]
    [InlineData("Kuba\nNowak")]
    [InlineData("Kuba!")]
    public void NormalizeDisplayName_RejectsTooLongHtmlControlCharactersAndUnsupportedPunctuation(string displayName)
    {
        Assert.Throws<InvalidOperationException>(() =>
            SupabaseProfileStore.NormalizeDisplayName(displayName));
    }

    [Fact]
    public async Task GetCurrentProfileAsync_WhenDisplayNameMissing_UsesSimpleClientSafetyFallback()
    {
        var auth = await CreateAuthAsync(authenticated: true);
        var handler = new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""[{"id":"{{CurrentUserId}}","display_name":" "}]""",
                    Encoding.UTF8,
                    "application/json")
            }));
        var store = CreateStore(handler, auth);

        var profile = await store.GetCurrentProfileAsync();

        Assert.Equal("Lifter X", profile.DisplayName);
        Assert.DoesNotContain(CurrentUserEmail, profile.DisplayName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CurrentUserId, profile.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProfilePage_RequiresAuthenticationAndPreservesReturnUrl()
    {
        var page = ReadClientFile("Pages/ProfilePage.razor");

        Assert.Contains("@page \"/profile\"", page);
        Assert.Contains("@attribute [Microsoft.AspNetCore.Authorization.Authorize]", page);
        Assert.Contains("auth/login?returnUrl=%2Fprofile", page);
    }

    private static SupabaseProfileStore CreateStore(
        RecordingHandler handler,
        AuthService auth)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://supabase.test/") };
        return new SupabaseProfileStore(http, "publishable-key", auth);
    }

    private static async Task<AuthService> CreateAuthAsync(bool authenticated)
    {
        var js = new FakeJsRuntime();
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
                    Email = CurrentUserEmail
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

    private static string ReadClientFile(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/TotalCall.Client",
            relativePath));

        return File.ReadAllText(path);
    }

    private sealed class RecordingHandler(
        Func<CapturedRequest, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        public int RequestCount => Requests.Count;

        public List<CapturedRequest> Requests { get; } = [];

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

    private sealed class FakeJsRuntime : IJSRuntime
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

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
