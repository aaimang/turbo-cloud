using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

namespace Turbo.Rooms.Grains.Modules;

/// <summary>
/// A room's floor layout captured and put back: a WIRED sandbox resets its room to how it was
/// built after a test moved and switched its furni.
/// </summary>
public sealed partial class RoomFurniModule
{
    /// <summary>Every floor furni's tile, height, rotation and state, by item id, and the floor's levels.</summary>
    public RoomLayoutSnapshot CaptureFloorLayout() =>
        new()
        {
            Items =
            [
                .. Items
                    .OfType<IRoomFloorItem>()
                    .OrderBy(x => x.ObjectId.Value)
                    .Select(x => new RoomLayoutItemSnapshot
                    {
                        ItemId = x.ObjectId.Value,
                        X = x.X,
                        Y = x.Y,
                        Z = x.Z.ToInt(),
                        Rotation = (int)x.Rotation,
                        State = x.Logic.GetState(),
                    }),
            ],
            FloorWidth = MapModule.Width,
            FloorLevels = MapModule.GetFloorLevels(),
        };

    /// <summary>
    /// Puts each furni of <paramref name="layout"/> still in the room back at its tile, height and
    /// rotation, and in its state, lowest first so a stack is rebuilt from the bottom; the moves go
    /// through <see cref="MoveFloorItemAsync"/>, so the logic of each furni hears of them as of any
    /// move and the room sees them. A wired box's state is its flash and is left alone. Answers how
    /// many furni it changed.
    /// </summary>
    public async Task<int> RestoreFloorLayoutAsync(
        ActionContext ctx,
        RoomLayoutSnapshot layout,
        CancellationToken ct
    )
    {
        var changed = 0;

        foreach (var saved in layout.Items.OrderBy(x => x.Z).ThenBy(x => x.ItemId))
        {
            if (!TryGetFloorItem(RoomObjectId.Parse(saved.ItemId), out var item))
                continue;

            if (!MapModule.InBounds(saved.X, saved.Y))
                continue;

            var z = Altitude.FromInt(saved.Z);
            var rotation = (Rotation)saved.Rotation;
            var touched = false;

            if (
                item.X != saved.X
                || item.Y != saved.Y
                || item.Rotation != rotation
                || item.Z.ToInt() != saved.Z
            )
                touched = await MoveFloorItemAsync(
                    ctx,
                    item,
                    MapModule.ToIdx(saved.X, saved.Y),
                    z,
                    rotation,
                    announce: true,
                    ct
                );

            if (
                item.Logic is not FurnitureWiredLogic
                && item.Logic.CanChangeState()
                && item.Logic.GetState() != saved.State
            )
            {
                await item.Logic.SetStateAsync(saved.State);
                touched = true;
            }

            if (touched)
                changed++;
        }

        return changed;
    }
}
