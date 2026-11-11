using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Furniture;

/// <summary>
/// A room's floor furni as they stood when it was captured: each one's tile, height, rotation
/// and state. Restoring it puts the furni still in the room back the same way.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record RoomLayoutSnapshot
{
    [Id(0)]
    public required ImmutableArray<RoomLayoutItemSnapshot> Items { get; init; }
}
