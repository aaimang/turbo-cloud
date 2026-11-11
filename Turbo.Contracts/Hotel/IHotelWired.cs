using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Contracts.Hotel;

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
}
