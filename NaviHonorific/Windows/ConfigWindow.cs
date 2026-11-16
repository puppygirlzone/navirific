using System;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using NaviHonorific.Navidrome;

namespace NaviHonorific.Windows;

public class ConfigWindow : Window
{
    private static readonly Track SampleTrack =
        new("", "Everything In Its Right Place", "Radiohead", "Kid A", 2000, "", "", 0);

    private readonly Plugin plugin;
    private Config Config => plugin.Config;

    private Task<string?>? test;

    public ConfigWindow(Plugin plugin) : base("NaviHonorific###NaviHonorificConfig")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 380),
            MaximumSize = new Vector2(900, 1200),
        };
        Size = new Vector2(460, 560);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var enabled = Config.Enabled;
        if (ImGui.Checkbox("Show my Navidrome track as my title", ref enabled))
        {
            Config.Enabled = enabled;
            plugin.SettingsChanged(repoll: true);
        }

        DrawStatus();

        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Server", ImGuiTreeNodeFlags.DefaultOpen))
            DrawServer();

        if (ImGui.CollapsingHeader("Title", ImGuiTreeNodeFlags.DefaultOpen))
            DrawTitle();
    }

    private void DrawStatus()
    {
        var watcher = plugin.Watcher;

        if (!plugin.Title.HonorificAvailable)
            ImGui.TextColored(ImGuiColors.DalamudRed, "Honorific isn't loaded. Install or enable it first.");
        else if (!Config.IsConfigured)
            ImGui.TextColored(ImGuiColors.DalamudYellow, "Fill in your server details below to get started.");
        else if (watcher.LastError != null)
            ImGui.TextColored(ImGuiColors.DalamudRed, $"Can't reach Navidrome: {watcher.LastError}");
        else if (!Config.Enabled)
            ImGui.TextDisabled("Turned off.");
        else if (watcher.Current is { } t)
            ImGui.TextColored(ImGuiColors.HealerGreen, $"Playing: {t.Title} - {t.Artist}");
        else
            ImGui.TextDisabled("Nothing playing right now.");

        if (Config.Enabled && Config.IsConfigured && watcher.LastPoll != default)
        {
            var ago = (DateTime.Now - watcher.LastPoll).TotalSeconds;
            ImGui.TextDisabled($"Last checked {ago:0.0}s ago, took {watcher.LastLatency.TotalMilliseconds:0}ms");
        }
    }

    private void DrawServer()
    {
        // Connection details only take effect once you leave the field, so we
        // aren't hammering the server with half-typed URLs.
        var url = Config.ServerUrl;
        if (ImGui.InputTextWithHint("Server URL", "https://music.example.com", ref url, 512))
            Config.ServerUrl = url.Trim();
        if (ImGui.IsItemDeactivatedAfterEdit()) plugin.SettingsChanged(repoll: true);

        var user = Config.Username;
        if (ImGui.InputText("Username", ref user, 128))
            Config.Username = user.Trim();
        if (ImGui.IsItemDeactivatedAfterEdit()) plugin.SettingsChanged(repoll: true);

        var pass = Config.Password;
        if (ImGui.InputText("Password", ref pass, 256, ImGuiInputTextFlags.Password))
            Config.Password = pass;
        if (ImGui.IsItemDeactivatedAfterEdit()) plugin.SettingsChanged(repoll: true);

        var busy = test is { IsCompleted: false };
        if (busy) ImGui.BeginDisabled();
        if (ImGui.Button("Test connection"))
            test = plugin.Watcher.TestConnection(Config.ServerUrl, Config.Username, Config.Password);
        if (busy) ImGui.EndDisabled();

        if (test != null)
        {
            ImGui.SameLine();
            if (!test.IsCompleted)
                ImGui.TextDisabled("Checking...");
            else if (test.Result == null)
                ImGui.TextColored(ImGuiColors.HealerGreen, "Connected!");
            else
                ImGui.TextColored(ImGuiColors.DalamudRed, test.Result);
        }

        ImGui.Spacing();

        var filter = Config.PlayerFilter;
        if (ImGui.InputTextWithHint("Only from player", "any", ref filter, 128))
            Config.PlayerFilter = filter;
        if (ImGui.IsItemDeactivatedAfterEdit()) plugin.SettingsChanged(repoll: true);
        Help("If you listen on more than one device, put the client name here (e.g. Feishin, Symfonium) " +
             "and only that one counts. Leave it empty to use whatever you played most recently.");

        var poll = Config.PollMs;
        if (ImGui.SliderInt("Check every", ref poll, NowPlayingWatcher.MinPollMs, 10000, "%d ms"))
        {
            Config.PollMs = Math.Max(NowPlayingWatcher.MinPollMs, poll);
            plugin.SettingsChanged();
        }
        Help("Lower = title changes sooner after you skip a song. It's one tiny request each time, " +
             "so 1000-2000 ms is fine even on a home server.");
    }

    private void DrawTitle()
    {
        var template = Config.Template;
        if (ImGui.InputText("Template", ref template, 128))
        {
            Config.Template = template;
            plugin.SettingsChanged();
        }
        Help("Use {title}, {artist}, {album} and {year}.\nHonorific caps titles at 32 characters, " +
             "anything longer gets cut off with \"...\".");

        var preview = HonorificTitle.Render(Config.Template, plugin.Watcher.Current ?? SampleTrack);
        ImGui.TextDisabled("Preview:");
        ImGui.SameLine();
        var color = Config.UseColor ? new Vector4(Config.Color, 1f) : ImGuiColors.DalamudWhite;
        ImGui.TextColored(color, preview.Length == 0 ? "(empty, nothing will show)" : $"<<{preview}>>");

        var prefix = Config.IsPrefix;
        if (ImGui.Checkbox("Show above my name (prefix)", ref prefix))
        {
            Config.IsPrefix = prefix;
            plugin.SettingsChanged();
        }

        ImGui.Spacing();

        var useColor = Config.UseColor;
        if (ImGui.Checkbox("##usecolor", ref useColor))
        {
            Config.UseColor = useColor;
            plugin.SettingsChanged();
        }
        ImGui.SameLine();
        var c = Config.Color;
        if (ImGui.ColorEdit3("Text colour", ref c, ImGuiColorEditFlags.NoInputs))
        {
            Config.Color = c;
            Config.UseColor = true;
            plugin.SettingsChanged();
        }

        var useGlow = Config.UseGlow;
        if (ImGui.Checkbox("##useglow", ref useGlow))
        {
            Config.UseGlow = useGlow;
            plugin.SettingsChanged();
        }
        ImGui.SameLine();
        var g = Config.Glow;
        if (ImGui.ColorEdit3("Glow colour", ref g, ImGuiColorEditFlags.NoInputs))
        {
            Config.Glow = g;
            Config.UseGlow = true;
            plugin.SettingsChanged();
        }
    }

    private static void Help(string text)
    {
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(text);
    }
}
