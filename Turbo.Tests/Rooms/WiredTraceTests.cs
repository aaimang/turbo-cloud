using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The wired trace a plugin turns on per room (<c>IHotelWired.SetTraceAsync</c>): each stack run,
/// each condition as it is asked and each effect, as steps read by id, and one line per run in the
/// room log under source 0 (System). Off, nothing is recorded. The stack: a click, user-count
/// conditions with one user in the room, a "write to logs".
/// </summary>
public sealed class WiredTraceTests
{
    private const int CLICK_ME = 23;
    private const int TRIGGER = 1;
    private const int FIRST = 2;
    private const int SECOND = 3;
    private const int LOG = 4;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionContext System => ActionContext.CreateForSystem(1);

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredTraceTests()
    {
        _room.Enter(5, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddBox<WiredTriggerClickFurni>(TRIGGER, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionWriteToLogs>(LOG, 0, 0, "wf_act_log");
    }

    [Fact]
    public async Task Nothing_is_traced_until_the_trace_is_on()
    {
        await BuildAsync(conditions: [(FIRST, 1)], delay: 0);

        await ClickAsync();

        var trace = await TraceAsync();

        trace.Enabled.Should().BeFalse();
        trace.Entries.Should().BeEmpty();
        Wired
            .GetRoomLogs()
            .Should()
            .ContainSingle(x => x.Source == RoomWiredSystem.LOG_SOURCE_WIRED);
        Wired.GetRoomLogs().Should().NotContain(x => x.Source == RoomWiredSystem.LOG_SOURCE_SYSTEM);
    }

    [Fact]
    public async Task A_traced_run_lists_its_condition_its_outcome_and_its_effect()
    {
        await BuildAsync(conditions: [(FIRST, 1)], delay: 0);
        (await _room.Harness.Room.SetWiredTraceAsync(System, true, Ct)).Should().BeTrue();

        await ClickAsync();

        var trace = await TraceAsync();

        trace.Enabled.Should().BeTrue();
        trace
            .Entries.Select(x => (x.Step, x.Outcome, x.BoxId, x.BoxLogic))
            .Should()
            .Equal(
                (
                    WiredTraceStepType.Condition,
                    WiredTraceOutcomeType.Passed,
                    FIRST,
                    "wf_cnd_user_count_in"
                ),
                (
                    WiredTraceStepType.Stack,
                    WiredTraceOutcomeType.Passed,
                    TRIGGER,
                    "wf_trg_click_furni"
                ),
                (WiredTraceStepType.Effect, WiredTraceOutcomeType.Ran, LOG, "wf_act_log")
            );
        trace.Entries.Select(x => x.RunId).Distinct().Should().ContainSingle();
        trace.Entries.Should().OnlyContain(x => x.StackId == _room.Map.ToIdx(0, 0));
        trace.LastId.Should().Be(trace.Entries[^1].Id);

        Wired
            .GetRoomLogs()
            .Where(x => x.Source == RoomWiredSystem.LOG_SOURCE_SYSTEM)
            .Select(x => x.Message)
            .Should()
            .Equal(
                $"Trace {trace.Entries[0].RunId}: stack {_room.Map.ToIdx(0, 0)} by wf_trg_click_furni #{TRIGGER}: Passed, conditions 1/1 matched of 1, 1 effect chosen"
            );

        (await TraceAsync(afterId: trace.LastId)).Entries.Should().BeEmpty();
    }

    // Conditions are asked until one fails; the trace lists the ones asked, not the ones skipped.
    [Fact]
    public async Task A_failing_condition_is_the_last_one_traced()
    {
        await BuildAsync(conditions: [(FIRST, 5), (SECOND, 6)], delay: 0);
        await _room.Harness.Room.SetWiredTraceAsync(System, true, Ct);

        await ClickAsync();

        var trace = await TraceAsync();

        trace
            .Entries.Select(x => (x.Step, x.Outcome))
            .Should()
            .Equal(
                (WiredTraceStepType.Condition, WiredTraceOutcomeType.Failed),
                (WiredTraceStepType.Stack, WiredTraceOutcomeType.Failed)
            );
        trace.Entries[1].Detail.Should().Be("0 effects chosen");
        Wired
            .GetRoomLogs()
            .Should()
            .ContainSingle(x =>
                x.Source == RoomWiredSystem.LOG_SOURCE_SYSTEM
                && x.Message.Contains("Failed, conditions 0/1 matched of 2")
            );
    }

    [Fact]
    public async Task A_delayed_effect_is_traced_when_it_waits_and_when_it_runs()
    {
        await BuildAsync(conditions: [], delay: 2);
        await _room.Harness.Room.SetWiredTraceAsync(System, true, Ct);

        await ClickAsync();

        var effects = (await TraceAsync())
            .Entries.Where(x => x.Step == WiredTraceStepType.Effect)
            .ToList();

        effects
            .Select(x => x.Outcome)
            .Should()
            .Equal(WiredTraceOutcomeType.Delayed, WiredTraceOutcomeType.Ran);
        effects[0].Detail.Should().Be("waits 1000 ms");
        effects.Select(x => x.RunId).Distinct().Should().ContainSingle();
    }

    // The Trace window shows when each step happened, in the room's wired time zone.
    [Fact]
    public async Task A_step_carries_its_time_in_the_room_s_time_zone()
    {
        await BuildAsync(conditions: [], delay: 0);
        await _room.Harness.Room.SetWiredTraceAsync(System, true, Ct);

        await ClickAsync();

        var trace = await TraceAsync();
        var step = trace.Entries[0];
        var expected = TimeZoneInfo
            .ConvertTime(
                DateTimeOffset.FromUnixTimeMilliseconds(step.AtUtcMs),
                Wired.GetRoomTimeZone()
            )
            .ToString("HH:mm:ss", global::System.Globalization.CultureInfo.InvariantCulture);

        trace.TimeZone.Should().Be(Wired.GetRoomTimeZone().Id);
        step.AtUtcMs.Should().BePositive();
        step.AtText.Should().Be(expected);
    }

    [Fact]
    public async Task Turning_the_trace_off_stops_recording_and_keeps_what_was_recorded()
    {
        await BuildAsync(conditions: [], delay: 0);
        await _room.Harness.Room.SetWiredTraceAsync(System, true, Ct);
        await ClickAsync();
        var recorded = (await TraceAsync()).Entries.Length;

        await _room.Harness.Room.SetWiredTraceAsync(System, false, Ct);
        await ClickAsync();

        var trace = await TraceAsync();

        trace.Enabled.Should().BeFalse();
        trace.Entries.Should().HaveCount(recorded);
    }

    private async Task<WiredTraceSnapshot> TraceAsync(long afterId = 0)
    {
        var trace = await _room.Harness.Room.GetWiredTraceAsync(System, afterId, Ct);

        trace.Should().NotBeNull();

        return trace!;
    }

    private async Task BuildAsync((int Id, int Min)[] conditions, int delay)
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

        // One user in the room: a minimum of 1 holds, 5 or more does not.
        foreach (var (id, min) in conditions)
        {
            _room.AddBox<WiredConditionRoomHabboCount>(id, 0, 0, "wf_cnd_user_count_in");
            (await _room.SaveAsync<UpdateConditionMessage>(id, intParams: [min, 50]))
                .Should()
                .BeTrue();
        }

        (
            await _room.SaveAsync<UpdateActionMessage>(
                LOG,
                intParams: [(int)WiredLogLevelType.Warning],
                stringParam: "ran",
                definitionSpecifics: [delay]
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
