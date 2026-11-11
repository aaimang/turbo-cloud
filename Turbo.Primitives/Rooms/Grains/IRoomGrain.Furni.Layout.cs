using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    /// <summary>
    /// The room's floor furni as they stand now (tile, height, rotation, state), or null when the
    /// caller may not change the room's wired.
    /// </summary>
    public Task<RoomLayoutSnapshot?> CaptureFloorLayoutAsync(
        ActionContext ctx,
        CancellationToken ct
    );

    /// <summary>
    /// Puts the floor furni of <paramref name="layout"/> that are still in the room back where and
    /// how they were captured, lowest first, and answers how many it changed; null when the caller
    /// may not change the room's wired. A furni no longer in the room is skipped, and one added
    /// since is left where it is.
    /// </summary>
    public Task<int?> RestoreFloorLayoutAsync(
        ActionContext ctx,
        RoomLayoutSnapshot layout,
        CancellationToken ct
    );
}
