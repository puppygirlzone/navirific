using System;
using System.Numerics;
using Dalamud.Configuration;

namespace Navirific;

[Serializable]
public class Config : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    public string ServerUrl { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    // Only match a specific player/client (e.g. "Feishin"). Empty = any.
    public string PlayerFilter { get; set; } = "";

    public int PollMs { get; set; } = 1500;

    public string Template { get; set; } = "{title}";
    public bool IsPrefix { get; set; }

    public bool UseColor { get; set; }
    public Vector3 Color { get; set; } = new(1f, 1f, 1f);
    public bool UseGlow { get; set; } = true;
    public Vector3 Glow { get; set; } = new(0.42f, 0.27f, 0.78f);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ServerUrl) && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrEmpty(Password);

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
