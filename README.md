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

You never need to reload the plugin or restart the game for a setting to apply. Server details kick in when you click out of the box, everything else as you change it. Hover the (?) next to a setting if it's not obvious what it does.

If Navidrome lives on a subpath, include it in the URL (`https://example.com/navidrome`).

## Templates

`{title}`, `{artist}`, `{album}` and `{year}` get swapped in. A few I like:

```
{title}
{artist} - {title}
listening to {artist}
{album} ({year})
```

Honorific won't show titles over 32 characters, so anything longer gets cut and ends in `...`. Short templates work best.

## Commands

- `/navihonorific` opens settings
- `/navihonorific on`, `off` or `toggle` switches it without opening the window
- `/nhon` does the same as all of the above

## Speed

By default it asks Navidrome what you're playing every 1.5 seconds (you can go down to 0.5). The request is tiny and the connection stays open between checks, so on a home server it's a few milliseconds each time. Skip a song and the title catches up within a second or two.

None of this runs on the game thread, so a slow server won't stutter anything. If the server drops out, the current title stays up for 30 seconds before it gives up and clears.

## Known weirdness

Pausing doesn't clear the title. Navidrome has no idea you paused, only that a song started, and it keeps it in "now playing" until it times out. `/nhon off` if you want it gone right now.

If the settings window says you're playing something but your nameplate's empty, check Honorific is actually enabled. The window shows a red warning when it can't find it.

Other people only see it if they have Honorific too and you're synced through something like Lightless. Same as any Honorific title.

Getting "Wrong username or password" when it's definitely right? If you log in through a reverse proxy or SSO, the plugin can't get past that. It needs a normal Navidrome account.

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


## Licence

MIT. See [LICENSE](LICENSE).
