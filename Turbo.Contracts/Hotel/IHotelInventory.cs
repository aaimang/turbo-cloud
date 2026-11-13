using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Contracts.Hotel;

/// <summary>A furni definition: its id, its class name (<c>throne</c>) and whether it stands on the floor or hangs on a wall.</summary>
public sealed record HotelFurniture(int DefinitionId, string Name, bool IsWallItem);

/// <summary>A furni in a player's inventory: its item id and its definition.</summary>
public sealed record HotelOwnedFurni(int ItemId, int DefinitionId);

/// <summary>What a grant under a reference did.</summary>
public enum GrantOutcome
{
    /// <summary>It was made.</summary>
    Granted,

    /// <summary>Made before under the same reference for the same player: nothing made now. Treat it as done.</summary>
    AlreadyGranted,

    /// <summary>
    /// Nothing made: no such definition or player, or the write failed. No receipt is kept, so the
    /// same reference can go through later.
    /// </summary>
    Refused,
}

/// <summary>What players hold: furni and badges.</summary>
public interface IHotelInventory
{
    /// <summary>The definition of that class name (any case), or null when the hotel has none.</summary>
    [HotelCall(HotelCallContext.Anywhere)]
    HotelFurniture? FindFurniture(string name);

    /// <summary>The definition of that id, or null.</summary>
    [HotelCall(HotelCallContext.Anywhere)]
    HotelFurniture? Furniture(int definitionId);

    /// <summary>
    /// Makes one furni of the definition in the player's inventory (online or not), recorded in
    /// the movement log under <paramref name="cause"/>, a code of the plugin's. Not keyed: call it
    /// once per thing given, or give it a reference (the overload below) when a retry must not
    /// give it twice. False when there is no such definition.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> GrantFurnitureAsync(
        PlayerId player,
        int definitionId,
        string cause,
        CancellationToken ct
    );

    /// <summary>
    /// Makes one furni of the definition in the player's inventory once per
    /// <paramref name="reference"/> (<c>&lt;plugin-key&gt;:&lt;id&gt;</c>, the wallet's rules:
    /// <see cref="HotelReference"/>): asked again under the same reference for the same player (a
    /// retry after a crash, a prize claimed twice) it makes nothing and answers
    /// <see cref="GrantOutcome.AlreadyGranted"/>. The reference is the movement's cause ref in the
    /// movement log, beside <paramref name="cause"/>.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<GrantOutcome> GrantFurnitureAsync(
        PlayerId player,
        int definitionId,
        string reference,
        string cause,
        CancellationToken ct
    );

    /// <summary>
    /// Makes one furni of the definition in the player's inventory, as
    /// <see cref="GrantFurnitureAsync(PlayerId, int, string, CancellationToken)"/> does, and answers
    /// its item id (to place it, or to point a wired box at it); null when nothing was made.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<int?> GrantFurnitureItemAsync(
        PlayerId player,
        int definitionId,
        string cause,
        CancellationToken ct
    );

    /// <summary>
    /// The furni in the player's inventory that are theirs to keep, by item id: a rented or
    /// expiring furni is left out. Furni standing in rooms are not in an inventory.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<IReadOnlyList<HotelOwnedFurni>> FurnitureOfAsync(PlayerId player, CancellationToken ct);

    /// <summary>
    /// Deletes every furni in the player's inventory that is theirs to keep (a rented or expiring
    /// furni stays, as in <see cref="FurnitureOfAsync"/>): the rows are gone for good, recorded in
    /// the movement log under <paramref name="cause"/>, a code of the plugin's, and the player's
    /// client is told each has left its list. Furni standing in rooms or held by a trade are not
    /// touched. Answers how many were deleted.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<int> DeleteAllFurnitureAsync(PlayerId player, string cause, CancellationToken ct);

    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> HasBadgeAsync(PlayerId player, string badgeCode, CancellationToken ct);

    /// <summary>Gives the player the badge; false when they had it already.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> GiveBadgeAsync(PlayerId player, string badgeCode, CancellationToken ct);
}
