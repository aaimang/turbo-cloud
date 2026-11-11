using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Contracts.Hotel;
using Turbo.Database.Context;
using Turbo.Main.Hotel;
using Turbo.Primitives.Action;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Primitives.Rooms.Watching;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Hotel;

/// <summary>
/// <c>IHotelRooms.RestoreLayoutAsync</c> with picked-up furni: they are placed as the player would
/// place them (the room service's placement, which asks the hotel's placement policies), lowest
/// first, and only when asked to; the room grain then sets every furni as captured. The room's
/// own part is <c>RoomFloorLayoutTests</c>.
/// </summary>
public sealed class HotelRoomLayoutRestoreTests
{
    private const int PLAYER = 5;

    private readonly Fakes _fakes = new();
    private readonly RoomId _room = RoomId.Parse(9);
    private readonly List<RoomLayoutSnapshot?> _captures = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Saved: a block (1), a stack of two on (3, 3) (2 at 0, 3 at 0.5) and a lamp (4).
    private static readonly RoomLayoutSnapshot SAVED = Layout(
        Item(1, 1, 1, 0),
        Item(3, 3, 3, 50, rotation: 2),
        Item(2, 3, 3, 0),
        Item(4, 5, 5, 0)
    );

    public HotelRoomLayoutRestoreTests()
    {
        _fakes.Handlers[nameof(IRoomDirectoryGrain.GetRoomPopulationAsync)] = _ =>
            Task.FromResult(1);
        _fakes.Handlers[nameof(IRoomGrain.CaptureFloorLayoutAsync)] = _ =>
        {
            var next = _captures[0];

            if (_captures.Count > 1)
                _captures.RemoveAt(0);

            return Task.FromResult(next);
        };
        _fakes.Handlers[nameof(IRoomGrain.RestoreFloorLayoutAsync)] = _ => Task.FromResult<int?>(1);
    }

    [Fact]
    public async Task Picked_up_furni_are_placed_as_the_player_would_lowest_first()
    {
        // Before: only the block and the lamp are in the room; after: the stack is back too.
        _captures.Add(Layout(Item(1, 1, 1, 0), Item(4, 5, 5, 0)));
        _captures.Add(SAVED);

        var result = await Rooms().RestoreLayoutAsync(_room, PLAYER, SAVED, true, Ct);

        result.Should().Be(new RoomLayoutRestore(1, 2));
        Placed().Should().Equal((2, 3, 3, Rotation.North), (3, 3, 3, (Rotation)2));
        _fakes
            .Log.Of(nameof(IRoomService.PlaceFloorItemInRoomAsync))
            .Select(x => (ActionContext)x.Args[0]!)
            .Should()
            .OnlyContain(x =>
                x.PlayerId == PLAYER && x.RoomId == _room && x.Origin == ActionOrigin.Player
            );
        _fakes.Log.Of(nameof(IRoomGrain.RestoreFloorLayoutAsync)).Should().ContainSingle();
    }

    [Fact]
    public async Task A_furni_the_placement_refused_is_not_counted_as_put_back()
    {
        _captures.Add(Layout(Item(1, 1, 1, 0), Item(4, 5, 5, 0)));
        _captures.Add(Layout(Item(1, 1, 1, 0), Item(4, 5, 5, 0), Item(2, 3, 3, 0)));

        var result = await Rooms().RestoreLayoutAsync(_room, PLAYER, SAVED, true, Ct);

        result.Should().Be(new RoomLayoutRestore(1, 1));
    }

    [Fact]
    public async Task Without_being_asked_no_picked_up_furni_is_placed()
    {
        _captures.Add(Layout(Item(1, 1, 1, 0)));

        var result = await Rooms().RestoreLayoutAsync(_room, PLAYER, SAVED, false, Ct);

        result.Should().Be(new RoomLayoutRestore(1, 0));
        Placed().Should().BeEmpty();
    }

    [Fact]
    public async Task A_player_who_may_not_change_the_wired_places_nothing()
    {
        _captures.Add(null);

        (await Rooms().RestoreLayoutAsync(_room, PLAYER, SAVED, true, Ct)).Should().BeNull();
        Placed().Should().BeEmpty();
        _fakes.Log.Of(nameof(IRoomGrain.RestoreFloorLayoutAsync)).Should().BeEmpty();
    }

    private HostHotelRooms Rooms() =>
        new(
            _fakes.Create<IDbContextFactory<TurboDbContext>>(),
            _fakes.Create<IGrainFactory>(),
            _fakes.Create<INavigatorService>(),
            _fakes.Create<IPluginRoomLeases>(),
            _fakes.Create<IRoomService>()
        );

    private List<(int ItemId, int X, int Y, Rotation Rotation)> Placed() =>
        [
            .. _fakes
                .Log.Of(nameof(IRoomService.PlaceFloorItemInRoomAsync))
                .Select(x =>
                    (
                        ((RoomObjectId)x.Args[1]!).Value,
                        (int)x.Args[2]!,
                        (int)x.Args[3]!,
                        (Rotation)x.Args[4]!
                    )
                ),
        ];

    private static RoomLayoutSnapshot Layout(params RoomLayoutItemSnapshot[] items) =>
        new() { Items = [.. items] };

    private static RoomLayoutItemSnapshot Item(int id, int x, int y, int z, int rotation = 0) =>
        new()
        {
            ItemId = id,
            X = x,
            Y = y,
            Z = z,
            Rotation = rotation,
            State = 0,
        };
}
