<p align="center">
  <img src="assets/icon.png" alt="" width="96" height="96">
</p>

<h1 align="center">Chest Counter</h1>

<p align="center">
  Count every chest you open in Fortnite, match by match, from the replays already on your PC.<br>
  A free and open-source Windows app that works offline.
</p>

<p align="center">
  <a href="https://github.com/Palawizard/chest-counter/releases/latest/download/Palawi.ChestCounter-win-Setup.exe"><strong>Download for Windows</strong></a>
  ·
  <a href="https://github.com/Palawizard/chest-counter/releases">All releases</a>
</p>

![Chest Counter in light and dark mode, with example data](assets/screenshot.png)

During the Treasure Hunters mini-season (from 1 November 2026), every chest you open is a chance at the rare Pirate Shop access token. Chest Counter tells you how many you have opened so far, and how that number was built.

It reads the replay files Fortnite saves after each match, finds every chest you opened in them, and keeps the result even after Fortnite deletes the replay. The window can stay open in the background: it updates on its own when a match ends.

## Features

- Each chest opening is read from your own replays.
- Matches without a replay and chests you broke instead of opening are estimated on a separate line, so the measured part of the total stays visible.
- Fortnite only keeps about 100 replays. Chest Counter saves every match it reads, so a deleted replay stays counted.
- No account or login, and no Epic or tracker API. Your replays stay on your PC.
- The window has a single field to fill in once, and nothing moves while you play.
- French or English, depending on your Windows language.
- New versions install themselves from GitHub releases.

## How it compares

Chest Counter is not the only way to count your chests. As of 11 November 2026, there are four other kinds of counters:

- **The in-game counter**: Quests tab, Treasure Hunt section.
- **Manual counters**: web pages where you press +1 after each chest, such as [Loot Counter](https://lootcounter.com/) or the [Pirate Token Tracker](https://pirateshoptoken.com/pirate-token-tracker/).
- **Other replay apps**: Windows apps that read the same replay files, such as [Fortnite-ChestCounter](https://github.com/LED-N/Fortnite-ChestCounter), [fortnite-chest-counter](https://github.com/rossalv/fortnite-chest-counter), [chest-tracker](https://github.com/thedackss/chest-tracker) and [Fortnite Chest Tracker](https://github.com/ayman-studio/fortnite-chest-tracker).
- **Replay websites**: [How Many Chests](https://howmanychests.com/), where you drop your replays on the page, and [Chest Tracker](https://chesttracker.xyz/), whose Windows app sends your matches to a profile on the site.

| | Chest Counter | In-game counter | Manual counters | Other replay apps | Replay websites |
|---|---|---|---|---|---|
| Counts on its own | Yes | Yes | No, you press +1 after each chest | Yes | Yes, once your replays are sent |
| Keeps counting past 1,000 chests | Yes | Players report that it stops at the 1,000-chest stage | Yes | Yes | Yes |
| Matches played before you started | Estimated from your first 20 counted matches, on a separate line | Counted | A starting number to type | Lost, a starting number to type, or an estimate, depending on the app | Only those whose replay is still on your PC |
| Chests you broke without opening | Estimated, on a separate line | Not documented | If you count them | Counted with the opened ones in most of them | Shown apart by Chest Tracker |
| Shown over the game | No, a window to keep beside it | No, in the Quests tab | No, on another screen or a phone | Yes, as an overlay | No |
| Your replays stay on your PC | Yes | Nothing is read | Nothing is read | Yes | No |

The in-game counter is the official number. Compare it with Chest Counter's **Read from replays** line, not with the estimated total.

### Replay apps side by side

| | Chest Counter | [Fortnite-ChestCounter](https://github.com/LED-N/Fortnite-ChestCounter) | [fortnite-chest-counter](https://github.com/rossalv/fortnite-chest-counter) | [chest-tracker](https://github.com/thedackss/chest-tracker) | [Fortnite Chest Tracker](https://github.com/ayman-studio/fortnite-chest-tracker) |
|---|---|---|---|---|---|
| Overlay over the game | No | Yes, and an OBS source | Yes, click-through | Yes | Yes, and an OBS source |
| Matches played while it is closed | Read at the next launch, while the replay exists | Not counted | Not stated | Not stated | Not stated |
| Keeps a match after Fortnite deletes its replay | Yes | Yes | Yes | Not stated | Yes, and can keep a copy of the replays |
| Matches whose replay was already gone | Estimated from your first 20 counted matches | Not counted | Not counted | You start from your in-game count | Estimated from your match count on fortnite.gg |
| Chests you broke without opening | Estimated (closest player within 10 m), on a separate line | Counted | Not counted | In the total, not in the quest count | Counted |
| Install and updates | Installer, updates itself | Single exe, checks for updates | Single exe, needs the .NET 10 Desktop Runtime | Single exe | Single exe or optional install, can update itself |
| Also | French and English | Several accounts, treasure hunt prizes, English and French | Per-match table in a console tool | Notification and HTML report after each match | Pirate token odds, several accounts, English, French and Spanish |
| License | GPL-3.0 | MIT | MIT | MIT | MIT code, GPL-3.0 exe |

Checked on 11 November 2026 from each project's own page; "Not stated" means the page does not say. New counters appear every week, and these projects change fast.

Most of these apps, Chest Counter included, read replays with [FortniteReplayDecompressor](https://github.com/Shiqan/FortniteReplayDecompressor).

## Install

1. Download [the installer](https://github.com/Palawizard/chest-counter/releases/latest/download/Palawi.ChestCounter-win-Setup.exe) and run it. It installs for your Windows user only, without administrator rights, and adds a shortcut to the Start menu and the desktop.
2. Windows SmartScreen may warn that the app is unrecognized, because the installer is not code-signed. Click **More info**, then **Run anyway**.
3. In Fortnite, turn on replay recording in the settings (Replays section). Chest Counter can only count matches that left a replay.

Requires Windows 10 or 11 (64-bit) and the Microsoft Edge WebView2 runtime, which Windows 11 already includes.

Updates download in the background. A new version is applied the next time you open the app, or right away with the **Restart** button that appears at the bottom of the window.

Uninstall from **Settings > Apps**. Your counted matches are kept in `%LOCALAPPDATA%\ChestCounter` and survive an uninstall or reinstall.

## Usage

Open Chest Counter and leave it open or minimized while you play. A match is counted once it has ended: Fortnite encrypts the replay of a match in progress until the match is over.

The big number is the estimated total. The line under it, Read from replays, counts the chests opened in matches that have a replay; compare this one with the in-game counter. The next line, Estimated, adds the chests from matches without a replay and the chests you broke without opening them.

If you played matches before installing Chest Counter, or Fortnite already deleted their replays, enter how many in **Matches without a replay**. Those are almost always your oldest matches, played before you got better at finding chests, so they are estimated from your first 20 counted matches.

## What is counted

Every opening of a chest by you: regular, rare, buried, relic and wanted poster chests. A chest that refills and is opened again counts again. Ammo boxes and coolers are not counted.

## Limits

- Fortnite keeps about 100 replays. A match whose replay is deleted before the app reads it can only be estimated, so open Chest Counter at least once every 100 matches.
- Replays do not say who broke a chest. A chest destroyed while it still held loot counts for you when you were the closest player, within 10 m, of the loot it dropped. Your recorded position does not follow a vehicle, so a chest destroyed while you are driving counts for 0.7. This part is always shown as estimated.
- Relic finder chests cannot be attributed and are not counted.
- A chest type added by a game update is ignored until the app is updated.
- All game modes found in the replay folder are counted.

## Privacy

Chest Counter only reads your replay folder (`%LOCALAPPDATA%\FortniteGame\Saved\Demos`) and never modifies it. The app itself only goes online to check this repository's GitHub releases for updates.

## Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet run --project src/ChestCounter -c Release
```

Optional arguments: `<replay folder> <state file>`.

Tests:

```
dotnet test --filter "Category!=RealReplay"
```

The tests marked `RealReplay` need a finished replay copied to `tests/fixtures/sample.replay` (git-ignored).

Installer and update packages are built with [Velopack](https://velopack.io). Install its CLI once with `dotnet tool install -g vpk --version 1.2.161`, then:

```
powershell -ExecutionPolicy Bypass -File scripts/release.ps1
```

The output is in `dist\releases`. Add `-Upload` to publish it as a draft GitHub release (uses the `gh` CLI login).

## License

[GPL-3.0](LICENSE). The figures use [Lilita One](https://github.com/google/fonts/tree/main/ofl/lilitaone) under the SIL Open Font License.

Chest Counter is not affiliated with or endorsed by Epic Games. Fortnite is a trademark of Epic Games, Inc.
