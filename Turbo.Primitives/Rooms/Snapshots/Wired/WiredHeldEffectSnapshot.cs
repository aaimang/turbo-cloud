using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

/// <summary>An effect a paused room holds back: the next one of a stack's chosen effects.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredHeldEffectSnapshot
{
    /// <summary>The stack, by the tile index its boxes stand on.</summary>
    [Id(0)]
    public required int StackId { get; init; }

    /// <summary>The effect's furni id.</summary>
    [Id(1)]
    public required int BoxId { get; init; }

    [Id(2)]
    public required string BoxLogic { get; init; }

    [Id(3)]
    public required int BoxCode { get; init; }

    /// <summary>How many effects of this chain are still to run, this one included.</summary>
    [Id(4)]
    public required int Remaining { get; init; }

    /// <summary>The trace run it belongs to; 0 when the run was not traced.</summary>
    [Id(5)]
    public long RunId { get; init; }
}
