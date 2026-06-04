using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace TotalCall.Client.Application.Auth;

/// <summary>
/// Projects <see cref="AuthService"/>'s session onto Blazor's authentication pipeline so the
/// app can use <c>&lt;AuthorizeView&gt;</c>, <c>&lt;AuthorizeRouteView&gt;</c>, <c>[Authorize]</c>
/// and the cascading <see cref="AuthenticationState"/> instead of bespoke flags.
/// </summary>
public sealed class TotalCallAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private const string AuthenticationScheme = "Supabase";

    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly AuthService _authService;

    public TotalCallAuthenticationStateProvider(AuthService authService)
    {
        _authService = authService;
        _authService.AuthStateChanged += OnAuthStateChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var session = _authService.CurrentSession;

        if (session is null)
        {
            return Task.FromResult(Anonymous);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.User.Id)
        };

        if (!string.IsNullOrWhiteSpace(session.User.Email))
        {
            claims.Add(new Claim(ClaimTypes.Name, session.User.Email));
            claims.Add(new Claim(ClaimTypes.Email, session.User.Email));
        }

        // A non-empty authentication type is what marks the identity as authenticated.
        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    private void OnAuthStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void Dispose()
    {
        _authService.AuthStateChanged -= OnAuthStateChanged;
    }
}
