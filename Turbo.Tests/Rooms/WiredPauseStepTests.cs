using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Pause and step (<c>IHotelWired.SetPausedAsync</c>, <c>StepAsync</c>): a paused room fires its
/// triggers but holds each stack's effects until they are stepped or the room is resumed. The
/// stack: a click and two "write to logs" effects.
/// </summary>
public sealed class WiredPauseStepTests
{
    private const int CLICK_ME = 23;
    private const int TRIGGER = 1;
    private const int LOG_A = 4;
    private const int LOG_B = 5;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionContext System => ActionContext.CreateForSystem(1);

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredPauseStepTests()
    {
        _room.Enter(5, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddBox<WiredTriggerClickFurni>(TRIGGER, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionWriteToLogs>(LOG_A, 0, 0, "wf_act_log");
        _room.AddBox<WiredActionWriteToLogs>(LOG_B, 0, 0, "wf_act_log");
    }

    [Fact]
    public async Task A_room_that_is_not_paused_runs_both_effects()
    {
        await BuildAsync();

        await ClickAsync();

        Ran.Should().Be(2);
        (await PauseAsync()).Held.Should().BeEmpty();
    }

    [Fact]
    public async Task A_paused_room_holds_the_chain_and_runs_nothing()
    {
        await BuildAsync();
        await _room.Harness.Room.SetWiredTraceAsync(System, true, Ct);
        (await _room.Harness.Room.SetWiredPausedAsync(System, true, Ct)).Should().BeTrue();

        await ClickAsync();

        Ran.Should().Be(0);

        var pause = await PauseAsync();

        pause.Paused.Should().BeTrue();
        pause.Held.Should().ContainSingle().Which.Remaining.Should().Be(2);
        (await _room.Harness.Room.GetWiredTraceAsync(System, 0, Ct))!
            .Entries.Should()
            .Contain(x =>
                x.Step == WiredTraceStepType.Effect && x.Outcome == WiredTraceOutcomeType.Held
            );
    }

    [Fact]
    public async Task A_step_runs_one_effect_and_keeps_the_rest_at_the_front()
    {
        await BuildAsync();
        await _room.Harness.Room.SetWiredPausedAsync(System, true, Ct);
        await ClickAsync();

        var first = (await _room.Harness.Room.StepWiredAsync(System, 1, Ct))!;

        Ran.Should().Be(1);
        first.Stepped.Should().Be(1);
        first.Paused.Should().BeTrue();
        first.Held.Should().ContainSingle().Which.Remaining.Should().Be(1);

        var second = (await _room.Harness.Room.StepWiredAsync(System, 1, Ct))!;

        Ran.Should().Be(2);
        second.Held.Should().BeEmpty();
        (await _room.Harness.Room.StepWiredAsync(System, 1, Ct))!.Stepped.Should().Be(0);
    }

    [Fact]
    public async Task Resuming_runs_what_is_held_and_unpauses()
    {
        await BuildAsync();
        await _room.Harness.Room.SetWiredPausedAsync(System, true, Ct);
        await ClickAsync();

        await _room.Harness.Room.SetWiredPausedAsync(System, false, Ct);

        Ran.Should().Be(2);

        var pause = await PauseAsync();

        pause.Paused.Should().BeFalse();
        pause.Held.Should().BeEmpty();

        await ClickAsync();

        Ran.Should().Be(4);
    }

    private int Ran =>
        Wired.GetRoomLogs().Count(x => x.Source == RoomWiredSystem.LOG_SOURCE_WIRED);

    private async Task<WiredPauseSnapshot> PauseAsync()
    {
        var pause = await _room.Harness.Room.GetWiredPauseAsync(System, Ct);

        pause.Should().NotBeNull();

        return pause!;
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

        foreach (var id in new[] { LOG_A, LOG_B })
            (
                await _room.SaveAsync<UpdateActionMessage>(
                    id,
                    intParams: [(int)WiredLogLevelType.Warning],
                    stringParam: "ran",
                    definitionSpecifics: [0]
                )
            )
                .Should()
                .BeTrue();

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = System,
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );
        await TickAsync(1);
    }

    private async Task ClickAsync()
    {
        await _room.FloorItem(CLICK_ME).Logic.OnClickAsync(_room.ContextFor(5), 0, Ct);
        await TickAsync(4);
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
