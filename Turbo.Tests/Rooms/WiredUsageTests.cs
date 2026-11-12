using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Staff's view of a room's wired load: its boxes and stacks with the monitor's figures, read only
/// in the system's context.
/// </summary>
public sealed class WiredUsageTests
{
    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_usage_counts_the_room_s_wired_boxes_with_the_monitor_s_figures()
    {
        _room.AddFloorItem(30, 1, 1);
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredTriggerClickFurni>(2, 1, 0, "wf_trg_click_furni");

        var usage = await _room.Harness.Room.GetWiredUsageAsync(
            ActionContext.CreateForSystem(1),
            Ct
        );

        usage.Should().NotBeNull();
        usage!.WiredBoxes.Should().Be(2);
        usage.Stats.FloorItemCount.Should().Be(3);
        usage.Executions.Should().Be(0);
        usage.Errors.Should().Be(0);
        usage.TraceOn.Should().BeFalse();
    }

    [Fact]
    public async Task A_player_cannot_read_the_usage()
    {
        var usage = await _room.Harness.Room.GetWiredUsageAsync(
            ActionContext.CreateForPlayer(7, 1),
            Ct
        );

        usage.Should().BeNull();
    }
}
