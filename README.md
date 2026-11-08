# Chest Counter

A small Windows app that counts the chests you open in Fortnite, from the replay files saved on your PC, during the Treasure Hunters mini-season (from 1 November 2026, build 43.x).

It shows how many matches it analyzed, the chests you opened and the average per match. If you enter how many matches have no replay, it also estimates their chests from the current average, and keeps the measured and estimated parts separate.

Replays are only ever opened for reading. Results are stored in `%LOCALAPPDATA%\ChestCounter\state.json`, so a match stays counted after Fortnite deletes its replay.

## Requirements

Windows, the .NET 10 SDK and the Microsoft Edge WebView2 runtime (included with Windows 11).

## Usage

```
dotnet run --project src/ChestCounter -c Release
```

Leave it open or minimized while you play: a replay is counted once its match has ended, so the counter does not move during a match. Closing the window stops the watching.

Optional arguments: `<replay folder> <state file>`.

## Tests

The tests that use a real replay need one at `tests/fixtures/sample.replay`. Copy any finished 43.x replay from `%LOCALAPPDATA%\FortniteGame\Saved\Demos` there. The file is git-ignored.

```
dotnet test
```

Without a replay:

```
dotnet test --filter "Category!=RealReplay"
```

## Installer

Building the installer requires [Inno Setup 6](https://jrsoftware.org/isinfo.php). From PowerShell:

```
dotnet publish src/ChestCounter -c Release -r win-x64 --self-contained -o dist/app
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\ChestCounter.iss
```

The result is `dist\ChestCounter-Setup-<version>.exe`. It installs for the current user without administrator rights and includes .NET. Uninstalling keeps the saved results. The installer is not signed, so Windows SmartScreen warns before it runs.

## What is counted

Every opening of a chest by the replay owner: regular, rare, buried, relic and wanted poster chests. A chest that refills and is opened again counts again. Ammo boxes and coolers are not counted.

## Limits

- Fortnite keeps about 100 replays. A match whose replay is deleted before the app reads it can only be estimated, so run the app at least once every 100 matches.
- Replays do not say who broke a chest. Chests destroyed without being opened (hit directly or falling with what held them) are estimated: a destruction counts for you when you were the closest player, within 10 m, of the loot it dropped. The estimate is shown apart from the measured count, which is the number to compare with the in-game counter. A match whose replay was deleted before this estimate existed gets the broken-per-opened ratio of the other matches. Relic finder chests cannot be attributed and are not counted.
- A new chest type added by a game update is ignored until its class is added to `ChestExports.cs`.
- All game modes present in the replay folder are counted.
