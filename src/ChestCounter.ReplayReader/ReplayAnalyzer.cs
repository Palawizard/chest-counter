using System.Numerics;
using System.Text.RegularExpressions;
using FortniteReplayReader.Models.NetFieldExports;
using Unreal.Core.Contracts;
using Unreal.Core.Models;
using Unreal.Core.Models.Enums;

namespace ChestCounter;

public enum ReplayStatus { InProgress, Failed, Analyzed }

public sealed record MatchRecord(string Id, string FileName, DateTime PlayedAt, string Branch, int Chests, double? BrokenNearby = null, int BrokenMethod = 0)
{
    public const int CurrentBrokenMethod = 1;
}

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

            var match = new MatchRecord(replay.Header.Guid, Path.GetFileName(path), replay.Info.Timestamp, replay.Header.Branch, reader.CountOpenings(recorder), reader.EstimateBroken(recorder), MatchRecord.CurrentBrokenMethod);
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

internal sealed partial class OwnerChestReader : FortniteReplayReader.ReplayReader
{
    private readonly Dictionary<uint, uint> _pawnToPlayerState = [];
    private readonly Dictionary<uint, ChestState> _chests = [];
    private readonly List<uint> _openers = [];
    private readonly List<double> _openingTimes = [];
    private readonly Dictionary<uint, uint> _pawnChannels = [];
    private readonly Dictionary<uint, Vector3> _pawnPositions = [];
    private readonly HashSet<uint> _lootChannels = [];
    private readonly List<(double Time, Vector3 At)> _loot = [];
    private double _worldTime;

    // Pawn positions and loot only come through at this level of detail.
    public OwnerChestReader() : base(null!, ParseMode.Full)
    {
    }

    public int CountOpenings(uint recorderPlayerState) =>
        _openers.Count(pawn => _pawnToPlayerState.GetValueOrDefault(pawn) == recorderPlayerState);

    internal int DestroyedWithLootInside => DestroyedWithLoot().Count;

    public double EstimateBroken(uint recorderPlayerState)
    {
        var ownerPawns = _pawnToPlayerState.Where(pair => pair.Value == recorderPlayerState).Select(pair => pair.Key).ToHashSet();
        return BreakEstimator.Estimate(DestroyedWithLoot(), _openingTimes, _loot, ownerPawns);
    }

    // Many chests vanish by closing their channel without ever replicating bDestroyed. The closing
    // bunch is read first because it can still carry the chest's last state.
    public override bool ReceivedSequencedBunch(DataBunch bunch)
    {
        uint? destroyedChest = null;
        if (bunch.bClose)
        {
            if (bunch.CloseReason == ChannelCloseReason.Destroyed
                && Channels[bunch.ChIndex]?.ActorId is uint actor
                && _netGuidCache.NetGuidToPathName.TryGetValue(actor, out var path)
                && ChestPath().IsMatch(path))
            {
                destroyedChest = actor;
            }

            if (_pawnChannels.Remove(bunch.ChIndex, out var pawn))
            {
                _pawnPositions.Remove(pawn);
            }

            _lootChannels.Remove(bunch.ChIndex);
        }

        var result = base.ReceivedSequencedBunch(bunch);
        if (destroyedChest is uint chest)
        {
            MarkDestroyed(StateOf(chest));
        }

        return result;
    }

    protected override void OnExportRead(uint channelIndex, INetFieldExportGroup? exportGroup)
    {
        base.OnExportRead(channelIndex, exportGroup);
        if (exportGroup is GameState state && (state.ReplicatedWorldTimeSecondsDouble ?? state.ReplicatedWorldTimeSeconds) is { } time)
        {
            _worldTime = time;
        }

        if (Channels[channelIndex]?.ActorId is not uint actor)
        {
            return;
        }

        switch (exportGroup)
        {
            case PlayerPawn pawn:
                TrackPawn(channelIndex, actor, pawn);
                break;
            case FortPickup pickup when pickup.bTossedFromContainer == true && _lootChannels.Add(channelIndex) && Channels[channelIndex]?.Actor?.Location is { } at:
                _loot.Add((_worldTime, new Vector3((float)at.X, (float)at.Y, (float)at.Z)));
                break;
            case SearchableChest chest:
                Track(actor, chest);
                break;
        }
    }

    private void TrackPawn(uint channelIndex, uint actor, PlayerPawn pawn)
    {
        _pawnChannels[channelIndex] = actor;
        if (pawn.PlayerState is uint playerState and not 0)
        {
            _pawnToPlayerState[actor] = playerState;
        }

        if (pawn.ReplicatedMovement?.Location is { } at)
        {
            _pawnPositions[actor] = new Vector3((float)at.X, (float)at.Y, (float)at.Z);
        }
    }

    // Chests refill during a match, so each false-to-true transition is a separate opening.
    internal void Track(uint actor, SearchableChest export)
    {
        var state = StateOf(actor);
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
                _openingTimes.Add(_worldTime);
            }
        }

        if (export.bDestroyed == true)
        {
            MarkDestroyed(state);
        }
    }

    private List<DestroyedChest> DestroyedWithLoot() => _chests.Values
        .Where(chest => chest.Destroyed is not null && !chest.SearchedWhenDestroyed)
        .Select(chest => chest.Destroyed!.Value)
        .ToList();

    private ChestState StateOf(uint actor)
    {
        if (!_chests.TryGetValue(actor, out var state))
        {
            _chests[actor] = state = new ChestState();
        }

        return state;
    }

    private void MarkDestroyed(ChestState state)
    {
        if (state.Destroyed is null)
        {
            state.SearchedWhenDestroyed = state.Searched;
            state.Destroyed = new DestroyedChest(_worldTime, new Dictionary<uint, Vector3>(_pawnPositions));
        }
    }

    [GeneratedRegex("^(Tiered_Chest_Athena|AlwaysSpawn_NormalChest|AlwaysSpawn_RareChest|B_Athena_BuriedChest_Chest|B_TieredChest_Relics|B_SpritePosters_Booty_Chest|B_Delilah_TieredChest_Relics)", RegexOptions.IgnoreCase)]
    private static partial Regex ChestPath();

    private sealed class ChestState
    {
        public uint? LastPawn;
        public bool Searched;
        public bool SearchedWhenDestroyed;
        public DestroyedChest? Destroyed;
    }
}
