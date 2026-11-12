using FluentAssertions;
using Orleans;
using Turbo.Database.Entities.Players;
using Turbo.Players.Figures;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// The client's avatar editor takes off a sold piece the player is not told they own. A player
/// who may wear any clothing (<c>figure.any</c>) is told every sold set is theirs, so the editor
/// keeps on what the hotel dresses them in; anyone else is told what they own.
/// </summary>
public sealed class ShownClothingTests : IDisposable
{
    private const int PLAYER = 52;
    private const int OWNED = 3030;
    private const int SOLD = 3409;
    private const int FREE = 180;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    private PlayerClothingService Clothing(bool mayWearAnything)
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER,
                Name = "dresser",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _db.Insert(new PlayerFigureSetEntity { PlayerEntityId = PLAYER, SetId = OWNED });

        _fakes.Handlers["HasAsync"] = call =>
            Task.FromResult(mayWearAnything && (string)call.Args[0]! == PermissionNodes.Figure.ANY);

        var figureData = _fakes.Create<IFigureDataProvider>();
        _fakes.Handlers[nameof(IFigureDataProvider.GetAsync)] = _ => Task.FromResult(Data());

        return new PlayerClothingService(_db, _fakes.Create<IGrainFactory>(), figureData);
    }

    private static FigureData Data() =>
        new(
            new Dictionary<string, FigureSetType>
            {
                ["ha"] = new(
                    "ha",
                    1,
                    false,
                    false,
                    false,
                    false,
                    new Dictionary<int, FigureSet>
                    {
                        [SOLD] = new(SOLD, 'U', 0, true, true, false, true, 1),
                        [OWNED] = new(OWNED, 'U', 0, true, true, false, true, 1),
                        [FREE] = new(FREE, 'U', 0, true, true, true, false, 1),
                    }
                ),
            },
            new Dictionary<int, FigurePalette>()
        );

    [Fact]
    public async Task A_player_who_may_wear_anything_is_told_every_sold_set_is_theirs()
    {
        var clothing = Clothing(mayWearAnything: true);

        (await clothing.GetShownAsync(new PlayerId(PLAYER), Ct))
            .Should()
            .BeEquivalentTo([OWNED, SOLD]);
        (await clothing.GetOwnedAsync(new PlayerId(PLAYER), Ct)).Should().BeEquivalentTo([OWNED]);
    }

    [Fact]
    public async Task Anyone_else_is_told_what_they_own()
    {
        var clothing = Clothing(mayWearAnything: false);

        (await clothing.GetShownAsync(new PlayerId(PLAYER), Ct)).Should().BeEquivalentTo([OWNED]);
    }
}
