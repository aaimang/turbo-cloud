using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

/// <summary>
/// A box's settings as stored, and a save of them in the editor's own shape: what lets a room's
/// wired be copied out and saved onto other boxes through the checks of <see cref="ApplyWiredUpdateAsync"/>.
/// </summary>
public abstract partial class FurnitureWiredLogic
{
    /// <summary>
    /// What the box stores, not what its editor is shown: the text as saved (an editor string a
    /// box works out, such as a snapshot's, is left out), and the definition specifics by name.
    /// Read after <see cref="EnsureWiredLoadedAsync"/>, when the lists hold the declared types.
    /// </summary>
    public WiredBoxSettingsSnapshot GetSettingsSnapshot()
    {
        var ints = _wiredData.DefinitionSpecifics.OfType<int>().ToList();
        var bools = _wiredData.DefinitionSpecifics.OfType<bool>().ToList();

        return new WiredBoxSettingsSnapshot
        {
            ItemId = ObjectId.Value,
            WiredType = WiredType,
            WiredCode = WiredCode,
            LogicName = LogicName,
            IntParams = [.. _wiredData.IntParams],
            StringParam = _wiredData.StringParam ?? string.Empty,
            StuffIds = [.. _wiredData.StuffIds],
            StuffIds2 = [.. _wiredData.StuffIds2],
            VariableIds = [.. _wiredData.VariableIds],
            FurniSources =
            [
                .. _wiredData.FurniSources.Select(x =>
                    x.Select(y => (int)WiredFurniSourceTypeExtensions.GetProtocolId(y))
                        .ToImmutableArray()
                ),
            ],
            UserSources =
            [
                .. _wiredData.PlayerSources.Select(x =>
                    x.Select(y => (int)WiredPlayerSourceTypeExtensions.GetProtocolId(y))
                        .ToImmutableArray()
                ),
            ],
            Delay = WiredType == WiredType.Action && ints.Count > 0 ? ints[0] : 0,
            Quantifier = WiredType == WiredType.Condition && ints.Count > 0 ? ints[0] : 0,
            Filter = bools.Count > 0 && bools[0],
            Invert = bools.Count > 1 && bools[1],
            SnapshotText = KeepsFurniSnapshot ? GetEditorStringParam() : string.Empty,
        };
    }

    /// <summary>
    /// <paramref name="settings"/> as this box's editor would send them: the definition specifics
    /// each kind's save carries (an effect its delay, a condition its quantifier, a selector its
    /// two switches) and none for the others, as the client's save packets do.
    /// </summary>
    public UpdateWiredMessage ToUpdate(WiredBoxSettingsSnapshot settings) =>
        new()
        {
            Id = ObjectId.Value,
            IntParams = [.. settings.IntParams],
            StringParam = settings.StringParam ?? string.Empty,
            StuffIds = [.. settings.StuffIds],
            StuffIds2 = [.. settings.StuffIds2],
            VariableIds = [.. settings.VariableIds],
            FurniSources =
            [
                .. settings.FurniSources.Select(x =>
                    x.Select(y => WiredFurniSourceTypeExtensions.FromProtocolId((WiredSourceType)y))
                        .ToArray()
                ),
            ],
            PlayerSources =
            [
                .. settings.UserSources.Select(x =>
                    x.Select(y =>
                            WiredPlayerSourceTypeExtensions.FromProtocolId((WiredSourceType)y)
                        )
                        .ToArray()
                ),
            ],
            DefinitionSpecifics = WiredType switch
            {
                WiredType.Action => [settings.Delay],
                WiredType.Condition => [settings.Quantifier],
                WiredType.Selector => [settings.Filter, settings.Invert],
                _ => new List<object>(),
            },
            TypeSpecifics = [],
        };
}
