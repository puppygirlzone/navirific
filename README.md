# navirific

Puts whatever you're listening to on Navidrome above (or below) your name in FFXIV, using [Honorific](https://github.com/Caraxi/Honorific).

There's already [SpotifyHonorific](https://github.com/Valiice/SpotifyHonorific) for Spotify. This is the same idea for people who host their own music.

```
                 Pup Girl
        «Everything In Its Right Place»
```

## What you need

- **Honorific**, from the normal Dalamud plugin list. This plugin only tells Honorific what to show, it doesn't draw anything itself.
- A **Navidrome** server you can reach from the PC you play on.
- A music client that tells Navidrome what's playing. Most do (Feishin, Symfonium, Tempo, Supersonic, the Navidrome web player). If your song shows up under "Now Playing" in Navidrome's web UI, you're good.

## Install

1. In game, type `/xlsettings` and go to **Experimental**.
2. Under **Custom Plugin Repositories**, paste this in and hit the **+**:

   ```
   https://raw.githubusercontent.com/puppygirlzone/navirific/main/repo.json
   ```

3. Tick the box next to it, then **Save and Close**.
4. Open `/xlplugins`, search for **NaviHonorific** and install it.

## Setup

Type `/navihonorific` (or `/nhon` if that's too long) to open the settings.

1. Put in your Navidrome URL, username and password. Same details you use for the web UI.
2. Hit **Test connection**. You want a green "Connected!".
3. Play something. The status line at the top should show it, and so should your nameplate.

That's it. Every setting applies the moment you change it, no reloading the plugin or restarting the game.

## Settings

| Setting | What it does |
|---|---|
| Show my Navidrome track as my title | The on/off switch. Off clears your title straight away. |
| Server URL | Where Navidrome lives, e.g. `https://music.example.com`. If it's on a subpath, include it (`https://example.com/navidrome`). |
| Username / Password | Your normal Navidrome login. |
| Only from player | If you play on more than one device, type the client name (Feishin, Symfonium...) and only that one counts. Empty means whatever played most recently. |
| Check every | How often to ask Navidrome. Default is 1500 ms, lowest is 500 ms. |
| Template | What the title says. `{title}`, `{artist}`, `{album}` and `{year}` get swapped in. |
| Prefix | Show the title above your name instead of below. |
| Text colour / Glow colour | Tick the box to use it, untick to fall back to Honorific's default. |

Some template ideas:

```
{title}
{artist} - {title}
listening to {artist}
{album} ({year})
```

Honorific won't show titles over 32 characters, so anything longer gets cut and ends in `...`. Short templates work best.

### Commands

- `/navihonorific` opens settings
- `/navihonorific on`, `off` or `toggle` switches it without opening the window
- `/nhon` does the same as all of the above

## How fast is it?

It asks Navidrome "what's playing for me?" every 1.5 seconds by default. The request is tiny and the connection is kept open between checks, so on a home server each check takes a few milliseconds. Skip a song and your title usually catches up within a second or two.

The polling happens off the game thread, so a slow or dead server won't stutter your game. If Navidrome drops out for a moment, your current title stays up instead of blinking off.

## Things that might trip you up

- **Pausing doesn't clear the title.** Navidrome doesn't know about pause, only "this started playing". The song stays in its now-playing list until it times out, so your title hangs around for a bit too. Use `/nhon off` if you want it gone now.
- **Nothing shows up, but the status says it's playing.** Make sure Honorific itself is enabled. The settings window says so in red if it can't find it.
- **Other people can't see it.** Honorific titles are local unless the other person has Honorific too and you're synced through something like Mare/Lightless. Same as any other Honorific title.
- **"Wrong username or password"** with the right password: if you log in to Navidrome through a reverse proxy or SSO, this plugin can't get through that. It needs a normal Navidrome account.

## Privacy

Your password is stored in your Dalamud plugin config folder (`pluginConfigs/NaviHonorific.json`) as plain text, the same way every other plugin stores settings. It's never sent as-is: each request uses the Subsonic salted token login. Still, use `https://` if your server is outside your house.

Nothing is sent anywhere except your own Navidrome server.

## Building it yourself

You need the .NET 10 SDK and Dalamud's dev files (XIVLauncher installs those for you if you've run the game with plugins enabled).

```bash
dotnet build -c Release
```

The plugin ends up in `NaviHonorific/bin/x64/Release/`. To load it without the repo, add that folder's `NaviHonorific.dll` under **Dev Plugin Locations** in `/xlsettings` → **Experimental**.

Releases are done by pushing a `v1.2.3` tag. GitHub Actions builds it, makes the release and updates `repo.json`.

## Layout

```
NaviHonorific/
  Plugin.cs              commands, framework hook, wiring
  Config.cs              saved settings
  NowPlayingWatcher.cs   background polling loop
  HonorificTitle.cs      Honorific IPC and the template
  Navidrome/
    SubsonicClient.cs    the two Subsonic calls we need (ping, getNowPlaying)
  Windows/
    ConfigWindow.cs      the settings window
repo.json                custom repo for Dalamud
```

## Licence

MIT. See [LICENSE](LICENSE).
