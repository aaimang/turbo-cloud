using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Contracts.Hotel;
using Turbo.Database.Context;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Primitives.Rooms.Watching;

namespace Turbo.Main.Hotel;

/// <summary>
/// <see cref="IHotelRooms"/> over the rooms' table (read only), the navigator's public list, the
/// room directory (who is where, without loading a room) and the room grains. Holds and watches
/// go through <see cref="IPluginRoomLeases"/> as <see cref="Plugin"/>'s: each plugin's container
/// is given its own copy (<see cref="ForPlugin"/>).
/// </summary>
internal sealed class HostHotelRooms(
    IDbContextFactory<TurboDbContext> database,
    IGrainFactory grains,
    INavigatorService navigator,
    IPluginRoomLeases leases,
    IRoomService roomService
) : IHotelRooms
{
    /// <summary>The plugin whose holds and watches these are; empty for the host's own.</summary>
    internal string Plugin { get; private init; } = string.Empty;

    internal HostHotelRooms ForPlugin(string pluginKey) =>
        new(database, grains, navigator, leases, roomService) { Plugin = pluginKey };

    public async Task<HotelRoom?> GetAsync(RoomId room, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var row = await db
            .Rooms.AsNoTracking()
            .Where(x => x.Id == room.Value)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.PlayerEntityId,
                OwnerName = x.PlayerEntity.Name,
            })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (row is null)
            return null;

        var isPublic = (await PublicIdsAsync(ct).ConfigureAwait(false)).Contains(room);

        return new HotelRoom(
            new RoomId(row.Id),
            row.Name,
            row.Description ?? string.Empty,
            new PlayerId(row.PlayerEntityId),
            row.OwnerName,
            isPublic
        );
    }

    public async Task<IReadOnlyCollection<RoomId>> PublicIdsAsync(CancellationToken ct) =>
        [
            .. (await navigator.GetOfficialRoomsAsync(ct).ConfigureAwait(false))
                .Select(x => x.RoomId)
                .Distinct(),
        ];

    public async Task<IReadOnlyList<PlayerId>> PlayersAsync(RoomId room, CancellationToken ct) =>
        await grains.GetRoomDirectoryGrain().GetRoomPlayersAsync(room, ct).ConfigureAwait(false);

    /// <summary>Like a send, a room no one is in is not loaded for it.</summary>
    public async Task<IReadOnlyList<HotelRoomFurni>> FurniAsync(RoomId room, CancellationToken ct)
    {
        if (
            await grains
                .GetRoomDirectoryGrain()
                .GetRoomPopulationAsync(room, ct)
                .ConfigureAwait(false) <= 0
        )
            return [];

        var items = await grains
            .GetRoomGrain(room)
            .GetFloorItemSnapshotsAsync(ct)
            .ConfigureAwait(false);

        return
        [
            .. items.Select(x => new HotelRoomFurni(x.ObjectId.Value, x.DefinitionId, x.X, x.Y)),
        ];
    }

    /// <summary>Like a send, a room no one is in is not loaded for it.</summary>
    public async Task<RoomLayoutSnapshot?> CaptureLayoutAsync(
        RoomId room,
        PlayerId by,
        CancellationToken ct
    ) =>
        await grains.GetRoomDirectoryGrain().GetRoomPopulationAsync(room, ct).ConfigureAwait(false)
        > 0
            ? await grains
                .GetRoomGrain(room)
                .CaptureFloorLayoutAsync(ActionContext.CreateForPlayer(by, room), ct)
                .ConfigureAwait(false)
            : null;

    /// <summary>
    /// Like a send, a room no one is in is not loaded for it. Picked-up furni go back through the
    /// room service's placement, the one a client's place takes, so the hotel's placement
    /// policies are asked; the room grain then sets every furni to its captured height, rotation
    /// and state.
    /// </summary>
    public async Task<RoomLayoutRestore?> RestoreLayoutAsync(
        RoomId room,
        PlayerId by,
        RoomLayoutSnapshot layout,
        bool putBackPickedUp,
        CancellationToken ct
    )
    {
        if (
            await grains
                .GetRoomDirectoryGrain()
                .GetRoomPopulationAsync(room, ct)
                .ConfigureAwait(false) <= 0
        )
            return null;

        var roomGrain = grains.GetRoomGrain(room);
        var ctx = ActionContext.CreateForPlayer(by, room);

        // Asked first: it is the permission check, and it says which furni are in the room.
        if (
            await roomGrain.CaptureFloorLayoutAsync(ctx, ct).ConfigureAwait(false) is not { } before
        )
            return null;

        var putBack = 0;

        if (putBackPickedUp)
        {
            var inRoom = before.Items.Select(x => x.ItemId).ToHashSet();
            var missing = layout
                .Items.Where(x => !inRoom.Contains(x.ItemId))
                .OrderBy(x => x.Z)
                .ThenBy(x => x.ItemId)
                .ToList();

            foreach (var item in missing)
                await roomService
                    .PlaceFloorItemInRoomAsync(
                        ctx,
                        RoomObjectId.Parse(item.ItemId),
                        item.X,
                        item.Y,
                        (Rotation)item.Rotation,
                        ct
                    )
                    .ConfigureAwait(false);

            if (missing.Count > 0)
            {
                var after = await roomGrain.CaptureFloorLayoutAsync(ctx, ct).ConfigureAwait(false);
                var nowIn = after?.Items.Select(x => x.ItemId).ToHashSet() ?? [];

                putBack = missing.Count(x => nowIn.Contains(x.ItemId));
            }
        }

        var changed = await roomGrain
            .RestoreFloorLayoutAsync(ctx, layout, ct)
            .ConfigureAwait(false);

        return changed is { } count ? new RoomLayoutRestore(count, putBack) : null;
    }

    /// <summary>
    /// The navigator's create, then the room grain's floor plan save as the owner. The plan's door
    /// is its first tile with a height, facing south-east; its walls and floor as the editor's
    /// defaults leave them.
    /// </summary>
    public async Task<HotelRoomCreated?> CreateAsync(
        PlayerId owner,
        string name,
        string baseModel,
        int playersMax,
        string? floorPlan,
        CancellationToken ct
    )
    {
        if (
            await navigator
                .CreateRoomAsync(
                    owner,
                    name,
                    string.Empty,
                    baseModel,
                    0,
                    playersMax,
                    RoomTradeModeType.Disabled,
                    ct
                )
                .ConfigureAwait(false)
            is not { } room
        )
            return null;

        if (string.IsNullOrWhiteSpace(floorPlan) || FirstTile(floorPlan) is not { } door)
            return new HotelRoomCreated(room, false);

        var removed = await grains
            .GetRoomGrain(room)
            .SaveFloorPlanAsync(
                ActionContext.CreateForPlayer(owner, room),
                floorPlan,
                new FloorPlanPropertiesSnapshot
                {
                    DoorX = door.X,
                    DoorY = door.Y,
                    DoorRotation = DOOR_ROTATION,
                    WallThickness = 0,
                    FloorThickness = 0,
                    FixedWallsHeight = -1,
                },
                ct
            )
            .ConfigureAwait(false);

        return new HotelRoomCreated(room, removed is not null);
    }

    // The client's default door direction (south-east), as a new room's door faces.
    private const int DOOR_ROTATION = 2;

    /// <summary>The first tile of a heightmap that is not a hole (<c>x</c>), row by row.</summary>
    internal static (int X, int Y)? FirstTile(string floorPlan)
    {
        var rows = floorPlan.Replace("\r\n", "\r").Replace("\n", "\r").Split('\r');

        for (var y = 0; y < rows.Length; y++)
        {
            var x = rows[y].IndexOfAny(TILE_HEIGHTS);

            if (x >= 0)
                return (x, y);
        }

        return null;
    }

    private static readonly char[] TILE_HEIGHTS = "0123456789abcdefghijklmnopqrstuvw".ToCharArray();

    public Task<bool> SendAsync(RoomId room, IPluginMessage message, CancellationToken ct) =>
        SendAsync(room, PluginMessageComposer.Compose(message), ct);

    /// <summary>
    /// The room's own send: one write for everyone in it. A room no one is in is left alone
    /// rather than loaded for it.
    /// </summary>
    public async Task<bool> SendAsync(RoomId room, IComposer composer, CancellationToken ct)
    {
        if (
            await grains
                .GetRoomDirectoryGrain()
                .GetRoomPopulationAsync(room, ct)
                .ConfigureAwait(false) <= 0
        )
            return false;

        await grains.GetRoomGrain(room).SendComposerToRoomAsync(composer, ct).ConfigureAwait(false);

        return true;
    }

    /// <summary>Like a send, a room no one is in is not loaded for it.</summary>
    public async Task<bool> WhisperAsync(
        RoomId room,
        PlayerId player,
        string text,
        CancellationToken ct
    ) =>
        await grains.GetRoomDirectoryGrain().GetRoomPopulationAsync(room, ct).ConfigureAwait(false)
            > 0
        && await grains
            .GetRoomGrain(room)
            .WhisperToPlayerAsync(player, text, ct)
            .ConfigureAwait(false);

    /// <summary>As the admin panel's kick: the room grain's rules for <paramref name="by"/>, then the player is sent out.</summary>
    public Task<bool> KickAsync(RoomId room, PlayerId player, PlayerId by, CancellationToken ct) =>
        roomService.KickPlayerAsync(ActionContext.CreateForPlayer(by, room), player, ct);

    public Task<FurniLogicReply> InvokeFurniLogicAsync(
        RoomId room,
        int itemId,
        FurniLogicMessage message,
        CancellationToken ct
    ) => grains.GetRoomGrain(room).InvokeFurniLogicAsync(itemId, message, ct);

    public Task LoadAsync(RoomId room, CancellationToken ct) =>
        grains.GetRoomGrain(room).EnsureRoomActiveAsync(ct);

    public Task<RoomHoldOutcome> HoldAsync(
        RoomId room,
        string holder,
        TimeSpan lease,
        CancellationToken ct
    ) => leases.HoldAsync(Plugin, room, holder, lease, ct);

    public Task<bool> ReleaseAsync(RoomId room, string holder, CancellationToken ct) =>
        leases.ReleaseAsync(Plugin, room, holder, ct);

    public Task<RoomWatchOutcome> WatchAsync(
        PlayerId player,
        RoomId room,
        IPluginMessage envelope,
        TimeSpan lease,
        CancellationToken ct
    ) =>
        leases.WatchAsync(
            Plugin,
            player,
            room,
            PluginEnvelopeComposer.OuterOf(envelope),
            lease,
            throughLinkedPair: false,
            ct
        );

    public Task<bool> UnwatchAsync(PlayerId player, RoomId room, CancellationToken ct) =>
        leases.UnwatchAsync(Plugin, player, room, ct);
}
