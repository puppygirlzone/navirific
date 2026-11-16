using System;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Plugin.Ipc;
using Navirific.Navidrome;

namespace Navirific;

// Framework thread only.
public sealed class HonorificTitle : IDisposable
{
    // Honorific rejects anything longer than this.
    public const int MaxLength = 32;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        IncludeFields = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ICallGateSubscriber<int, string, object> setTitle;
    private readonly ICallGateSubscriber<int, object> clearTitle;
    private readonly ICallGateSubscriber<object> ready;
    private readonly ICallGateSubscriber<object> disposing;

    // What we last sent. null means "we haven't set anything", which is
    // different from "we cleared it".
    private string? sent;
    private DateTime retryAt;

    public HonorificTitle()
    {
        var pi = Plugin.PluginInterface;
        setTitle = pi.GetIpcSubscriber<int, string, object>("Honorific.SetCharacterTitle");
        clearTitle = pi.GetIpcSubscriber<int, object>("Honorific.ClearCharacterTitle");
        ready = pi.GetIpcSubscriber<object>("Honorific.Ready");
        disposing = pi.GetIpcSubscriber<object>("Honorific.Disposing");

        // If Honorific gets reloaded it forgets our title, so send it again.
        ready.Subscribe(Forget);
        disposing.Subscribe(Forget);
    }

    public bool HonorificAvailable => setTitle.HasAction;

    public bool InSync => sent != null || DateTime.UtcNow < retryAt;

    public string? Showing { get; private set; }

    // Forces the next Apply to actually send, e.g. after logging in.
    public void Forget() => sent = null;

    public void Apply(Track? track, Config config)
    {
        var text = track == null ? null : Render(config.Template, track);
        var json = string.IsNullOrWhiteSpace(text) ? "" : Serialize(text, config);

        if (json == sent) return;

        try
        {
            if (json.Length == 0)
                clearTitle.InvokeAction(0);
            else
                setTitle.InvokeAction(0, json);

            sent = json;
            Showing = json.Length == 0 ? null : text;
        }
        catch (Exception e)
        {
            // Honorific went away between the HasAction check and now. Ready will kick us.
            Plugin.Log.Verbose($"Honorific IPC failed: {e.Message}");
            sent = null;
            retryAt = DateTime.UtcNow.AddSeconds(2);
        }
    }

    public void Clear()
    {
        if (string.IsNullOrEmpty(sent)) return;
        try { clearTitle.InvokeAction(0); } catch { }
        sent = "";
        Showing = null;
    }

    public static string Render(string template, Track t)
    {
        var s = template
            .Replace("{title}", t.Title, StringComparison.OrdinalIgnoreCase)
            .Replace("{artist}", t.Artist, StringComparison.OrdinalIgnoreCase)
            .Replace("{album}", t.Album, StringComparison.OrdinalIgnoreCase)
            .Replace("{year}", t.Year?.ToString() ?? "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        // Cut on whole characters so we don't chop an emoji in half.
        if (s.Length > MaxLength)
        {
            var cut = 0;
            var e = StringInfo.GetTextElementEnumerator(s);
            while (e.MoveNext() && e.ElementIndex + e.GetTextElement().Length <= MaxLength - 3)
                cut = e.ElementIndex + e.GetTextElement().Length;
            s = s[..cut].TrimEnd() + "...";
        }

        return s;
    }

    private static string Serialize(string text, Config config) =>
        JsonSerializer.Serialize(new TitlePayload
        {
            Title = text,
            IsPrefix = config.IsPrefix,
            Color = config.UseColor ? config.Color : null,
            Glow = config.UseGlow ? config.Glow : null,
        }, JsonOpts);

    // Mirrors the bits of Honorific's TitleData we care about.
    private sealed class TitlePayload
    {
        public string Title { get; init; } = "";
        public bool IsPrefix { get; init; }
        public Vector3? Color { get; init; }
        public Vector3? Glow { get; init; }
    }

    public void Dispose()
    {
        ready.Unsubscribe(Forget);
        disposing.Unsubscribe(Forget);
        Clear();
    }
}
