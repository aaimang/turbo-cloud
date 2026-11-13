using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Hotel;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Testing;

/// <summary>A wired event a plugin fired in a room of a <see cref="FakeHotel"/>.</summary>
public sealed record FakeWiredFire(RoomId Room, IWiredEvent Event, PlayerId? Triggerer);

/// <summary>
/// The wired events of a <see cref="FakeHotel"/>: what the plugin fired, in which room and for
/// whom. Like the hotel's, an event reaches only a room someone is in, and the triggerer counts
/// only when they are in it. No stack runs: a plugin's boxes are tested in turbo-cloud's room
/// harness, or on a hotel.
/// </summary>
public sealed class FakeWired : IHotelWired
{
    private readonly FakeHotel _hotel;
    private readonly List<FakeWiredFire> _fired = [];

    internal FakeWired(FakeHotel hotel) => _hotel = hotel;

    public IReadOnlyList<FakeWiredFire> Fired => _fired;

    public Task<bool> FireAsync<TEvent>(
        RoomId room,
        TEvent evt,
        PlayerId? triggerer,
        CancellationToken ct
    )
        where TEvent : IWiredEvent
    {
        if (!_hotel.Rooms.IsPopulated(room))
            return Task.FromResult(false);

        _fired.Add(
            new FakeWiredFire(
                room,
                evt,
                triggerer is { } player && _hotel.Rooms.IsIn(room, player) ? player : null
            )
        );

        return Task.FromResult(true);
    }

    public Task<bool> FireForPlayerAsync<TEvent>(PlayerId player, TEvent evt, CancellationToken ct)
        where TEvent : IWiredEvent =>
        _hotel.Rooms.RoomOf(player) is { } room
            ? FireAsync(room, evt, player, ct)
            : Task.FromResult(false);

    /// <summary>The rooms whose trace a plugin turned on, and who turned it on.</summary>
    public IReadOnlyDictionary<RoomId, PlayerId> Tracing => _tracing;

    private readonly Dictionary<RoomId, PlayerId> _tracing = [];

    /// <summary>
    /// Records that the trace is on or off; like the hotel's, only in a room someone is in. Who
    /// may change a room's wired is not modelled.
    /// </summary>
    public Task<bool> SetTraceAsync(RoomId room, PlayerId by, bool enabled, CancellationToken ct)
    {
        if (!_hotel.Rooms.IsPopulated(room))
            return Task.FromResult(false);

        if (enabled)
            _tracing[room] = by;
        else
            _tracing.Remove(room);

        return Task.FromResult(true);
    }

    /// <summary>
    /// No stack runs here, so a traced room has no steps: the trace is on or off and empty; null
    /// in a room no one is in.
    /// </summary>
    public Task<WiredTraceSnapshot?> GetTraceAsync(
        RoomId room,
        PlayerId by,
        long afterId,
        CancellationToken ct
    ) =>
        Task.FromResult<WiredTraceSnapshot?>(
            _hotel.Rooms.IsPopulated(room)
                ? new WiredTraceSnapshot
                {
                    Enabled = _tracing.ContainsKey(room),
                    Entries = [],
                    LastId = 0,
                }
                : null
        );

    private readonly HashSet<RoomId> _paused = [];

    /// <summary>Whether a test paused this room's wired.</summary>
    public bool IsPaused(RoomId room) => _paused.Contains(room);

    /// <summary>Records the pause; true while someone is in the room.</summary>
    public Task<bool> SetPausedAsync(RoomId room, PlayerId by, bool paused, CancellationToken ct)
    {
        if (!_hotel.Rooms.IsPopulated(room))
            return Task.FromResult(false);

        if (paused)
            _paused.Add(room);
        else
            _paused.Remove(room);

        return Task.FromResult(true);
    }

    /// <summary>Steps nothing: this fake holds no effects.</summary>
    public Task<WiredPauseSnapshot?> StepAsync(
        RoomId room,
        PlayerId by,
        int count,
        CancellationToken ct
    ) => GetPauseAsync(room, by, ct);

    /// <summary>The recorded pause with no held effects; null in a room no one is in.</summary>
    public Task<WiredPauseSnapshot?> GetPauseAsync(RoomId room, PlayerId by, CancellationToken ct) =>
        Task.FromResult<WiredPauseSnapshot?>(
            _hotel.Rooms.IsPopulated(room)
                ? new WiredPauseSnapshot { Paused = _paused.Contains(room), Held = [] }
                : null
        );

    private readonly Dictionary<RoomId, Dictionary<int, WiredBoxSettingsSnapshot>> _boxes = [];

    /// <summary>Stands a wired box with these settings in the room, as a test's starting point.</summary>
    public void AddBox(RoomId room, WiredBoxSettingsSnapshot box)
    {
        if (!_boxes.TryGetValue(room, out var boxes))
            _boxes[room] = boxes = [];

        boxes[box.ItemId] = box;
    }

    /// <summary>The boxes of a room as the fake holds them, by item id.</summary>
    public IReadOnlyDictionary<int, WiredBoxSettingsSnapshot> BoxesOf(RoomId room) =>
        _boxes.TryGetValue(room, out var boxes)
            ? boxes
            : new Dictionary<int, WiredBoxSettingsSnapshot>();

    /// <summary>The boxes added or saved in the room; who may read them is not modelled.</summary>
    public Task<ImmutableArray<WiredBoxSettingsSnapshot>?> ReadBoxesAsync(
        RoomId room,
        PlayerId by,
        CancellationToken ct
    ) =>
        Task.FromResult<ImmutableArray<WiredBoxSettingsSnapshot>?>(
            _hotel.Rooms.IsPopulated(room) ? [.. BoxesOf(room).Values.OrderBy(x => x.ItemId)] : null
        );

    /// <summary>
    /// Stores the settings over a box the room holds, keeping what it is (kind, code, logic); a
    /// box it does not hold is the server error. No box's own checks run here: those are the
    /// hotel's, tested in turbo-cloud.
    /// </summary>
    public Task<WiredSaveResult?> SaveBoxAsync(
        RoomId room,
        PlayerId by,
        WiredBoxSettingsSnapshot settings,
        CancellationToken ct
    )
    {
        if (!_hotel.Rooms.IsPopulated(room))
            return Task.FromResult<WiredSaveResult?>(null);

        if (!_boxes.TryGetValue(room, out var boxes))
            _boxes[room] = boxes = [];

        if (!boxes.TryGetValue(settings.ItemId, out var box))
        {
            // A furni standing in the room whose definition is named as a wired box (wf_...) takes
            // its first save as a box of that name; anything else is no box.
            if (
                _hotel.Rooms.Find(room)?.Furni.FirstOrDefault(x => x.ItemId == settings.ItemId)
                    is not { } furni
                || _hotel.Inventory.Furniture(furni.DefinitionId)?.Name is not { } name
                || !name.StartsWith("wf_", StringComparison.Ordinal)
            )
                return Task.FromResult<WiredSaveResult?>(WiredSaveResult.ServerError);

            box = settings with { LogicName = name };
        }

        boxes[settings.ItemId] = settings with
        {
            WiredType = box.WiredType,
            WiredCode = box.WiredCode,
            LogicName = box.LogicName,
        };

        return Task.FromResult<WiredSaveResult?>(WiredSaveResult.Saved);
    }

    /// <summary>
    /// The variables the room's variable boxes (<c>wf_var_...</c>) make: each named by its text,
    /// with the id a hotel gives a box's variable (<c>WiredVariableId.OfBox</c>). No built-in
    /// variables are listed, and the room lists a new one at once.
    /// </summary>
    public Task<ImmutableArray<HotelWiredVariable>?> ReadVariablesAsync(
        RoomId room,
        PlayerId by,
        CancellationToken ct
    ) =>
        Task.FromResult<ImmutableArray<HotelWiredVariable>?>(
            _hotel.Rooms.IsPopulated(room)
                ?
                [
                    .. BoxesOf(room)
                        .Values.Where(x =>
                            x.LogicName.StartsWith("wf_var_", StringComparison.Ordinal)
                            && x.StringParam.Length > 0
                        )
                        .Select(x => new HotelWiredVariable(
                            WiredVariableId.OfBox(x.ItemId).ToString(),
                            x.StringParam,
                            WiredVariableType.Created
                        )),
                ]
                : null
        );

    /// <summary>What <see cref="UsageAsync"/> answers: set the rooms a test's staff page shows.</summary>
    public List<WiredRoomUsageSnapshot> Usage { get; } = [];

    public Task<ImmutableArray<WiredRoomUsageSnapshot>> UsageAsync(CancellationToken ct) =>
        Task.FromResult<ImmutableArray<WiredRoomUsageSnapshot>>([.. Usage]);

    /// <summary>The last <typeparamref name="TEvent"/> fired in <paramref name="room"/>.</summary>
    public TEvent AssertFired<TEvent>(RoomId room, Func<TEvent, bool>? match = null)
        where TEvent : IWiredEvent =>
        _fired
            .Where(x => x.Room == room)
            .Select(x => x.Event)
            .OfType<TEvent>()
            .LastOrDefault(x => match?.Invoke(x) ?? true)
        ?? throw new HotelAssertionException(
            $"Expected a {typeof(TEvent).Name} fired in room {room}; it was fired {FakeHotelText.List(_fired.Where(x => x.Room == room).Select(x => x.Event.GetType().Name))}."
        );

    public void AssertNothingFired()
    {
        if (_fired.Count > 0)
            throw new HotelAssertionException(
                $"Expected no wired event fired; {FakeHotelText.List(_fired.Select(x => $"{x.Event.GetType().Name} in room {x.Room}"))} was."
            );
    }
}
