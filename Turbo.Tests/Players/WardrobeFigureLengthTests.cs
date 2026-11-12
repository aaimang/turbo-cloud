using FluentAssertions;
using Turbo.Database.Entities.Players;
using Turbo.Players.Grains.Wardrobe;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Wardrobe;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// A saved outfit holds any look a player can wear: the wardrobe takes a figure as long as a
/// player's own (279 characters). Habbo looks with many parts run past 100.
/// </summary>
public sealed class WardrobeFigureLengthTests : IDisposable
{
    private const int PLAYER = 51;

    // 163 characters: a habbo.com look with every part and two colours on most of them.
    private const string LONG_LOOK =
        "hr-3163-61-61.hd-180-1370.ch-3030-1408-1408.lg-3023-110-110.sh-3068-1408-110."
        + "ha-3409-110-110.he-3274-1408-1408.ea-3168-110-110.fa-3276-1408-1408.cc-3039-110-110";

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    private async Task<IPlayerWardrobeGrain> WardrobeAsync()
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
        var grain = GrainHarness.Create<PlayerWardrobeGrain>(_fakes, _db, PLAYER);
        await grain.OnActivateAsync(Ct);

        return grain;
    }

    [Fact]
    public async Task A_look_longer_than_100_characters_is_kept_in_the_wardrobe()
    {
        LONG_LOOK.Length.Should().BeGreaterThan(100);
        var wardrobe = await WardrobeAsync();

        (await wardrobe.SaveOutfitAsync(1, LONG_LOOK, AvatarGenderType.Male, Ct)).Should().BeTrue();

        (await wardrobe.GetOutfitsAsync(Ct))
            .Should()
            .ContainSingle(x => x.SlotId == 1 && x.Figure == LONG_LOOK);
    }

    [Fact]
    public async Task A_look_longer_than_a_player_figure_is_refused()
    {
        var wardrobe = await WardrobeAsync();
        var tooLong = string.Concat(Enumerable.Repeat("ha-3409-110-110.", 18));

        tooLong.Length.Should().BeGreaterThan(279);
        (await wardrobe.SaveOutfitAsync(1, tooLong, AvatarGenderType.Male, Ct)).Should().BeFalse();
        (await wardrobe.GetOutfitsAsync(Ct)).Should().BeEmpty();
    }
}
