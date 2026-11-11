using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Furniture;

/// <summary>Where one floor furni stood and what state it was in when a room's layout was captured.</summary>
[GenerateSerializer, Immutable]
public sealed record RoomLayoutItemSnapshot
{
    [Id(0)]
    public required int ItemId { get; init; }

    [Id(1)]
    public required int X { get; init; }

    [Id(2)]
    public required int Y { get; init; }

    /// <summary>The height in hundredths of a tile (<c>Altitude.ToInt</c>).</summary>
    [Id(3)]
    public required int Z { get; init; }

    [Id(4)]
    public required int Rotation { get; init; }

    [Id(5)]
    public required int State { get; init; }
}
