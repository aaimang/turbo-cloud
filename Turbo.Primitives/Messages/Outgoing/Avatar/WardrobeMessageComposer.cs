using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Snapshots.Wardrobe;

namespace Turbo.Primitives.Messages.Outgoing.Avatar;

[GenerateSerializer, Immutable]
public sealed record WardrobeMessageComposer : IComposer
{
    /// <summary>The only state the client's wardrobe reply knows: the outfits are loaded.</summary>
    public const int STATE_LOADED = 1;

    [Id(0)]
    public required int State { get; init; }

    [Id(1)]
    public required List<OutfitDataSnapshot> Outfits { get; init; }
}
