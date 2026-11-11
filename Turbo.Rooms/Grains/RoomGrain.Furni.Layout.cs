using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<RoomLayoutSnapshot?> CaptureFloorLayoutAsync(
        ActionContext ctx,
        CancellationToken ct
    )
    {
        if (!await CanModifyWiredAsync(ctx, ct))
            return null;

        return FurniModule.CaptureFloorLayout();
    }

    public async Task<int?> RestoreFloorLayoutAsync(
        ActionContext ctx,
        RoomLayoutSnapshot layout,
        CancellationToken ct
    )
    {
        if (!await CanModifyWiredAsync(ctx, ct))
            return null;

        return await FurniModule.RestoreFloorLayoutAsync(ctx, layout, ct);
    }
}
