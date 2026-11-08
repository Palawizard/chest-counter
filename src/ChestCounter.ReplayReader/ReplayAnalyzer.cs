using FortniteReplayReader.Models.NetFieldExports;
using Unreal.Core.Contracts;
using Unreal.Core.Models.Enums;
using Unreal.Core.Models;

namespace ChestCounter;

public enum ReplayStatus { InProgress, Failed, Analyzed }

public sealed record MatchRecord(string Id, string FileName, DateTime PlayedAt, string Branch, int Chests);

public sealed record ReplayOutcome(ReplayStatus Status, MatchRecord? Match = null, string? Error = null);

public static class ReplayAnalyzer
{
    private const uint FileMagic = 0x1CA2E27F;

    public static ReplayOutcome Analyze(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (IsInProgress(stream))
            {
                return new ReplayOutcome(ReplayStatus.InProgress);
            }

            stream.Position = 0;
            var reader = new OwnerChestReader();
            var replay = reader.ReadReplay(stream);
            if (replay.GameData.RecorderId is not uint recorder || string.IsNullOrEmpty(replay.Header?.Guid))
            {
                return new ReplayOutcome(ReplayStatus.Failed, Error: "replay has no owner or id");
            }

            var match = new MatchRecord(replay.Header.Guid, Path.GetFileName(path), replay.Info.Timestamp, replay.Header.Branch, reader.CountOpenings(recorder));
            return new ReplayOutcome(ReplayStatus.Analyzed, match);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new ReplayOutcome(ReplayStatus.InProgress);
        }
        catch (Exception e)
        {
            return new ReplayOutcome(ReplayStatus.Failed, Error: $"{e.GetType().Name}: {e.Message}");
        }
    }

    // Reads the flag directly: the library loads the whole file before rejecting a live replay,
    // which is too heavy to repeat while the game is writing it.
    internal static bool IsInProgress(Stream stream)
    {
        if (LiveFlagOffset(stream) is not long offset)
        {
            return false;
        }

        stream.Position = offset;
        Span<byte> flag = stackalloc byte[4];
        return stream.Read(flag) < 4 || BitConverter.ToUInt32(flag) != 0;
    }

    internal static long? LiveFlagOffset(Stream stream)
    {
        stream.Position = 0;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        try
        {
            if (reader.ReadUInt32() != FileMagic)
            {
                return null;
            }

            if (reader.ReadUInt32() >= (uint)ReplayVersionHistory.HISTORY_CUSTOM_VERSIONS)
            {
                stream.Seek(reader.ReadInt32() * 20L, SeekOrigin.Current);
            }

            stream.Seek(12, SeekOrigin.Current);
            var nameLength = reader.ReadInt32();
            stream.Seek(nameLength < 0 ? -nameLength * 2L : nameLength, SeekOrigin.Current);
            return stream.Position;
        }
        catch (EndOfStreamException)
        {
            return stream.Length;
        }
    }
}

internal sealed class OwnerChestReader : FortniteReplayReader.ReplayReader
{
    private readonly Dictionary<uint, uint> _pawnToPlayerState = [];
    private readonly Dictionary<uint, ChestState> _chests = [];
    private readonly List<uint> _openers = [];

    public OwnerChestReader() : base(null!, ParseMode.Normal)
    {
    }

    public int CountOpenings(uint recorderPlayerState) =>
        _openers.Count(pawn => _pawnToPlayerState.GetValueOrDefault(pawn) == recorderPlayerState);

    protected override void OnExportRead(uint channelIndex, INetFieldExportGroup? exportGroup)
    {
        base.OnExportRead(channelIndex, exportGroup);
        if (Channels[channelIndex]?.ActorId is not uint actor)
        {
            return;
        }

        if (exportGroup is PlayerPawn { PlayerState: uint playerState and not 0 })
        {
            _pawnToPlayerState[actor] = playerState;
        }
        else if (exportGroup is SearchableChest chest)
        {
            Track(actor, chest);
        }
    }

    // Chests refill during a match, so each false-to-true transition is a separate opening.
    private void Track(uint actor, SearchableChest export)
    {
        if (!_chests.TryGetValue(actor, out var state))
        {
            _chests[actor] = state = new ChestState();
        }

        if (export.SearchingPawn is uint pawn and not 0)
        {
            state.LastPawn = pawn;
        }

        if (export.bAlreadySearched == false)
        {
            state.Searched = false;
        }
        else if (export.bAlreadySearched == true && !state.Searched)
        {
            state.Searched = true;
            if (state.LastPawn is uint opener)
            {
                _openers.Add(opener);
            }
        }
    }

    private sealed class ChestState
    {
        public uint? LastPawn;
        public bool Searched;
    }
}
