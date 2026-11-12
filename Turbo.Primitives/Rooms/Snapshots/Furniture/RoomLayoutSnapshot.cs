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

    /// <summary>A tile of <see cref="FloorLevels"/> where the room has no floor.</summary>
    public const int NO_FLOOR = -1;

    /// <summary>The room's width in tiles, the row length of <see cref="FloorLevels"/>; 0 when not read.</summary>
    [Id(1)]
    public int FloorWidth { get; init; }

    /// <summary>
    /// The level of the room's floor on each tile without furni, in hundredths as
    /// <see cref="RoomLayoutItemSnapshot.Z"/> is, row by row (tile <c>y * FloorWidth + x</c>),
    /// <see cref="NO_FLOOR"/> where there is none; empty when not read. What a copy of furni
    /// measures their heights from.
    /// </summary>
    [Id(2)]
    public ImmutableArray<int> FloorLevels { get; init; } = [];

    /// <summary>
    /// The floor's level on a tile, in hundredths; <see cref="NO_FLOOR"/> outside the room, on a
    /// tile with no floor, or when the levels were not read.
    /// </summary>
    public int FloorLevelAt(int x, int y) =>
        FloorWidth > 0
        && x >= 0
        && x < FloorWidth
        && y >= 0
        && y * FloorWidth + x < FloorLevels.Length
            ? FloorLevels[y * FloorWidth + x]
            : NO_FLOOR;

    /// <summary>
    /// The lowest floor level, in hundredths, on the tiles from (<paramref name="x1"/>,
    /// <paramref name="y1"/>) to (<paramref name="x2"/>, <paramref name="y2"/>) that have floor;
    /// 0 when none has. What G-Presets measures a copy's heights from.
    /// </summary>
    public int LowestFloorLevel(int x1, int y1, int x2, int y2)
    {
        var lowest = int.MaxValue;

        for (var y = System.Math.Min(y1, y2); y <= System.Math.Max(y1, y2); y++)
        {
            for (var x = System.Math.Min(x1, x2); x <= System.Math.Max(x1, x2); x++)
            {
                var level = FloorLevelAt(x, y);

                if (level != NO_FLOOR && level < lowest)
                    lowest = level;
            }
        }

        return lowest == int.MaxValue ? 0 : lowest;
    }
}
