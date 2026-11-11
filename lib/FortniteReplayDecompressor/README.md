# FortniteReplayDecompressor

A copy of [FortniteReplayDecompressor](https://github.com/Shiqan/FortniteReplayDecompressor) by Shiqan (MIT, see `LICENSE`), taken at commit `76f74c1` (version 3.1.0). Only the four projects Chest Counter needs are kept: `Unreal.Core`, `Unreal.Encryption`, `OozSharp` and `FortniteReplayReader`.

`OozSharp/Kraken.cs` is a C# port of [ooz](https://github.com/powzix/ooz) by Powzix, under GPL-3.0-or-later, as stated in its header.

## Changes made for Chest Counter

- `Unreal.Core/Models/NetGuidCache.cs`: a level instance named after its class with a number and an instance suffix (`Tiered_Chest_Athena5_6e02a0a859c897db_11`) is matched to that exact class (`Tiered_Chest_Athena_C`). Before, these chests were never read.
- `Unreal.Core/Models/NetGuidCache.cs`: when a new export group arrives, the paths that found no group are tried again. Before, an actor sent before its class was exported stayed unread for the whole replay.
- `Unreal.Core/NetFieldParser.cs`: `GroupAlias` lets the application read a group that has no type of its own with a declared type. Chest Counter uses it for chest classes it has no export for.
