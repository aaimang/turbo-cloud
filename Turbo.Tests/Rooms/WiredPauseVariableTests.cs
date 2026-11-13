using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A stack that gives a user a variable and then changes it, run unpaused and stepped one effect
/// at a time: the second effect finds the variable the first gave in both.
/// </summary>
public sealed class WiredPauseVariableTests
{
    private const int CLICKER = 5;
    private const int CLICKED = 6;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionContext System => ActionContext.CreateForSystem(1);

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_change_finds_the_variable_the_give_made(bool stepped)
    {
        await BuildAsync();
        await _room.Harness.Room.SetWiredTraceAsync(System, true, Ct);

        if (stepped)
            await _room.Harness.Room.SetWiredPausedAsync(System, true, Ct);

        await ClickAsync();

        if (stepped)
        {
            await _room.Harness.Room.StepWiredAsync(System, 1, Ct);
            await _room.Harness.Room.StepWiredAsync(System, 1, Ct);
        }

        var effects = (await _room.Harness.Room.GetWiredTraceAsync(System, 0, Ct))!
            .Entries.Where(x =>
                x.Step == WiredTraceStepType.Effect && x.Outcome != WiredTraceOutcomeType.Held
            )
            .Select(x => (x.BoxLogic, x.Outcome))
            .ToList();

        effects
            .Should()
            .Equal(
                ("wf_act_give_var", WiredTraceOutcomeType.Ran),
                ("wf_act_change_var_val", WiredTraceOutcomeType.Ran)
            );
    }

    private async Task BuildAsync()
    {
        _room.Enter(CLICKER, 1, 1);
        _room.Enter(CLICKED, 2, 2);

        var mark = _room.AddBox<WiredVariableUser>(12, 6, 6, "wf_var_user");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                12,
                intParams: [(int)WiredAvailabilityType.UserActive, 1],
                stringParam: "mark"
            )
        )
            .Should()
            .BeTrue();
        await mark.LoadWiredAsync(Ct);
        await StartAsync(6, 6);

        _room.AddBox<WiredTriggerClickUser>(1, 0, 0, "wf_trg_click_user");
        _room.AddBox<WiredActionGiveVariable>(2, 0, 0, "wf_act_give_var");
        _room.AddBox<WiredActionChangeVariable>(3, 0, 0, "wf_act_change_var_val");

        (await _room.SaveAsync<UpdateTriggerMessage>(1, intParams: [0, 0])).Should().BeTrue();

        var id = mark.GetVarSnapshot().VariableId.ToString();

        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [(int)WiredVariableTargetType.User, 0, 7, 0],
                definitionSpecifics: [0],
                variableIds: [id]
            )
        )
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                3,
                intParams:
                [
                    (int)WiredVariableTargetType.User,
                    (int)WiredVariableOperationType.Add,
                    0,
                    0,
                    1,
                    -10,
                    0,
                ],
                definitionSpecifics: [0],
                variableIds: [id, "0"]
            )
        )
            .Should()
            .BeTrue();
        await StartAsync(0, 0);
    }

    private async Task ClickAsync()
    {
        await Wired.OnRoomEventAsync(
            new PlayerClickedAvatarEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForPlayer((PlayerId)(100 + CLICKER), (RoomId)1),
                PlayerId = (PlayerId)(100 + CLICKER),
                TargetObjectId = CLICKED,
            },
            Ct
        );
        await TickAsync(3);
    }

    private async Task StartAsync(int x, int y)
    {
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = System,
                StackIds = [_room.Map.ToIdx(x, y)],
            },
            Ct
        );

        await TickAsync(1);
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
