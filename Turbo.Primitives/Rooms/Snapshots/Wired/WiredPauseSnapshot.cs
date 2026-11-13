using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

/// <summary>
/// A room's wired pause: whether it is paused and the effects it holds back, in the order a step
/// will run them. While it is paused the room still fires triggers and asks conditions, but each
/// effect waits to be stepped.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredPauseSnapshot
{
    [Id(0)]
    public required bool Paused { get; init; }

    /// <summary>The effects waiting, the next to run first.</summary>
    [Id(1)]
    public required ImmutableArray<WiredHeldEffectSnapshot> Held { get; init; }

    /// <summary>How many effects the last step ran (0 when nothing was stepped).</summary>
    [Id(2)]
    public int Stepped { get; init; }
}
