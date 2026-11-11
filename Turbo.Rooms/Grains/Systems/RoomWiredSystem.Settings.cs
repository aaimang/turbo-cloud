using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// The room's wired boxes' settings read out and saved back: a plugin copies a room's wired (a
/// G-Presets export) or builds one from a file (an import). A save takes the editor's own path,
/// <see cref="ApplyUpdateAsync"/>, so every check a client's save meets is met here too.
/// </summary>
public sealed partial class RoomWiredSystem
{
    /// <summary>Every wired box standing in the room, by item id, as it stores its settings.</summary>
    public async Task<ImmutableArray<WiredBoxSettingsSnapshot>> GetBoxSettingsAsync(
        CancellationToken ct
    )
    {
        var boxes = FurniModule
            .Items.Select(x => x.Logic)
            .OfType<FurnitureWiredLogic>()
            .Where(x => !x.IsImported)
            .OrderBy(x => x.ObjectId.Value)
            .ToList();

        foreach (var box in boxes)
            await box.EnsureWiredLoadedAsync(ct);

        return [.. boxes.Select(x => x.GetSettingsSnapshot())];
    }

    /// <summary>
    /// <paramref name="settings"/> as the editor of box <see cref="WiredBoxSettingsSnapshot.ItemId"/>
    /// would send them, to save through <see cref="ApplyUpdateAsync"/>; false when that is no
    /// wired box of the room.
    /// </summary>
    public bool TryBuildUpdate(
        WiredBoxSettingsSnapshot settings,
        [NotNullWhen(true)] out UpdateWiredMessage? update
    )
    {
        update =
            FurniModule.TryGetItem(RoomObjectId.Parse(settings.ItemId), out var item)
            && item.Logic is FurnitureWiredLogic { IsImported: false } box
                ? box.ToUpdate(settings)
                : null;

        return update is not null;
    }
}
