using System.Net.Http.Headers;
using TotalCall.Client.Application.Auth;

namespace TotalCall.Client.Infrastructure.Supabase;

internal sealed class SupabasePredictionApiClient(
    HttpClient? httpClient,
    string publishableKey,
    AuthService authService)
{
    public async Task<SupabaseAuthenticatedContext> GetAuthenticatedContextAsync(
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var session = await authService.GetSessionAsync(cancellationToken);
        if (session is null || string.IsNullOrWhiteSpace(session.User.Id))
        {
            throw new InvalidOperationException("Cloud prediction storage requires an authenticated user.");
        }

        return new SupabaseAuthenticatedContext(session.User.Id, session.AccessToken);
    }

    public HttpRequestMessage BuildAuthenticatedRequest(
        HttpMethod method,
        string url,
        string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("apikey", publishableKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    public HttpRequestMessage BuildPublicRequest(HttpMethod method, string url)
    {
        EnsureConfigured();

        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("apikey", publishableKey);
        return request;
    }

    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        return httpClient!.SendAsync(request, cancellationToken);
    }

    public static Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return Task.CompletedTask;
        }

        throw new HttpRequestException(
            $"Supabase prediction request failed ({(int)response.StatusCode}).",
            null,
            response.StatusCode);
    }

    private void EnsureConfigured()
    {
        if (httpClient is null || string.IsNullOrWhiteSpace(publishableKey))
        {
            throw new InvalidOperationException(
                "Supabase is not configured. Set Supabase:Url and Supabase:PublishableKey in wwwroot/appsettings.json.");
        }
    }
}

internal sealed record SupabaseAuthenticatedContext(string UserId, string AccessToken);
