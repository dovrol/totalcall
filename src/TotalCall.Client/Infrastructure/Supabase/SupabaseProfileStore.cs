using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TotalCall.Client.Application.Auth;

namespace TotalCall.Client.Infrastructure.Supabase;

public sealed class SupabaseProfileStore(
    HttpClient? httpClient,
    string publishableKey,
    AuthService authService)
{
    public const string DisplayNameTakenMessage = "This display name is already taken.";
    public const string MissingDisplayNameFallback = "Lifter X";

    private static readonly JsonSerializerOptions SupabaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<UserProfile> GetCurrentProfileAsync(CancellationToken cancellationToken = default)
    {
        var context = await GetAuthenticatedContextAsync(cancellationToken);
        var url = "rest/v1/profiles"
                  + $"?id=eq.{Uri.EscapeDataString(context.UserId)}"
                  + "&select=id,display_name"
                  + "&limit=1";

        using var request = BuildRequest(HttpMethod.Get, url, context.AccessToken);
        using var response = await httpClient!.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var rows = await response.Content.ReadFromJsonAsync<List<ProfileRow>>(
            SupabaseJsonOptions,
            cancellationToken);

        var row = rows?.FirstOrDefault()
                  ?? throw new InvalidOperationException("Profile was not found for the current user.");

        return MapProfile(row);
    }

    public async Task<UserProfile> UpdateDisplayNameAsync(
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeDisplayName(displayName);
        var context = await GetAuthenticatedContextAsync(cancellationToken);
        var url = "rest/v1/profiles"
                  + $"?id=eq.{Uri.EscapeDataString(context.UserId)}"
                  + "&select=id,display_name";

        using var request = BuildRequest(HttpMethod.Patch, url, context.AccessToken);
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        request.Content = JsonContent.Create(
            new ProfileUpdate { DisplayName = normalized },
            options: SupabaseJsonOptions);

        using var response = await httpClient!.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var rows = await response.Content.ReadFromJsonAsync<List<ProfileRow>>(
            SupabaseJsonOptions,
            cancellationToken);

        var row = rows?.FirstOrDefault()
                  ?? throw new InvalidOperationException("Supabase profile update did not return a profile.");

        return MapProfile(row);
    }

    public static string NormalizeDisplayName(string displayName)
    {
        var normalized = displayName.Trim();

        if (normalized.Length == 0)
        {
            throw new InvalidOperationException("Display name is required.");
        }

        if (normalized.Length > 32)
        {
            throw new InvalidOperationException("Display name must be at most 32 characters.");
        }

        if (normalized.Any(character => char.IsControl(character) || character is '<' or '>'))
        {
            throw new InvalidOperationException("Display name contains unsupported characters.");
        }

        if (normalized.Any(character => !IsAllowedDisplayNameCharacter(character)))
        {
            throw new InvalidOperationException(
                "Display name may contain only letters, numbers, spaces, dots, hyphens and underscores.");
        }

        return normalized;
    }

    private async Task<AuthenticatedContext> GetAuthenticatedContextAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var session = await authService.GetSessionAsync(cancellationToken);
        if (session is null || string.IsNullOrWhiteSpace(session.User.Id))
        {
            throw new InvalidOperationException("Profile access requires an authenticated user.");
        }

        return new AuthenticatedContext(session.User.Id, session.AccessToken);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("apikey", publishableKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private void EnsureConfigured()
    {
        if (httpClient is null || string.IsNullOrWhiteSpace(publishableKey))
        {
            throw new InvalidOperationException(
                "Supabase is not configured. Set Supabase:Url and Supabase:PublishableKey in wwwroot/appsettings.json.");
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict ||
            body.Contains("profiles_display_name_ci_unique_idx", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(DisplayNameTakenMessage);
        }

        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(body)
                ? $"Supabase profile request failed ({(int)response.StatusCode})."
                : $"Supabase profile request failed ({(int)response.StatusCode}): {body}",
            null,
            response.StatusCode);
    }

    private static UserProfile MapProfile(ProfileRow row)
    {
        return new UserProfile(
            row.Id,
            string.IsNullOrWhiteSpace(row.DisplayName)
                ? MissingDisplayNameFallback
                : row.DisplayName.Trim());
    }

    private static bool IsAllowedDisplayNameCharacter(char character)
    {
        // ASCII-only on purpose so this matches the database constraint
        // (^[A-Za-z0-9 ._-]+$) regardless of the database lc_ctype locale.
        return character is
            (>= 'a' and <= 'z') or
            (>= 'A' and <= 'Z') or
            (>= '0' and <= '9') or
            ' ' or '.' or '-' or '_';
    }

    private sealed record AuthenticatedContext(string UserId, string AccessToken);

    private sealed record ProfileRow
    {
        public required string Id { get; init; }

        public string? DisplayName { get; init; }
    }

    private sealed record ProfileUpdate
    {
        public required string DisplayName { get; init; }
    }
}

public sealed record UserProfile(
    string Id,
    string DisplayName);
