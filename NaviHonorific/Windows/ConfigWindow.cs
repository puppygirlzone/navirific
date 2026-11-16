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

    // Typed into here, copied to Config when you click out of the box.
    private string url = "", user = "", pass = "", filter = "";

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

    public override void OnOpen()
    {
        url = Config.ServerUrl;
        user = Config.Username;
        pass = Config.Password;
        filter = Config.PlayerFilter;
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
        // Don't poll half-typed URLs.
        ImGui.InputTextWithHint("Server URL", "https://music.example.com", ref url, 512);
        if (ImGui.IsItemDeactivatedAfterEdit()) CommitConnection();

        ImGui.InputText("Username", ref user, 128);
        if (ImGui.IsItemDeactivatedAfterEdit()) CommitConnection();

        ImGui.InputText("Password", ref pass, 256, ImGuiInputTextFlags.Password);
        if (ImGui.IsItemDeactivatedAfterEdit()) CommitConnection();

        var busy = test is { IsCompleted: false };
        if (busy) ImGui.BeginDisabled();
        if (ImGui.Button("Test connection"))
        {
            CommitConnection();
            test = plugin.Watcher.TestConnection(Config.ServerUrl, Config.Username, Config.Password);
        }
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

        ImGui.InputTextWithHint("Only from player", "any", ref filter, 128);
        if (ImGui.IsItemDeactivatedAfterEdit()) CommitConnection();
        Help("If you listen on more than one device, put the client name here (e.g. Feishin, Symfonium) " +
             "and only that one counts. Leave it empty to use whatever you played most recently.");

        // Slider works in half-second steps; nobody needs 1337 ms.
        var steps = Math.Clamp((int)Math.Round(Config.PollMs / 500.0), 1, 20);
        if (ImGui.SliderInt("Check every", ref steps, 1, 20, $"{steps * 0.5:0.0} s"))
        {
            Config.PollMs = steps * 500;
            plugin.SettingsChanged();
        }
        Help("Lower = title changes sooner after you skip a song. It's one tiny request each time, " +
             "so 1-2 seconds is fine even on a home server.");
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

    private void CommitConnection()
    {
        Config.ServerUrl = url.Trim();
        Config.Username = user.Trim();
        Config.Password = pass;
        Config.PlayerFilter = filter.Trim();
        plugin.SettingsChanged(repoll: true);
    }

    private static void Help(string text)
    {
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(text);
    }
}
