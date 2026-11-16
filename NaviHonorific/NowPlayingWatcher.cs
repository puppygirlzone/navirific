using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NaviHonorific.Navidrome;

namespace NaviHonorific;

// Runs off the game thread. Plugin.OnUpdate picks up Current from here.
public sealed class NowPlayingWatcher : IDisposable
{
    public const int MinPollMs = 500;

    // How long a dead server gets before we give up on the last song.
    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromSeconds(30);

    private readonly Config config;
    private readonly SubsonicClient client = new();
    private readonly CancellationTokenSource stop = new();
    private volatile CancellationTokenSource wake = new();
    private readonly Task loop;

    public Track? Current { get; private set; }
    public string? LastError { get; private set; }
    public DateTime LastPoll { get; private set; }
    public TimeSpan LastLatency { get; private set; }

    private DateTime? failingSince;

    // Bumped every time Current changes, so the title side can do a cheap int compare.
    public int Revision => revision;
    private int revision;

    public NowPlayingWatcher(Config config)
    {
        this.config = config;
        loop = Task.Run(Run);
    }

    // Skip whatever's left of the current wait and poll right away.
    // Called when settings change so they apply without waiting a full interval.
    public void Poke()
    {
        try { wake.Cancel(); } catch (ObjectDisposedException) { }
    }

    public Task<string?> TestConnection(string server, string user, string password) =>
        Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                await client.Ping(server, user, password, cts.Token);
                return (string?)null;
            }
            catch (Exception e)
            {
                return Describe(e);
            }
        });

    private async Task Run()
    {
        while (!stop.IsCancellationRequested)
        {
            if (config.Enabled && config.IsConfigured)
            {
                await PollOnce();
            }
            else
            {
                SetTrack(null);
                LastError = null;
                failingSince = null;
            }

            var delay = Math.Max(MinPollMs, config.PollMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stop.Token, wake.Token);
            try
            {
                await Task.Delay(delay, linked.Token);
            }
            catch (OperationCanceledException)
            {
                if (stop.IsCancellationRequested) break;
                var old = wake;
                wake = new CancellationTokenSource();
                old.Dispose();
            }
        }
    }

    private async Task PollOnce()
    {
        var started = DateTime.UtcNow;
        try
        {
            var entries = await client.NowPlaying(config.ServerUrl, config.Username, config.Password, stop.Token);
            LastLatency = DateTime.UtcNow - started;
            LastError = null;
            failingSince = null;

            var filter = config.PlayerFilter.Trim();
            var mine = entries
                .Where(e => e.User.Equals(config.Username, StringComparison.OrdinalIgnoreCase))
                .Where(e => filter.Length == 0 || e.Player.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.MinutesAgo)
                .FirstOrDefault();

            SetTrack(mine);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            // Ride out short blips so the title doesn't flicker, but don't leave
            // a song up forever if the server's actually gone.
            LastError = Describe(e);
            failingSince ??= DateTime.UtcNow;
            if (DateTime.UtcNow - failingSince > GiveUpAfter)
                SetTrack(null);
            Plugin.Log.Debug($"Navidrome poll failed: {LastError}");
        }
        finally
        {
            LastPoll = DateTime.Now;
        }
    }

    private void SetTrack(Track? track)
    {
        if (track == Current) return;
        Current = track;
        Interlocked.Increment(ref revision);
    }

    private static string Describe(Exception e) => e switch
    {
        SubsonicException se => se.Message,
        TaskCanceledException => "Timed out",
        System.Net.Http.HttpRequestException he => he.Message,
        UriFormatException or InvalidOperationException => "That server URL doesn't look right",
        _ => e.Message,
    };

    public void Dispose()
    {
        stop.Cancel();
        var finished = false;
        try { finished = loop.Wait(TimeSpan.FromSeconds(2)); } catch { finished = true; }

        // If the loop is somehow still going, let the GC have these instead of
        // pulling them out from under it.
        if (!finished) return;
        stop.Dispose();
        wake.Dispose();
        client.Dispose();
    }
}
