using FortniteReplayReader.Models.NetFieldExports;
using Unreal.Core.Attributes;
using Unreal.Core.Contracts;
using Unreal.Core.Models.Enums;

namespace ChestCounter;

[NetFieldExportGroup("/Script/FortniteGame.FortPlayerPawn:MulticastUpdateVehicleInputStateReliable", ParseMode.Normal)]
public sealed class VehicleInput : INetFieldExportGroup
{
    [NetFieldExport("bIsBraking", RepLayoutCmdType.PropertyBool)]
    public bool? bIsBraking { get; set; }

    [NetFieldExport("bIsHonking", RepLayoutCmdType.PropertyBool)]
    public bool? bIsHonking { get; set; }

    [NetFieldExport("bIsSprinting", RepLayoutCmdType.PropertyBool)]
    public bool? bIsSprinting { get; set; }

    [NetFieldExport("bIsReversing", RepLayoutCmdType.PropertyBool)]
    public bool? bIsReversing { get; set; }

    [NetFieldExport("bUsingGamepad", RepLayoutCmdType.PropertyBool)]
    public bool? bUsingGamepad { get; set; }
}

[NetFieldExportClassNetCache("PlayerPawn_Athena_C_ClassNetCache", minimalParseMode: ParseMode.Normal)]
public sealed class PawnVehicleInputCache
{
    [NetFieldExportRPC("MulticastUpdateVehicleInputStateReliable", "/Script/FortniteGame.FortPlayerPawn:MulticastUpdateVehicleInputStateReliable", isFunction: true)]
    public VehicleInput? Input { get; set; }
}

// Runs as a separate pass: at the level of detail positions need, the library's own pawn
// handler replaces this message and it never arrives.
internal sealed class OwnerDrivingReader : FortniteReplayReader.ReplayReader
{
    private readonly Dictionary<uint, uint> _pawnToPlayerState = [];
    private readonly List<(uint Pawn, double Time)> _inputs = [];
    private double _worldTime;

    public OwnerDrivingReader() : base(null!, ParseMode.Normal)
    {
    }

    public IReadOnlyList<double> ReadOwnerDrivingTimes(Stream stream)
    {
        var recorder = ReadReplay(stream).GameData.RecorderId;
        return _inputs.Where(input => _pawnToPlayerState.GetValueOrDefault(input.Pawn) == recorder).Select(input => input.Time).ToList();
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

        if (exportGroup is PlayerPawn { PlayerState: uint playerState and not 0 })
        {
            _pawnToPlayerState[actor] = playerState;
        }
        else if (exportGroup is VehicleInput)
        {
            _inputs.Add((actor, _worldTime));
        }
    }
}
