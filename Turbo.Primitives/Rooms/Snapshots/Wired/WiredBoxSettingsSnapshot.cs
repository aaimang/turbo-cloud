using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

/// <summary>
/// A wired box's settings as it stores them: what its editor saves, in the fields of the save
/// packet (<c>UpdateWiredMessage</c>), with the definition specifics by name. Read from a room to
/// copy its wired elsewhere, and saved onto a box through the box's own checks, as its editor's
/// save is. On a save, <see cref="ItemId"/> names the box and <see cref="WiredType"/>,
/// <see cref="WiredCode"/> and <see cref="LogicName"/> are not read: the box is what it is.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredBoxSettingsSnapshot
{
    [Id(0)]
    public required int ItemId { get; init; }

    [Id(1)]
    public required WiredType WiredType { get; init; }

    [Id(2)]
    public required int WiredCode { get; init; }

    /// <summary>The furni logic the box is, such as <c>wf_act_toggle_state</c>.</summary>
    [Id(3)]
    public required string LogicName { get; init; }

    [Id(4)]
    public required ImmutableArray<int> IntParams { get; init; }

    [Id(5)]
    public required string StringParam { get; init; }

    /// <summary>The furni it picked, by item id.</summary>
    [Id(6)]
    public required ImmutableArray<int> StuffIds { get; init; }

    /// <summary>The furni of its second slot (move to furni, furni to furni, send signal).</summary>
    [Id(7)]
    public required ImmutableArray<int> StuffIds2 { get; init; }

    [Id(8)]
    public required ImmutableArray<string> VariableIds { get; init; }

    /// <summary>Each furni slot's sources, by the client's source codes.</summary>
    [Id(9)]
    public required ImmutableArray<ImmutableArray<int>> FurniSources { get; init; }

    /// <summary>Each user slot's sources, by the client's source codes.</summary>
    [Id(10)]
    public required ImmutableArray<ImmutableArray<int>> UserSources { get; init; }

    /// <summary>An effect's delay, in half-second pulses; 0 for any other box.</summary>
    [Id(11)]
    public int Delay { get; init; }

    /// <summary>A condition's quantifier code; 0 for any other box.</summary>
    [Id(12)]
    public int Quantifier { get; init; }

    /// <summary>A selector's "filter existing selection" switch.</summary>
    [Id(13)]
    public bool Filter { get; init; }

    /// <summary>A selector's "invert" switch.</summary>
    [Id(14)]
    public bool Invert { get; init; }

    /// <summary>
    /// For a box that keeps a furni snapshot (Match Snapshot and its conditions), the snapshot as
    /// Habbo writes it into the box's text: one "id,state,rotation,x,y,altitude" entry per furni
    /// joined by ";", "N" for each part the box leaves out. Empty for any other box. Read only: a
    /// save takes its snapshot from the picked furni as they stand.
    /// </summary>
    [Id(15)]
    public string SnapshotText { get; init; } = string.Empty;
}
