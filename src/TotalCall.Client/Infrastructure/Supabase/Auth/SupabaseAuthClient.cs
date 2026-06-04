using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TotalCall.Client.Application.Auth;

namespace TotalCall.Client.Infrastructure.Supabase.Auth;

/// <summary>
/// Thin client over the Supabase Auth (GoTrue) REST API at <c>/auth/v1</c>.
/// Uses the publishable (anon) key only — never the secret key — and speaks the
/// PKCE code flow so magic links resolve to a <c>?code=</c> callback.
/// </summary>
public sealed class SupabaseAuthClient(HttpClient httpClient, string publishableKey)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Sends a passwordless magic link to <paramref name="email"/>. The link carries the
    /// PKCE challenge and redirects the browser to <paramref name="redirectTo"/> with a code.
    /// </summary>
    public async Task SendMagicLinkAsync(
        string email,
        string redirectTo,
        string codeChallenge,
        CancellationToken cancellationToken = default)
    {
        var url = "auth/v1/otp?redirect_to=" + Uri.EscapeDataString(redirectTo);
        var body = new OtpRequest
        {
            Email = email,
            CreateUser = true,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = Pkce.ChallengeMethod
        };

        using var request = BuildRequest(HttpMethod.Post, url, anonAuthorization: true, body);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>Exchanges the PKCE auth code + verifier for a full session.</summary>
    public Task<AuthSession> ExchangeCodeForSessionAsync(
        string authCode,
        string codeVerifier,
        CancellationToken cancellationToken = default)
    {
        var body = new PkceTokenRequest { AuthCode = authCode, CodeVerifier = codeVerifier };
        return RequestSessionAsync("auth/v1/token?grant_type=pkce", body, cancellationToken);
    }

    /// <summary>Trades a refresh token for a fresh session when the access token expires.</summary>
    public Task<AuthSession> RefreshSessionAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var body = new RefreshTokenRequest { RefreshToken = refreshToken };
        return RequestSessionAsync("auth/v1/token?grant_type=refresh_token", body, cancellationToken);
    }

    /// <summary>Revokes the session server-side. Best-effort: callers ignore failures on sign-out.</summary>
    public async Task SignOutAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(HttpMethod.Post, "auth/v1/logout", anonAuthorization: false, body: null);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<AuthSession> RequestSessionAsync(
        string url,
        object body,
        CancellationToken cancellationToken)
    {
        using var request = BuildRequest(HttpMethod.Post, url, anonAuthorization: true, body);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken);

        if (token is null || string.IsNullOrEmpty(token.AccessToken) || token.User is null)
        {
            throw new SupabaseAuthException("Supabase returned an incomplete session.");
        }

        return MapSession(token);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url, bool anonAuthorization, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("apikey", publishableKey);

        if (anonAuthorization)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", publishableKey);
        }

        if (body is not null)
        {
            // Pass the runtime type explicitly so the concrete request record is serialized.
            request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
        }

        return request;
    }

    private static AuthSession MapSession(TokenResponse token)
    {
        var expiresAt = token.ExpiresAt is > 0
            ? DateTimeOffset.FromUnixTimeSeconds(token.ExpiresAt.Value)
            : DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn > 0 ? token.ExpiresIn : 3600);

        return new AuthSession
        {
            AccessToken = token.AccessToken!,
            RefreshToken = token.RefreshToken ?? string.Empty,
            ExpiresAt = expiresAt,
            User = new AuthUser
            {
                Id = token.User!.Id ?? string.Empty,
                Email = token.User.Email
            }
        };
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? payload = null;
        try
        {
            payload = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            // Fall back to the status code below.
        }

        throw new SupabaseAuthException(ExtractErrorMessage(payload, response.StatusCode));
    }

    private static string ExtractErrorMessage(string? payload, System.Net.HttpStatusCode statusCode)
    {
        if (!string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;

                foreach (var field in new[] { "error_description", "msg", "message", "error" })
                {
                    if (root.TryGetProperty(field, out var value) &&
                        value.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(value.GetString()))
                    {
                        return value.GetString()!;
                    }
                }
            }
            catch (JsonException)
            {
                // Non-JSON payload — fall through to the generic status message.
            }
        }

        return $"Supabase Auth request failed ({(int)statusCode}).";
    }

    private sealed record OtpRequest
    {
        public required string Email { get; init; }

        public bool CreateUser { get; init; }

        public required string CodeChallenge { get; init; }

        public required string CodeChallengeMethod { get; init; }
    }

    private sealed record PkceTokenRequest
    {
        public required string AuthCode { get; init; }

        public required string CodeVerifier { get; init; }
    }

    private sealed record RefreshTokenRequest
    {
        public required string RefreshToken { get; init; }
    }

    private sealed record TokenResponse
    {
        public string? AccessToken { get; init; }

        public string? RefreshToken { get; init; }

        public int ExpiresIn { get; init; }

        public long? ExpiresAt { get; init; }

        public UserResponse? User { get; init; }
    }

    private sealed record UserResponse
    {
        public string? Id { get; init; }

        public string? Email { get; init; }
    }
}
