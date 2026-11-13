using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Contracts.Hotel;

/// <summary>
/// A wired variable of a room: its id as boxes name it, its name, and its kind (made by a variable
/// box, built in, derived by an add-on, or smart).
/// </summary>
public sealed record HotelWiredVariable(string Id, string Name, WiredVariableType Type);

/// <summary>
/// Plugin events for the plugin's own wired triggers (<see cref="WiredTrigger{TEvent}"/>,
/// <c>docs/plugin-wired.md</c>). An event is queued for the room's next wired tick, where each
/// trigger that hears it runs its stack under the room's usual limits. A room no one is in is not
/// loaded for it.
/// </summary>
public interface IHotelWired
{
    /// <summary>
    /// Fires <paramref name="evt"/> in <paramref name="room"/>, with <paramref name="triggerer"/> as
    /// the triggering user when they are in it; false when no one is in the room.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> FireAsync<TEvent>(RoomId room, TEvent evt, PlayerId? triggerer, CancellationToken ct)
        where TEvent : IWiredEvent;

    /// <summary>
    /// Fires <paramref name="evt"/> in the room <paramref name="player"/> is in, with them as the
    /// triggering user; false when they are in no room.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> FireForPlayerAsync<TEvent>(PlayerId player, TEvent evt, CancellationToken ct)
        where TEvent : IWiredEvent;

    /// <summary>
    /// Turns the wired trace of <paramref name="room"/> on or off, as <paramref name="by"/>, who
    /// must be allowed to change the room's wired. While it is on, every stack run, each
    /// condition it asks and each effect it chooses is recorded, and each run writes a line to the
    /// room's log (source System). False when no one is in the room or <paramref name="by"/> may
    /// not; the trace lasts while the room stays loaded.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> SetTraceAsync(RoomId room, PlayerId by, bool enabled, CancellationToken ct);

    /// <summary>
    /// The trace steps of <paramref name="room"/> after <paramref name="afterId"/> (0 for all it
    /// keeps), oldest first, as <paramref name="by"/> may read them; null when no one is in the
    /// room or <paramref name="by"/> may not read its wired.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<WiredTraceSnapshot?> GetTraceAsync(
        RoomId room,
        PlayerId by,
        long afterId,
        CancellationToken ct
    );

    /// <summary>
    /// Pauses or resumes the wired of <paramref name="room"/>, as <paramref name="by"/>, who must
    /// be allowed to change the room's wired. Paused, triggers still fire and conditions are asked,
    /// but each effect waits to be stepped; resuming runs what waits, in order. False when no one
    /// is in the room or <paramref name="by"/> may not.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> SetPausedAsync(RoomId room, PlayerId by, bool paused, CancellationToken ct);

    /// <summary>
    /// Runs the next <paramref name="count"/> held effects of a paused room, one at a time, as
    /// <paramref name="by"/>; the pause as it is after. Null when no one is in the room or
    /// <paramref name="by"/> may not change its wired.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<WiredPauseSnapshot?> StepAsync(RoomId room, PlayerId by, int count, CancellationToken ct);

    /// <summary>
    /// Whether <paramref name="room"/> is paused and the effects it holds, as <paramref name="by"/>
    /// may read them; null when no one is in the room or <paramref name="by"/> may not read its wired.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<WiredPauseSnapshot?> GetPauseAsync(RoomId room, PlayerId by, CancellationToken ct);

    /// <summary>
    /// Every wired box of <paramref name="room"/> with its settings as stored, as
    /// <paramref name="by"/> may read them; null when no one is in the room or
    /// <paramref name="by"/> may not read its wired. What a room's wired is copied out as.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<ImmutableArray<WiredBoxSettingsSnapshot>?> ReadBoxesAsync(
        RoomId room,
        PlayerId by,
        CancellationToken ct
    );

    /// <summary>
    /// Saves <paramref name="settings"/> onto the box of <paramref name="room"/> it names, as
    /// <paramref name="by"/> saving it from the box's editor: the same permission, the same checks,
    /// the same refusals (<see cref="WiredSaveResult"/>), and the stack is rebuilt. Picks and
    /// variable ids are the room's own: map them before saving. Null when no one is in the room.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<WiredSaveResult?> SaveBoxAsync(
        RoomId room,
        PlayerId by,
        WiredBoxSettingsSnapshot settings,
        CancellationToken ct
    );

    /// <summary>
    /// The variables of <paramref name="room"/> as <paramref name="by"/> may read them: every one
    /// the room knows, by the id boxes name it with. A variable a box was just saved to make is
    /// listed from the room's next wired tick. Null when no one is in the room or
    /// <paramref name="by"/> may not read its wired.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<ImmutableArray<HotelWiredVariable>?> ReadVariablesAsync(
        RoomId room,
        PlayerId by,
        CancellationToken ct
    );

    /// <summary>
    /// The wired load of every loaded room: the monitor's figures, its wired boxes and stacks, and
    /// its executions, their server time and errors since it loaded. For staff tools; a room that
    /// cannot be read (unloading as it is asked) is left out.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<ImmutableArray<WiredRoomUsageSnapshot>> UsageAsync(CancellationToken ct);
}
