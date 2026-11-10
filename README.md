<p align="center">
  <img src="assets/icon.png" alt="" width="96" height="96">
</p>

<h1 align="center">Chest Counter</h1>

<p align="center">
  Count every chest you open in Fortnite, match by match, from the replays already on your PC.<br>
  Free, open source, offline, for Windows.
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

- **Counted, not guessed.** Each chest opening is read from your own replays, one by one.
- **Measured and estimated stay apart.** Matches without a replay and chests you broke instead of opening are estimated, and shown on their own line, so you always know which part of the total is proven.
- **Keeps your history.** Fortnite only keeps about 100 replays. Chest Counter saves every match it reads, so deleted replays stay counted.
- **Works offline.** No account, no login, no Epic or tracker API. Your replays never leave your PC.
- **Quiet.** One window, one number to enter once, no animation while you play.
- **French and English**, picked from your Windows language.
- **Updates itself** in the background from GitHub releases.

## How it compares

As of November 2026, the other ways to know how many chests you opened:

| | Chest Counter | In-game counter (Quests tab) | Stats sites (Fortnite Tracker and similar) |
|---|---|---|---|
| Counts chests opened | Yes | Yes, as one running total | No chest count found |
| Detail per match | Yes | No | Not for chests |
| Separates measured from estimated | Yes | No | No |
| Needs the game or an account | No | Yes, in game | Public stats must be turned on |
| Works offline | Yes | No | No |
| Open source | Yes | No | No |

The in-game counter is the reference: Chest Counter's measured part is the number to compare it with. What Chest Counter adds is where the number comes from, what happens to matches whose replay is gone, and a count you can keep an eye on without opening the game menu.

Replay libraries such as [FortniteReplayDecompressor](https://github.com/Shiqan/FortniteReplayDecompressor) can read chest data, but they are libraries for developers, not an app. Chest Counter is built on it.

## Install

1. Download [the installer](https://github.com/Palawizard/chest-counter/releases/latest/download/Palawi.ChestCounter-win-Setup.exe) and run it. It installs for your Windows user only, without administrator rights, and adds a shortcut to the Start menu and the desktop.
2. Windows SmartScreen may warn that the app is unrecognized, because the installer is not code-signed. Click **More info**, then **Run anyway**.
3. In Fortnite, turn on replay recording in the settings (Replays section). Chest Counter can only count matches that left a replay.

Requires Windows 10 or 11 (64-bit) and the Microsoft Edge WebView2 runtime, which Windows 11 already includes.

Updates download in the background. A new version is applied the next time you open the app, or right away with the **Restart** button that appears at the bottom of the window.

Uninstall from **Settings > Apps**. Your counted matches are kept in `%LOCALAPPDATA%\ChestCounter` and survive an uninstall or reinstall.

## Usage

Open Chest Counter and leave it open or minimized while you play. A match is counted once it has ended: Fortnite encrypts the replay of a match in progress until the match is over.

The big number is the estimated total. Under it:

- **Read from replays**: chests opened in matches that have a replay. This is the number to compare with the in-game counter.
- **Estimated**: chests from matches without a replay, plus chests you broke without opening them.

If you played matches before installing Chest Counter, or Fortnite already deleted their replays, enter how many in **Matches without a replay**. They are estimated from your current average.

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

[GPL-3.0](LICENSE). The figures use [Chivo Mono](https://github.com/Omnibus-Type/Chivo) under the SIL Open Font License.

Chest Counter is not affiliated with or endorsed by Epic Games. Fortnite is a trademark of Epic Games, Inc.
