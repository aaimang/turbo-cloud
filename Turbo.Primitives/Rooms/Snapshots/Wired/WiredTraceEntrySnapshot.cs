using Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

/// <summary>
/// One step of a room's wired trace: a stack run, a condition it asked or an effect it chose.
/// The steps of one stack run share its <see cref="RunId"/>; a condition is listed only when it
/// was asked, so one after the first that failed is missing because it was never asked.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredTraceEntrySnapshot
{
    /// <summary>Climbs by one per step while the room is loaded; what a reader asks to read after.</summary>
    [Id(0)]
    public required long Id { get; init; }

    /// <summary>The stack run the step belongs to.</summary>
    [Id(1)]
    public required long RunId { get; init; }

    /// <summary>The stack, by the tile index its boxes stand on.</summary>
    [Id(2)]
    public required int StackId { get; init; }

    [Id(3)]
    public required WiredTraceStepType Step { get; init; }

    [Id(4)]
    public required WiredTraceOutcomeType Outcome { get; init; }

    /// <summary>The box the step is about (the trigger for a stack run); 0 for a stack run no trigger started.</summary>
    [Id(5)]
    public required int BoxId { get; init; }

    /// <summary>The box's furni logic, such as <c>wf_trg_click_furni</c>; empty without a box.</summary>
    [Id(6)]
    public required string BoxLogic { get; init; }

    /// <summary>The box's code in the client's table for its kind; -1 without a box.</summary>
    [Id(7)]
    public required int BoxCode { get; init; }

    /// <summary>What else is worth knowing, in a few words (why a run stopped, how long a delay is).</summary>
    [Id(8)]
    public required string Detail { get; init; }

    /// <summary>Room time of the step, in milliseconds.</summary>
    [Id(9)]
    public required long AtMs { get; init; }

    /// <summary>When the step happened, in Unix milliseconds.</summary>
    [Id(10)]
    public long AtUtcMs { get; init; }

    /// <summary>The step's time of day in the room's wired time zone ("17:44:05"), as the room log writes times.</summary>
    [Id(11)]
    public string AtText { get; init; } = string.Empty;
}
