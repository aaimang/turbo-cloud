using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Main.Hotel;

/// <summary><see cref="IHotelInventory"/> over the furni definitions, the inventory grains and the badge grains.</summary>
internal sealed class HostHotelInventory(
    IGrainFactory grains,
    IFurnitureDefinitionProvider definitions
) : IHotelInventory
{
    public HotelFurniture? FindFurniture(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : ToFurniture(definitions.TryGetDefinitionByName(name.Trim()));

    public HotelFurniture? Furniture(int definitionId) =>
        ToFurniture(definitions.TryGetDefinition(definitionId));

    public async Task<bool> GrantFurnitureAsync(
        PlayerId player,
        int definitionId,
        string cause,
        CancellationToken ct
    ) =>
        definitions.TryGetDefinition(definitionId) is not null
        && await grains
            .GetInventoryGrain(player)
            .GrantFurnitureAsync(definitionId, null, MovementCause.New(cause, player), ct)
            .ConfigureAwait(false)
            is not null;

    public async Task<GrantOutcome> GrantFurnitureAsync(
        PlayerId player,
        int definitionId,
        string reference,
        string cause,
        CancellationToken ct
    )
    {
        HotelReference.ThrowIfInvalid(reference);

        if (definitions.TryGetDefinition(definitionId) is null)
            return GrantOutcome.Refused;

        return await grains
            .GetInventoryGrain(player)
            .GrantFurnitureOnceAsync(
                definitionId,
                reference,
                MovementCause.New(cause, player, reference),
                ct
            )
            .ConfigureAwait(false)
            ? GrantOutcome.Granted
            : GrantOutcome.AlreadyGranted;
    }

    public async Task<int?> GrantFurnitureItemAsync(
        PlayerId player,
        int definitionId,
        string cause,
        CancellationToken ct
    ) =>
        definitions.TryGetDefinition(definitionId) is null
            ? null
            : (
                await grains
                    .GetInventoryGrain(player)
                    .GrantFurnitureAsync(definitionId, null, MovementCause.New(cause, player), ct)
                    .ConfigureAwait(false)
            )
                ?.ItemId
                .Value;

    /// <summary>A furni with an expiry (a rental, a temporary furni) is not the player's to keep.</summary>
    public async Task<IReadOnlyList<HotelOwnedFurni>> FurnitureOfAsync(
        PlayerId player,
        CancellationToken ct
    ) =>
        [
            .. (
                await grains
                    .GetInventoryGrain(player)
                    .GetAllItemSnapshotsAsync(ct)
                    .ConfigureAwait(false)
            )
                .Where(x => x.SecondsToExpiration < 0)
                .OrderBy(x => x.ItemId.Value)
                .Select(x => new HotelOwnedFurni(x.ItemId.Value, x.Definition.Id)),
        ];

    /// <summary>How many furni are used up in one transaction; a refused batch is retried item by item.</summary>
    private const int DELETE_BATCH = 100;

    public async Task<int> DeleteAllFurnitureAsync(
        PlayerId player,
        string cause,
        CancellationToken ct
    )
    {
        var inventory = grains.GetInventoryGrain(player);
        var ids = (await inventory.GetAllItemSnapshotsAsync(ct).ConfigureAwait(false))
            .Where(x => x.SecondsToExpiration < 0)
            .Select(x => x.ItemId)
            .ToList();
        var movement = MovementCause.New(cause, player);
        var deleted = 0;

        foreach (var batch in ids.Chunk(DELETE_BATCH))
        {
            var taken = await inventory
                .ConsumeAllFurnitureAsync([.. batch], movement, ct)
                .ConfigureAwait(false);

            if (!taken.IsEmpty)
            {
                deleted += taken.Length;

                continue;
            }

            foreach (var id in batch)
                if (
                    await inventory.ConsumeFurnitureAsync(id, movement, ct).ConfigureAwait(false)
                    is not null
                )
                    deleted++;
        }

        return deleted;
    }

    public Task<bool> HasBadgeAsync(PlayerId player, string badgeCode, CancellationToken ct) =>
        grains.GetPlayerBadgeGrain(player).HasBadgeAsync(badgeCode, ct);

    public Task<bool> GiveBadgeAsync(PlayerId player, string badgeCode, CancellationToken ct) =>
        grains.GetPlayerBadgeGrain(player).GiveBadgeAsync(badgeCode, ct);

    private static HotelFurniture? ToFurniture(FurnitureDefinitionSnapshot? definition) =>
        definition is null
            ? null
            : new(definition.Id, definition.Name, definition.ProductType == ProductType.Wall);
}
