using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Providers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A room's floor layout captured and put back (<c>IHotelRooms.CaptureLayoutAsync</c> /
/// <c>RestoreLayoutAsync</c>, a WIRED sandbox's reset): each furni still in the room goes back to
/// its tile, height, rotation and state; furni gone since are skipped, furni added since stay.
/// </summary>
public sealed class RoomFloorLayoutTests
{
    private const int BLOCK = 21;
    private const int LAMP = 22;
    private const int ON_TOP = 23;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionContext System => ActionContext.CreateForSystem(1);

    private RoomFurniModule Furni => _room.Harness.Module<RoomFurniModule>();

    public RoomFloorLayoutTests()
    {
        _room.AddFloorItem(BLOCK, 1, 1);
        _room.AddFloorItem(LAMP, 2, 2);
        _room.AddFloorItem(ON_TOP, 5, 5);
    }

    [Fact]
    public async Task A_restore_puts_moved_turned_and_switched_furni_back()
    {
        await MoveAsync(ON_TOP, 3, 3, Altitude.FromInt(150), Rotation.North);
        var layout = await CaptureAsync();

        await MoveAsync(BLOCK, 6, 4, null, Rotation.East);
        await MoveAsync(ON_TOP, 0, 7, null, Rotation.South);
        await _room.FloorItem(LAMP).Logic.SetStateAsync(1);

        (await _room.Harness.Room.RestoreFloorLayoutAsync(System, layout, Ct)).Should().Be(3);

        Where(BLOCK).Should().Be((1, 1, 0, Rotation.North));
        Where(ON_TOP).Should().Be((3, 3, 150, Rotation.North));
        _room.FloorItem(LAMP).Logic.GetState().Should().Be(0);
    }

    [Fact]
    public async Task A_restore_of_an_unchanged_room_changes_nothing()
    {
        var layout = await CaptureAsync();

        (await _room.Harness.Room.RestoreFloorLayoutAsync(System, layout, Ct)).Should().Be(0);
    }

    [Fact]
    public async Task Furni_gone_since_are_skipped_and_furni_added_since_stay()
    {
        var layout = await CaptureAsync();

        await MoveAsync(BLOCK, 6, 6, null, Rotation.North);
        _room.AddFloorItem(24, 4, 1);
        var withAGhost = layout with
        {
            Items =
            [
                .. layout.Items,
                new RoomLayoutItemSnapshot
                {
                    ItemId = 99,
                    X = 0,
                    Y = 0,
                    Z = 0,
                    Rotation = 0,
                    State = 0,
                },
            ],
        };

        (await _room.Harness.Room.RestoreFloorLayoutAsync(System, withAGhost, Ct)).Should().Be(1);

        Where(BLOCK).Should().Be((1, 1, 0, Rotation.North));
        Where(24).Should().Be((4, 1, 0, Rotation.North));
    }

    [Fact]
    public async Task The_capture_lists_each_floor_furni_with_its_tile_height_rotation_and_state()
    {
        await MoveAsync(ON_TOP, 3, 3, Altitude.FromInt(150), Rotation.West);
        await _room.FloorItem(LAMP).Logic.SetStateAsync(1);

        var layout = await CaptureAsync();

        layout
            .Items.Select(x => (x.ItemId, x.X, x.Y, x.Z, x.Rotation, x.State))
            .Should()
            .Equal(
                (BLOCK, 1, 1, 0, (int)Rotation.North, 0),
                (LAMP, 2, 2, 0, (int)Rotation.North, 1),
                (ON_TOP, 3, 3, 150, (int)Rotation.West, 0)
            );
    }

    [Fact]
    public async Task A_player_who_may_not_change_the_wired_can_neither_capture_nor_restore()
    {
        var visitor = ActionContext.CreateForPlayer(777, 1);
        var layout = await CaptureAsync();

        await MoveAsync(BLOCK, 6, 6, null, Rotation.North);

        (await _room.Harness.Room.CaptureFloorLayoutAsync(visitor, Ct)).Should().BeNull();
        (await _room.Harness.Room.RestoreFloorLayoutAsync(visitor, layout, Ct)).Should().BeNull();
        Where(BLOCK).Should().Be((6, 6, 0, Rotation.North));
    }

    [Fact]
    public async Task A_capture_carries_the_floor_s_level_on_each_tile()
    {
        var room = new WiredRoom(model: StepModel());
        var layout = await room.Harness.Room.CaptureFloorLayoutAsync(System, Ct);

        layout!.FloorWidth.Should().Be(3);
        layout.FloorLevelAt(0, 0).Should().Be(0);
        layout.FloorLevelAt(2, 0).Should().Be(100);
        layout.FloorLevelAt(2, 1).Should().Be(200);
        layout.FloorLevelAt(0, 1).Should().Be(RoomLayoutSnapshot.NO_FLOOR);
        layout.FloorLevelAt(9, 9).Should().Be(RoomLayoutSnapshot.NO_FLOOR);
        layout.LowestFloorLevel(1, 0, 2, 1).Should().Be(0);
        layout.LowestFloorLevel(2, 0, 2, 1).Should().Be(100);
        layout.LowestFloorLevel(0, 1, 0, 1).Should().Be(0);
    }

    // Three tiles wide, a step up to the east, no floor at the west of the second row: "001" over "x02".
    private static RoomModelSnapshot StepModel()
    {
        var model = RoomModelProvider.CleanModelString("001\rx02");
        var compiled = RoomModelProvider.CompileModelFromString(model);

        return new RoomModelSnapshot
        {
            Id = 1,
            Name = "step",
            Model = model,
            DoorX = 0,
            DoorY = 0,
            DoorRotation = Rotation.East,
            Width = compiled.Width,
            Height = compiled.Height,
            Size = compiled.Width * compiled.Height,
            BaseHeights = compiled.Heights,
            BaseFlags = compiled.Flags,
        };
    }

    private async Task<RoomLayoutSnapshot> CaptureAsync()
    {
        var layout = await _room.Harness.Room.CaptureFloorLayoutAsync(System, Ct);

        layout.Should().NotBeNull();

        return layout!;
    }

    private Task<bool> MoveAsync(int id, int x, int y, Altitude? z, Rotation rotation) =>
        Furni.MoveFloorItemAsync(
            System,
            _room.FloorItem(id),
            _room.Map.ToIdx(x, y),
            z,
            rotation,
            announce: false,
            Ct
        );

    private (int X, int Y, int Z, Rotation Rotation) Where(int id)
    {
        var item = _room.FloorItem(id);

        return (item.X, item.Y, item.Z.ToInt(), item.Rotation);
    }
}
