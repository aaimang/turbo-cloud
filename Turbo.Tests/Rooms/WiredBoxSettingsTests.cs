using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A room's wired boxes' settings read out and saved back (<c>IHotelWired.ReadBoxesAsync</c> /
/// <c>SaveBoxAsync</c>, a G-Presets export and import): each box as it stores itself, the
/// definition specifics by name (an effect's delay, a condition's quantifier, a selector's two
/// switches), and a save through the editor's own checks.
/// </summary>
public sealed class WiredBoxSettingsTests
{
    private const int CLICK_ME = 23;
    private const int TRIGGER = 1;
    private const int EFFECT = 2;
    private const int CONDITION = 3;
    private const int SELECTOR = 4;
    private const int OTHER_EFFECT = 5;
    private const int MATCH = 6;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionContext System => ActionContext.CreateForSystem(1);

    public WiredBoxSettingsTests()
    {
        _room.Enter(5, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddBox<WiredTriggerClickFurni>(TRIGGER, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionWriteToLogs>(EFFECT, 0, 0, "wf_act_log");
        _room.AddBox<WiredConditionRoomHabboCount>(CONDITION, 0, 0, "wf_cnd_user_count_in");
        _room.AddBox<WiredSelectorItemsByType>(SELECTOR, 0, 0, "wf_slc_furni_bytype");
        _room.AddBox<WiredActionWriteToLogs>(OTHER_EFFECT, 1, 0, "wf_act_log");
    }

    [Fact]
    public async Task Each_box_reads_as_it_stores_its_settings()
    {
        await BuildAsync();

        var boxes = await ReadAsync();

        boxes
            .Select(x => (x.ItemId, x.LogicName, x.WiredType))
            .Should()
            .Equal(
                (TRIGGER, "wf_trg_click_furni", WiredType.Trigger),
                (EFFECT, "wf_act_log", WiredType.Action),
                (CONDITION, "wf_cnd_user_count_in", WiredType.Condition),
                (SELECTOR, "wf_slc_furni_bytype", WiredType.Selector),
                (OTHER_EFFECT, "wf_act_log", WiredType.Action)
            );

        var trigger = boxes.Single(x => x.ItemId == TRIGGER);
        trigger.StuffIds.Should().Equal(CLICK_ME);
        trigger
            .FurniSources.Should()
            .ContainSingle()
            .Which.Should()
            // The client's code for "picked furni", as a save packet and a G-Presets file carry it.
            .Equal((int)WiredSourceType.SelectedItems);

        var effect = boxes.Single(x => x.ItemId == EFFECT);
        effect.StringParam.Should().Be("hello");
        effect.IntParams.Should().Equal((int)WiredLogLevelType.Warning);
        effect.Delay.Should().Be(4);

        boxes.Single(x => x.ItemId == CONDITION).IntParams.Should().Equal(1, 50);
        boxes.Single(x => x.ItemId == SELECTOR).Filter.Should().BeTrue();
        boxes.Single(x => x.ItemId == SELECTOR).Invert.Should().BeFalse();
    }

    [Fact]
    public async Task One_box_s_settings_saved_onto_another_read_back_the_same()
    {
        await BuildAsync();
        var effect = (await ReadAsync()).Single(x => x.ItemId == EFFECT);

        var result = await _room.Harness.Room.SaveWiredBoxSettingsAsync(
            System,
            effect with
            {
                ItemId = OTHER_EFFECT,
            },
            Ct
        );

        result.IsSaved.Should().BeTrue();
        (await ReadAsync())
            .Single(x => x.ItemId == OTHER_EFFECT)
            .Should()
            .BeEquivalentTo(effect with { ItemId = OTHER_EFFECT });
    }

    // G-Presets files carry the client's source codes (100 picked furni, 200 selector, 0 the
    // triggering user, 100 a bot by name): a save read them as the server's own numbers and
    // refused every box with a source (invalid_furni_source, invalid_user_source).
    [Fact]
    public async Task Sources_are_the_client_s_codes_both_ways()
    {
        await BuildAsync();
        var trigger = (await ReadAsync()).Single(x => x.ItemId == TRIGGER);

        var result = await _room.Harness.Room.SaveWiredBoxSettingsAsync(
            System,
            trigger with
            {
                FurniSources =
                [
                    [(int)WiredSourceType.SelectedItems],
                ],
            },
            Ct
        );

        result.IsSaved.Should().BeTrue();
        (await ReadAsync())
            .Single(x => x.ItemId == TRIGGER)
            .FurniSources.Should()
            .ContainSingle()
            .Which.Should()
            .Equal(100);
    }

    // A G-Presets export writes a snapshot box's snapshot as "bindings", from the box's text.
    [Fact]
    public async Task A_snapshot_box_reads_its_snapshot_as_Habbo_writes_it()
    {
        await BuildAsync();
        _room.AddBox<WiredActionMatchToSnapshot>(MATCH, 2, 0, "wf_act_match_to_sshot");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                MATCH,
                intParams: [1, 0, 1, 0],
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        var boxes = await ReadAsync();

        // State and position kept, rotation and altitude left out ("N"): the fridge at 7,4.
        boxes.Single(x => x.ItemId == MATCH).SnapshotText.Should().Be($"{CLICK_ME},0,N,7,4,N");
        boxes.Single(x => x.ItemId == EFFECT).SnapshotText.Should().BeEmpty();
    }

    [Fact]
    public async Task A_save_meets_the_editor_s_checks()
    {
        await BuildAsync();
        var condition = (await ReadAsync()).Single(x => x.ItemId == CONDITION);

        // More picks than any box may hold is refused before the box's own checks, as a client's
        // save is (WiredConfig.SelectedItemsLimit, 20 by default).
        var result = await _room.Harness.Room.SaveWiredBoxSettingsAsync(
            System,
            condition with
            {
                StuffIds = [.. Enumerable.Range(100, 21)],
            },
            Ct
        );

        result.IsSaved.Should().BeFalse();
        (await ReadAsync()).Single(x => x.ItemId == CONDITION).Should().BeEquivalentTo(condition);
    }

    [Fact]
    public async Task A_box_that_is_not_in_the_room_is_the_server_error()
    {
        await BuildAsync();
        var effect = (await ReadAsync()).Single(x => x.ItemId == EFFECT);

        (
            await _room.Harness.Room.SaveWiredBoxSettingsAsync(
                System,
                effect with
                {
                    ItemId = 99,
                },
                Ct
            )
        )
            .Should()
            .Be(Turbo.Primitives.Rooms.Wired.WiredSaveResult.ServerError);
        (
            await _room.Harness.Room.SaveWiredBoxSettingsAsync(
                System,
                effect with
                {
                    ItemId = CLICK_ME,
                },
                Ct
            )
        )
            .Should()
            .Be(Turbo.Primitives.Rooms.Wired.WiredSaveResult.ServerError);
    }

    [Fact]
    public async Task A_player_who_may_not_read_the_wired_reads_nothing()
    {
        (
            await _room.Harness.Room.GetWiredBoxSettingsAsync(
                ActionContext.CreateForPlayer(777, 1),
                Ct
            )
        )
            .Should()
            .BeNull();
    }

    private async Task<IReadOnlyList<WiredBoxSettingsSnapshot>> ReadAsync()
    {
        var boxes = await _room.Harness.Room.GetWiredBoxSettingsAsync(System, Ct);

        boxes.Should().NotBeNull();

        return boxes!.Value;
    }

    private async Task BuildAsync()
    {
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                TRIGGER,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams: [(int)WiredLogLevelType.Warning],
                stringParam: "hello",
                definitionSpecifics: [4]
            )
        )
            .Should()
            .BeTrue();
        (await _room.SaveAsync<UpdateConditionMessage>(CONDITION, intParams: [1, 50]))
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                SELECTOR,
                intParams: [0],
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [true, false]
            )
        ).Should().BeTrue();
    }
}
