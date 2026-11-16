using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Navirific.Navidrome;

public record Track(string Id, string Title, string Artist, string Album, int? Year, string Player, string User, int MinutesAgo);

public class SubsonicException(string message) : Exception(message);

// Subsonic API rather than Navidrome's own, since that one wants a JWT.
public sealed class SubsonicClient : IDisposable
{
    private const string ApiVersion = "1.16.1";
    private const string ClientName = "Navirific";

    private readonly HttpClient http;

    public SubsonicClient()
    {
        // Keep the connection warm between polls; a fresh TLS handshake every
        // second or two is most of the latency otherwise.
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(5),
        };
        http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"{ClientName}/1.0");
    }

    public async Task Ping(string server, string user, string password, CancellationToken ct)
    {
        using var doc = await Get(server, "ping", user, password, ct);
    }

    public async Task<List<Track>> NowPlaying(string server, string user, string password, CancellationToken ct)
    {
        using var doc = await Get(server, "getNowPlaying", user, password, ct);
        var result = new List<Track>();

        var resp = doc.RootElement.GetProperty("subsonic-response");
        if (!resp.TryGetProperty("nowPlaying", out var np) || !np.TryGetProperty("entry", out var entries))
            return result;

        foreach (var e in entries.EnumerateArray())
        {
            result.Add(new Track(
                Str(e, "id"),
                Str(e, "title"),
                Str(e, "artist"),
                Str(e, "album"),
                e.TryGetProperty("year", out var y) && y.TryGetInt32(out var year) ? year : null,
                Str(e, "playerName"),
                Str(e, "username"),
                e.TryGetProperty("minutesAgo", out var m) && m.TryGetInt32(out var mins) ? mins : 0));
        }

        return result;
    }

    private async Task<JsonDocument> Get(string server, string method, string user, string password, CancellationToken ct)
    {
        var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var token = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(password + salt))).ToLowerInvariant();

        var url = $"{server.TrimEnd('/')}/rest/{method}.view" +
                  $"?u={Uri.EscapeDataString(user)}&t={token}&s={salt}&v={ApiVersion}&c={ClientName}&f=json";

        using var res = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!res.IsSuccessStatusCode)
            throw new SubsonicException($"HTTP {(int)res.StatusCode} {res.ReasonPhrase}");

        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        JsonDocument doc;
        try
        {
            doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch (JsonException)
        {
            throw new SubsonicException("Got something that isn't JSON back. Is the URL pointing at Navidrome?");
        }

        if (!doc.RootElement.TryGetProperty("subsonic-response", out var resp))
        {
            doc.Dispose();
            throw new SubsonicException("Response wasn't a Subsonic response. Is the URL pointing at Navidrome?");
        }

        if (Str(resp, "status") != "ok")
        {
            var msg = resp.TryGetProperty("error", out var err) ? Str(err, "message") : "";
            doc.Dispose();
            throw new SubsonicException(string.IsNullOrEmpty(msg) ? "Server said no (unknown error)" : msg);
        }

        return doc;
    }

    private static string Str(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    public void Dispose() => http.Dispose();
}
