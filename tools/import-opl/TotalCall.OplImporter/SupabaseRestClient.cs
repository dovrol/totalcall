using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TotalCall.OplImporter;

// Thin PostgREST wrapper. Uses the Supabase service_role key for all calls.
// All importer writes target the public schema; admin tables are protected by
// RLS + REVOKE, not by schema isolation. The schema parameter is kept for
// future flexibility (e.g. graphql_public) — current callers always pass "public".
public sealed class SupabaseRestClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _serviceKey;

    public SupabaseRestClient(string baseUrl, string serviceKey)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _serviceKey = serviceKey;
        _http = new HttpClient();
        _http.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<JsonArray> GetAsync(
        string schema,
        string table,
        string query,
        CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{_baseUrl}/rest/v1/{table}?{query}");
        ApplyAuth(req);
        req.Headers.Add("Accept-Profile", schema);

        using var res = await _http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"GET {schema}.{table} failed: {(int)res.StatusCode} {res.ReasonPhrase} — {body}");
        }
        return JsonNode.Parse(body) as JsonArray ?? new JsonArray();
    }

    public async Task UpsertAsync(
        string schema,
        string table,
        string onConflict,
        JsonArray rows,
        CancellationToken ct,
        bool returnRepresentation = false)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var url = $"{_baseUrl}/rest/v1/{table}?on_conflict={onConflict}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        ApplyAuth(req);
        req.Headers.Add("Content-Profile", schema);
        req.Headers.Add("Prefer",
            returnRepresentation
                ? "resolution=merge-duplicates,return=representation"
                : "resolution=merge-duplicates,return=minimal");
        req.Content = new StringContent(rows.ToJsonString(), Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"UPSERT {schema}.{table} failed: {(int)res.StatusCode} {res.ReasonPhrase} — {body}");
        }
    }

    public async Task<JsonArray> UpsertReturningAsync(
        string schema,
        string table,
        string onConflict,
        JsonArray rows,
        CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return new JsonArray();
        }

        var url = $"{_baseUrl}/rest/v1/{table}?on_conflict={onConflict}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        ApplyAuth(req);
        req.Headers.Add("Content-Profile", schema);
        req.Headers.Add("Accept-Profile", schema);
        req.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");
        req.Content = new StringContent(rows.ToJsonString(), Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"UPSERT {schema}.{table} failed: {(int)res.StatusCode} {res.ReasonPhrase} — {body}");
        }
        return JsonNode.Parse(body) as JsonArray ?? new JsonArray();
    }

    public async Task PatchAsync(
        string schema,
        string table,
        string query,
        JsonObject patch,
        CancellationToken ct)
    {
        var url = $"{_baseUrl}/rest/v1/{table}?{query}";
        using var req = new HttpRequestMessage(HttpMethod.Patch, url);
        ApplyAuth(req);
        req.Headers.Add("Content-Profile", schema);
        req.Headers.Add("Prefer", "return=minimal");
        req.Content = new StringContent(patch.ToJsonString(), Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"PATCH {schema}.{table} failed: {(int)res.StatusCode} {res.ReasonPhrase} — {body}");
        }
    }

    private void ApplyAuth(HttpRequestMessage req)
    {
        req.Headers.Add("apikey", _serviceKey);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);
    }
}
