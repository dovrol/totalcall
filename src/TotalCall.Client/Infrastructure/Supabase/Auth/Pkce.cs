using System.Security.Cryptography;
using System.Text;

namespace TotalCall.Client.Infrastructure.Supabase.Auth;

/// <summary>
/// Generates PKCE (Proof Key for Code Exchange) verifier/challenge pairs for the
/// Supabase Auth code flow. Mirrors the behaviour of @supabase/gotrue-js so the
/// magic-link callback returns <c>?code=</c> instead of an implicit hash fragment.
/// </summary>
public static class Pkce
{
    /// <summary>S256 is always used because the runtime hashes the verifier.</summary>
    public const string ChallengeMethod = "s256";

    /// <summary>
    /// Creates a high-entropy code verifier (43 unreserved characters) that is later
    /// exchanged together with the auth code for a session.
    /// </summary>
    public static string CreateVerifier()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    /// <summary>
    /// Derives the code challenge sent with the OTP request:
    /// <c>base64url(sha256(verifier))</c> without padding.
    /// </summary>
    public static string CreateChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
