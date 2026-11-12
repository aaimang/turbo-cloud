using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

/// <summary>
/// A loaded room's wired load as staff see it across the hotel: the monitor's figures
/// (<see cref="Stats"/>) and how much the room's wired has run since it loaded.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredRoomUsageSnapshot
{
    [Id(0)]
    public required int RoomId { get; init; }

    [Id(1)]
    public required WiredRoomStatsSnapshot Stats { get; init; }

    /// <summary>The room's wired boxes (triggers, effects, conditions, selectors, add-ons, variables).</summary>
    [Id(2)]
    public required int WiredBoxes { get; init; }

    /// <summary>The stacks the boxes make.</summary>
    [Id(3)]
    public required int Stacks { get; init; }

    /// <summary>Wired executions since the room loaded.</summary>
    [Id(4)]
    public required long Executions { get; init; }

    /// <summary>Server time the executions took since the room loaded, in milliseconds.</summary>
    [Id(5)]
    public required long ExecutionMsTotal { get; init; }

    /// <summary>The longest single execution since the room loaded, in milliseconds.</summary>
    [Id(6)]
    public required long ExecutionMsMax { get; init; }

    /// <summary>Wired errors counted since the room loaded (the monitor's error list, summed).</summary>
    [Id(7)]
    public required int Errors { get; init; }

    /// <summary>Whether the room's wired trace is on.</summary>
    [Id(8)]
    public required bool TraceOn { get; init; }
}
