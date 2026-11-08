# Chest Counter

A small Windows app that counts the chests you open in Fortnite, from the replay files saved on your PC, during the Treasure Hunters mini-season (from 1 November 2026, build 43.x).

It shows how many matches it analyzed, the chests you opened and the average per match. If you enter how many matches have no replay, it also estimates their chests from the current average, and keeps the measured and estimated parts separate.

Replays are only ever opened for reading. Results are stored in `%LOCALAPPDATA%\ChestCounter\state.json`, so a match stays counted after Fortnite deletes its replay.

## Requirements

Windows and the .NET 10 SDK.

## Usage

```
dotnet run --project src/ChestCounter -c Release
```

Leave it open while you play: a replay is counted once its match has ended.

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

## What is counted

Every opening of a chest by the replay owner: regular, rare, buried, relic and wanted poster chests. A chest that refills and is opened again counts again. Ammo boxes and coolers are not counted.

## Limits

- Fortnite keeps about 100 replays. A match whose replay is deleted before the app reads it can only be estimated, so run the app at least once every 100 matches.
- Chests broken instead of opened, and relic finder chests, cannot be attributed to a player and are not counted.
- A new chest type added by a game update is ignored until its class is added to `ChestExports.cs`.
- All game modes present in the replay folder are counted.
