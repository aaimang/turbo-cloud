using System.Collections.Immutable;
using FluentAssertions;
using Orleans;
using Turbo.Contracts.Hotel;
using Turbo.Main.Hotel;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Hotel;

/// <summary>
/// <c>IHotelInventory.FurnitureOfAsync</c> and <c>GrantFurnitureItemAsync</c>: a player's furni by
/// item id, rentals left out, and a grant that answers the item it made (what a G-Presets import
/// places and points its wired boxes at).
/// </summary>
public sealed class HotelInventoryFurnitureTests
{
    private const int PLAYER = 5;
    private const int CHAIR = 30;

    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public HotelInventoryFurnitureTests()
    {
        _fakes.Handlers[nameof(IFurnitureDefinitionProvider.TryGetDefinition)] = call =>
            (int)call.Args[0]! == CHAIR ? Definition(CHAIR) : null;
    }

    [Fact]
    public async Task A_player_s_furni_are_listed_by_item_id_without_rentals()
    {
        _fakes.Handlers[nameof(IInventoryGrain.GetAllItemSnapshotsAsync)] = _ =>
            Task.FromResult<ImmutableArray<FurnitureItemSnapshot>>([
                Item(12, CHAIR),
                Item(9, 31),
                Item(10, CHAIR, secondsToExpiration: 3600),
            ]);

        var furni = await Inventory().FurnitureOfAsync(PLAYER, Ct);

        furni.Should().Equal(new HotelOwnedFurni(9, 31), new HotelOwnedFurni(12, CHAIR));
    }

    [Fact]
    public async Task A_grant_answers_the_item_it_made()
    {
        _fakes.Handlers[nameof(IInventoryGrain.GrantFurnitureAsync)] = _ =>
            Task.FromResult<FurnitureItemSnapshot?>(Item(77, CHAIR));

        (await Inventory().GrantFurnitureItemAsync(PLAYER, CHAIR, "test.grant", Ct))
            .Should()
            .Be(77);
    }

    [Fact]
    public async Task A_grant_of_a_definition_the_hotel_lacks_makes_nothing()
    {
        (await Inventory().GrantFurnitureItemAsync(PLAYER, 999, "test.grant", Ct))
            .Should()
            .BeNull();
        _fakes.Log.Of(nameof(IInventoryGrain.GrantFurnitureAsync)).Should().BeEmpty();
    }

    private HostHotelInventory Inventory() =>
        new(_fakes.Create<IGrainFactory>(), _fakes.Create<IFurnitureDefinitionProvider>());

    private static FurnitureDefinitionSnapshot Definition(int id) =>
        new()
        {
            Id = id,
            SpriteId = id,
            Name = $"furni_{id}",
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            LogicName = "default_floor",
            TotalStates = 1,
            Width = 1,
            Length = 1,
            StackHeight = 1,
            CanStack = true,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
            UsagePolicy = FurnitureUsageType.Everybody,
            ExtraData = null,
            CustomParams = null,
        };

    private static FurnitureItemSnapshot Item(
        int itemId,
        int definitionId,
        int secondsToExpiration = -1
    ) =>
        new()
        {
            ItemId = RoomObjectId.Parse(itemId),
            SpriteId = definitionId,
            OwnerId = PLAYER,
            OwnerName = "player",
            Definition = Definition(definitionId),
            StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "0" },
            ExtraData = string.Empty,
            SecondsToExpiration = secondsToExpiration,
            HasRentPeriodStarted = false,
            RoomId = 0,
        };
}
