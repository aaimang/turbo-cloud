using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Contracts.Hotel;
using Turbo.Database.Context;
using Turbo.Main.Hotel;
using Turbo.Primitives.Action;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Primitives.Rooms.Watching;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Hotel;

/// <summary>
/// <c>IHotelRooms.CreateAsync</c>: the navigator's own create (its checks and room limit), then the
/// floor plan saved as the owner would save it in the editor, the door on the plan's first tile.
/// </summary>
public sealed class HotelRoomCreateTests
{
    private const int OWNER = 5;
    private static readonly RoomId NEW_ROOM = RoomId.Parse(40);

    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public HotelRoomCreateTests()
    {
        _fakes.Handlers[nameof(INavigatorService.CreateRoomAsync)] = _ =>
            Task.FromResult<RoomId?>(NEW_ROOM);
        _fakes.Handlers[nameof(IRoomGrain.SaveFloorPlanAsync)] = _ =>
            Task.FromResult<ImmutableArray<PlayerId>?>([]);
    }

    [Fact]
    public async Task A_room_is_made_by_the_navigator_and_takes_the_floor_plan_as_its_owner()
    {
        var made = await Rooms().CreateAsync(OWNER, "Sandbox", "model_a", 25, "xxx\rx00\r000", Ct);

        made.Should().Be(new HotelRoomCreated(NEW_ROOM, true));

        var create = _fakes.Log.Of(nameof(INavigatorService.CreateRoomAsync)).Single();
        create.Args[0].Should().Be((PlayerId)OWNER);
        create.Args[1].Should().Be("Sandbox");
        create.Args[3].Should().Be("model_a");
        create.Args[5].Should().Be(25);

        var save = _fakes.Log.Of(nameof(IRoomGrain.SaveFloorPlanAsync)).Single();
        save.Key.Should().Be(NEW_ROOM.Value);
        ((ActionContext)save.Args[0]!).PlayerId.Should().Be((PlayerId)OWNER);
        save.Args[1].Should().Be("xxx\rx00\r000");
        var door = (FloorPlanPropertiesSnapshot)save.Args[2]!;
        (door.DoorX, door.DoorY).Should().Be((1, 1));
    }

    [Fact]
    public async Task Without_a_floor_plan_the_room_keeps_its_model()
    {
        (await Rooms().CreateAsync(OWNER, "Sandbox", "model_a", 25, null, Ct))
            .Should()
            .Be(new HotelRoomCreated(NEW_ROOM, false));
        _fakes.Log.Of(nameof(IRoomGrain.SaveFloorPlanAsync)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_refused_floor_plan_still_leaves_the_room()
    {
        _fakes.Handlers[nameof(IRoomGrain.SaveFloorPlanAsync)] = _ =>
            Task.FromResult<ImmutableArray<PlayerId>?>(null);

        (await Rooms().CreateAsync(OWNER, "Sandbox", "model_a", 25, "000", Ct))
            .Should()
            .Be(new HotelRoomCreated(NEW_ROOM, false));
    }

    [Fact]
    public async Task A_room_the_navigator_refuses_is_not_made()
    {
        _fakes.Handlers[nameof(INavigatorService.CreateRoomAsync)] = _ =>
            Task.FromResult<RoomId?>(null);

        (await Rooms().CreateAsync(OWNER, "Sandbox", "model_a", 25, "000", Ct)).Should().BeNull();
        _fakes.Log.Of(nameof(IRoomGrain.SaveFloorPlanAsync)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("000", 0, 0)]
    [InlineData("xxx\nxx5", 2, 1)]
    [InlineData("xx\r\nxa", 1, 1)]
    public void The_door_is_the_first_tile_with_a_height(string plan, int x, int y) =>
        HostHotelRooms.FirstTile(plan).Should().Be((x, y));

    [Fact]
    public void A_plan_of_holes_has_no_door() =>
        HostHotelRooms.FirstTile("xx\rxx").Should().BeNull();

    private HostHotelRooms Rooms() =>
        new(
            _fakes.Create<IDbContextFactory<TurboDbContext>>(),
            _fakes.Create<IGrainFactory>(),
            _fakes.Create<INavigatorService>(),
            _fakes.Create<IPluginRoomLeases>(),
            _fakes.Create<IRoomService>()
        );
}
