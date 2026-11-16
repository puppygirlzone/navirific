using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Navirific.Windows;

namespace Navirific;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string Command = "/navirific";
    private const string ShortCommand = "/nrf";

    public Config Config { get; }
    public NowPlayingWatcher Watcher { get; }
    public HonorificTitle Title { get; }

    private readonly WindowSystem windows = new("Navirific");
    private readonly ConfigWindow configWindow;

    private int appliedRevision = -1;
    private bool dirty;
    private DateTime? saveAt;

    public Plugin()
    {
        Config = PluginInterface.GetPluginConfig() as Config ?? new Config();

        Title = new HonorificTitle();
        Watcher = new NowPlayingWatcher(Config);

        configWindow = new ConfigWindow(this);
        windows.AddWindow(configWindow);

        var help = new CommandInfo(OnCommand)
        {
            HelpMessage = "Open settings. Add \"on\", \"off\" or \"toggle\" to switch it without opening the window.",
        };
        Commands.AddHandler(Command, help);
        Commands.AddHandler(ShortCommand, new CommandInfo(OnCommand) { HelpMessage = $"Same as {Command}." });

        PluginInterface.UiBuilder.Draw += windows.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += configWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi += configWindow.Toggle;

        Framework.Update += OnUpdate;
        ClientState.Login += OnLogin;
    }

    // Settings window calls this after any change. Makes sure the title is rebuilt
    // on the next frame even if the song didn't change. Saving waits a moment so
    // dragging a slider doesn't write the file sixty times a second.
    public void SettingsChanged(bool repoll = false)
    {
        saveAt = DateTime.UtcNow.AddMilliseconds(500);
        dirty = true;
        if (repoll) Watcher.Poke();
    }

    private void OnUpdate(IFramework _)
    {
        if (saveAt is { } due && DateTime.UtcNow >= due)
        {
            saveAt = null;
            Config.Save();
        }

        // Honorific silently ignores titles for a player object that doesn't exist yet,
        // so wait until we're actually standing somewhere.
        if (Objects.LocalPlayer == null) return;

        if (!Config.Enabled)
        {
            Title.Clear();
            return;
        }

        var rev = Watcher.Revision;
        if (rev == appliedRevision && !dirty && Title.InSync) return;
        if (!Title.HonorificAvailable) return;

        Title.Apply(Watcher.Current, Config);
        appliedRevision = rev;
        dirty = false;
    }

    private void OnLogin()
    {
        Title.Forget();
        dirty = true;
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "on":
                SetEnabled(true);
                break;
            case "off":
                SetEnabled(false);
                break;
            case "toggle":
                SetEnabled(!Config.Enabled);
                break;
            default:
                configWindow.Toggle();
                break;
        }
    }

    private void SetEnabled(bool on)
    {
        Config.Enabled = on;
        SettingsChanged(repoll: true);
        Chat.Print(on ? "Now showing your Navidrome track." : "Stopped. Title cleared.", "Navirific");
    }

    public void Dispose()
    {
        Framework.Update -= OnUpdate;
        ClientState.Login -= OnLogin;

        PluginInterface.UiBuilder.Draw -= windows.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= configWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi -= configWindow.Toggle;
        windows.RemoveAllWindows();

        Commands.RemoveHandler(Command);
        Commands.RemoveHandler(ShortCommand);

        if (saveAt != null) Config.Save();

        Watcher.Dispose();
        Title.Dispose();
    }
}
