using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

/// <summary>
/// A room's wired trace: whether it is being recorded, and the steps after the one a reader last
/// read, oldest first. The room keeps the latest <c>WiredConfig.MaxTraceEntries</c> steps.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredTraceSnapshot
{
    [Id(0)]
    public required bool Enabled { get; init; }

    [Id(1)]
    public required ImmutableArray<WiredTraceEntrySnapshot> Entries { get; init; }

    /// <summary>The id of the last step the room recorded; a reader asks for the steps after it next.</summary>
    [Id(2)]
    public required long LastId { get; init; }

    /// <summary>The room's wired time zone, which each step's <c>AtText</c> is written in.</summary>
    [Id(3)]
    public string TimeZone { get; init; } = string.Empty;
}
