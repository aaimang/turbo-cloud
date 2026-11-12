using FluentAssertions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Main.Hotel;
using Turbo.Players.Configuration;
using Turbo.Primitives.Messages.Outgoing.Avatar;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Wardrobe;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// The client asks for its wardrobe once and keeps it. An outfit a plugin saves is sent to the
/// player with the whole wardrobe, so it shows without logging in again; a refused save sends
/// nothing.
/// </summary>
public sealed class HotelOutfitSaveTests : IDisposable
{
    private const int PLAYER = 53;
    private const string LOOK = "hd-180-1.ch-3030-1408";

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    private HostHotelPlayers Players(bool saved)
    {
        _fakes.Handlers["SaveOutfitAsync"] = _ => Task.FromResult(saved);
        _fakes.Handlers["GetOutfitsAsync"] = _ =>
            Task.FromResult(
                new List<OutfitDataSnapshot>
                {
                    new()
                    {
                        SlotId = 1,
                        Figure = LOOK,
                        Gender = AvatarGenderType.Male,
                    },
                }
            );

        return new HostHotelPlayers(
            _db,
            _fakes.Create<IGrainFactory>(),
            _fakes.Create<ISessionGateway>(),
            _fakes.Create<ICurrencyTypeProvider>(),
            Options.Create(new PlayerConfig())
        );
    }

    private IEnumerable<WardrobeMessageComposer> WardrobesSent() =>
        _fakes
            .Log.Of("TrySendComposerAsync")
            .Select(x => x.Args[0])
            .OfType<WardrobeMessageComposer>();

    [Fact]
    public async Task A_saved_outfit_is_sent_to_the_player_with_their_wardrobe()
    {
        var players = Players(saved: true);

        (await players.SaveOutfitAsync(new PlayerId(PLAYER), 1, LOOK, AvatarGenderType.Male, Ct))
            .Should()
            .BeTrue();

        var sent = WardrobesSent().Should().ContainSingle().Subject;
        sent.State.Should().Be(WardrobeMessageComposer.STATE_LOADED);
        sent.Outfits.Should().ContainSingle(x => x.SlotId == 1 && x.Figure == LOOK);
    }

    [Fact]
    public async Task A_refused_save_sends_nothing()
    {
        var players = Players(saved: false);

        (await players.SaveOutfitAsync(new PlayerId(PLAYER), 1, LOOK, AvatarGenderType.Male, Ct))
            .Should()
            .BeFalse();

        WardrobesSent().Should().BeEmpty();
    }
}
