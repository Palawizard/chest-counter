using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using FortniteReplayReader.Models.NetFieldExports;
using Unreal.Core.Contracts;
using Unreal.Core.Models;
using Unreal.Core.Models.Enums;

namespace ChestCounter;

public enum ReplayStatus { InProgress, Failed, Analyzed, Cut }

// Method is the analysis that produced the record: older records are read again while their replay
// exists. Openings keeps each counted chest so a later version can correct a match whose replay is gone.
// Length is the replay's length in seconds.
public sealed record MatchRecord(string Id, string FileName, DateTime PlayedAt, string Branch, int Chests, double? BrokenNearby = null, int Method = 0, IReadOnlyList<ChestOpening>? Openings = null, double? Length = null)
{
    // 3: chests counted once when they come back into view (2.3). 2.2 wrote "BrokenMethod" instead.
    public const int CurrentMethod = 3;
}

// Short names keep state.json small: about 50 bytes per chest.
// Chest is a hash of the chest's identity within the match, not the identity itself.
public sealed record ChestOpening([property: JsonPropertyName("t")] double Time, [property: JsonPropertyName("c")] string Class, [property: JsonPropertyName("k")] string Chest);

// NewChestClasses: chest classes read without a declared type, so a game update's new chest shows up in the log.
public sealed record ReplayOutcome(ReplayStatus Status, MatchRecord? Match = null, string? Error = null, IReadOnlyList<string>? NewChestClasses = null);

public static class ReplayAnalyzer
{
    private const uint FileMagic = 0x1CA2E27F;

    // A match quit from the menu leaves a finished replay; one still marked live with no write for
    // this long was cut by a crash and will never be finished.
    internal static readonly TimeSpan CutAfter = TimeSpan.FromMinutes(10);

    public static ReplayOutcome Analyze(string path)
    {
        try
        {
            using var stream = OpenShared(path);
            if (IsInProgress(stream))
            {
                return new ReplayOutcome(DateTime.UtcNow - File.GetLastWriteTimeUtc(path) >= CutAfter ? ReplayStatus.Cut : ReplayStatus.InProgress);
            }

            stream.Position = 0;
            var reader = new OwnerChestReader();
            var replay = reader.ReadReplay(stream);
            if (replay.GameData.RecorderId is not uint recorder || string.IsNullOrEmpty(replay.Header?.Guid))
            {
                return new ReplayOutcome(ReplayStatus.Failed, Error: "replay has no owner or id");
            }

            using var drivingStream = OpenShared(path);
            var driving = new OwnerDrivingReader().ReadOwnerDrivingTimes(drivingStream);
            var match = new MatchRecord(replay.Header.Guid, Path.GetFileName(path), replay.Info.Timestamp, replay.Header.Branch, reader.CountOpenings(recorder), reader.EstimateBroken(recorder, driving), MatchRecord.CurrentMethod, reader.OpeningsBy(recorder), replay.Info.LengthInMs / 1000.0);
            return new ReplayOutcome(ReplayStatus.Analyzed, match, NewChestClasses: reader.GenericClasses.Order().ToList());
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

    private static FileStream OpenShared(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

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
    // A chest that comes back into view, or whose map cell reloads, gets a new actor and channel
    // and sends its current state straight away. Measured on 99 replays: 625 of 643 duplicate
    // openings arrived within this delay of the channel opening.
    internal const double StaleAfterOpen = 0.5;
    private const float SameSpawnDistance = 150f;

    private readonly Dictionary<uint, uint> _pawnToPlayerState = [];
    private readonly Dictionary<string, ChestState> _chests = [];
    private readonly Dictionary<uint, string> _actorKeys = [];
    private readonly List<(string Class, float X, float Y, string Key)> _spawnedChests = [];
    private readonly Dictionary<uint, double> _channelOpenedAt = [];
    private readonly HashSet<uint> _channelsWithChestState = [];
    private readonly HashSet<string> _genericClasses = [];
    private readonly List<uint> _openers = [];
    private readonly List<ChestOpening> _openingDetails = [];
    private readonly List<double> _openingTimes = [];
    private readonly Dictionary<uint, uint> _pawnChannels = [];
    private readonly Dictionary<uint, Vector3> _pawnPositions = [];
    private readonly HashSet<uint> _lootChannels = [];
    private readonly List<(double Time, Vector3 At)> _loot = [];
    private double _worldTime;

    // Pawn positions and loot only come through at this level of detail.
    public OwnerChestReader() : base(null!, ParseMode.Full)
    {
        _netFieldParser.GroupAlias = ChestClasses.Alias;
    }

    public int CountOpenings(uint recorderPlayerState) => OpeningsBy(recorderPlayerState).Count;

    public List<ChestOpening> OpeningsBy(uint recorderPlayerState) =>
        _openers.Select((pawn, index) => (pawn, index))
            .Where(opening => _pawnToPlayerState.GetValueOrDefault(opening.pawn) == recorderPlayerState)
            .Select(opening => _openingDetails[opening.index])
            .ToList();

    internal int DestroyedWithLootInside => DestroyedWithLoot().Count;

    public double EstimateBroken(uint recorderPlayerState, IReadOnlyList<double> drivingTimes)
    {
        var ownerPawns = _pawnToPlayerState.Where(pair => pair.Value == recorderPlayerState).Select(pair => pair.Key).ToHashSet();
        return BreakEstimator.Estimate(DestroyedWithLoot(), _openingTimes, _loot, ownerPawns, drivingTimes);
    }

    // Many chests vanish by closing their channel without ever replicating bDestroyed. The closing
    // bunch is read first because it can still carry the chest's last state.
    public override bool ReceivedSequencedBunch(DataBunch bunch)
    {
        string? destroyedChest = null;
        if (bunch.bClose)
        {
            if (bunch.CloseReason == ChannelCloseReason.Destroyed
                && Channels[bunch.ChIndex]?.ActorId is uint actor
                && _netGuidCache.GetNetFieldExportGroup(actor) is { } group
                && ChestClasses.IsSearchableChest(group))
            {
                destroyedChest = KeyOf(bunch.ChIndex, actor);
            }

            if (_pawnChannels.Remove(bunch.ChIndex, out var pawn))
            {
                _pawnPositions.Remove(pawn);
            }

            _lootChannels.Remove(bunch.ChIndex);
        }

        var result = base.ReceivedSequencedBunch(bunch);
        if (destroyedChest is string chest)
        {
            MarkDestroyed(StateOf(chest));
        }

        return result;
    }

    protected override void OnChannelOpened(uint channelIndex, NetworkGUID? actor)
    {
        base.OnChannelOpened(channelIndex, actor);
        ChannelOpened(channelIndex, _worldTime);
    }

    internal void ChannelOpened(uint channelIndex, double time)
    {
        _channelOpenedAt[channelIndex] = time;
        _channelsWithChestState.Remove(channelIndex);
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
                var key = KeyOf(channelIndex, actor);
                if (chest is GenericChest)
                {
                    _genericClasses.Add(ClassOfKey(key));
                }

                Track(key, channelIndex, _worldTime, chest);
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

    // Map-placed chests keep their level name across actors; spawned ones (buried, relic, poster)
    // only keep their class and spawn point.
    private string KeyOf(uint channelIndex, uint actor)
    {
        if (_actorKeys.TryGetValue(actor, out var key))
        {
            return key;
        }

        if (_netGuidCache.TryGetPathName(actor, out var path))
        {
            key = path;
        }
        else if (Channels[channelIndex]?.Actor?.Location is { } at)
        {
            var chestClass = Channels[channelIndex]?.ArchetypeId is uint archetype && _netGuidCache.TryGetPathName(archetype, out var archetypePath) ? archetypePath : "";
            key = SpawnedChestKey(chestClass, (float)at.X, (float)at.Y);
        }
        else
        {
            key = $"#{actor}";
        }

        _actorKeys[actor] = key;
        return key;
    }

    // Horizontal only: a buried chest rises out of the ground.
    internal string SpawnedChestKey(string chestClass, float x, float y)
    {
        var known = _spawnedChests.FirstOrDefault(c => c.Class == chestClass && Math.Abs(c.X - x) < SameSpawnDistance && Math.Abs(c.Y - y) < SameSpawnDistance).Key;
        if (known is not null)
        {
            return known;
        }

        var key = $"{chestClass}@{x:F0},{y:F0}";
        _spawnedChests.Add((chestClass, x, y, key));
        return key;
    }

    internal int Openings => _openers.Count;

    public IReadOnlyCollection<string> GenericClasses => _genericClasses;

    // Chests refill during a match, so each false-to-true transition is a separate opening. The
    // first state of a new channel is the chest's current state: "searched" at once means already
    // counted, while a chest that refilled out of view sends nothing because unsearched is the default.
    internal void Track(string key, uint channelIndex, double time, SearchableChest export)
    {
        var state = StateOf(key);
        if (export.SearchingPawn is uint pawn and not 0)
        {
            state.LastPawn = pawn;
        }

        if (_channelsWithChestState.Add(channelIndex))
        {
            var stale = time - _channelOpenedAt.GetValueOrDefault(channelIndex, double.NegativeInfinity) < StaleAfterOpen;
            if (export.bAlreadySearched == true && stale)
            {
                if (state.Searched)
                {
                    if (export.bDestroyed == true)
                    {
                        MarkDestroyed(state);
                    }

                    return;
                }
            }
            else
            {
                state.Searched = false;
            }
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
                _openingTimes.Add(time);
                _openingDetails.Add(new ChestOpening(Math.Round(time, 1), ClassOfKey(key), Hash(key)));
            }
        }

        if (export.bDestroyed == true)
        {
            MarkDestroyed(state);
        }
    }

    private static string ClassOfKey(string key) => key.StartsWith('#') ? "" : ChestClasses.ClassOf(key.Split('@')[0]);

    private static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)), 0, 4);

    private List<DestroyedChest> DestroyedWithLoot() => _chests.Values
        .Where(chest => chest.Destroyed is not null && !chest.SearchedWhenDestroyed)
        .Select(chest => chest.Destroyed!.Value)
        .ToList();

    private ChestState StateOf(string key)
    {
        if (!_chests.TryGetValue(key, out var state))
        {
            _chests[key] = state = new ChestState();
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

    private sealed class ChestState
    {
        public uint? LastPawn;
        public bool Searched;
        public bool SearchedWhenDestroyed;
        public DestroyedChest? Destroyed;
    }
}
