using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// Pause and step. A paused room still fires triggers and asks conditions, but an effect does not
/// run: the chain it belongs to is held, with the index of its next effect, until a step runs it
/// (<see cref="StepAsync"/>) or the room is resumed. A chain left part way by a step goes back to
/// the front, so a step goes down one stack before the next. A held chain was chosen when its
/// stack fired and runs against the room as it is when stepped, and each step is its own
/// execution, so the execution budgets start afresh.
/// </summary>
public sealed partial class RoomWiredSystem
{
    private bool _paused;

    // The chains waiting for a step, the next to run first.
    // A chain that waited for its delay is also in _pendingStackExecutions, and is stale once
    // that no longer holds it; one that ran in the execution that chose it never was.
    private readonly LinkedList<(
        WiredExecutionKey Key,
        WiredPendingStackExecution Pending,
        bool Registered
    )> _held = [];

    // How many effects a step still lets through, and the chain it is stepping.
    private int _stepBudget;
    private WiredExecutionKey? _stepping;

    /// <summary>Pauses the room's wired, or resumes it and runs what was held, in order.</summary>
    public async Task SetPausedAsync(bool paused, CancellationToken ct)
    {
        if (paused)
        {
            _paused = true;

            return;
        }

        _paused = false;

        while (_held.First is { } node)
        {
            _held.RemoveFirst();

            if (IsLive(node.Value))
                await RunHeldAsync(node.Value.Key, node.Value.Pending, ct);
        }
    }

    /// <summary>Runs the next <paramref name="count"/> held effects, one execution each.</summary>
    public async Task<WiredPauseSnapshot> StepAsync(int count, CancellationToken ct)
    {
        var stepped = 0;

        while (stepped < count && _held.First is { } node)
        {
            _held.RemoveFirst();

            var (key, pending, _) = node.Value;

            if (!IsLive(node.Value))
                continue;

            _stepBudget = 1;
            _stepping = key;

            try
            {
                await RunHeldAsync(key, pending, ct);
            }
            finally
            {
                _stepBudget = 0;
                _stepping = null;
            }

            stepped++;
        }

        return GetPause(stepped);
    }

    public WiredPauseSnapshot GetPause(int stepped) =>
        new()
        {
            Paused = _paused,
            Held =
            [
                .. _held
                    .Where(IsLive)
                    .Select(x =>
                    {
                        var box = x.Pending.Actions[
                            Math.Min(x.Pending.NextActionIndex, x.Pending.Actions.Count - 1)
                        ];
                        var logic = box as FurnitureWiredLogic;

                        return new WiredHeldEffectSnapshot
                        {
                            StackId = x.Pending.Stack.StackId,
                            BoxId = logic?.ObjectId.Value ?? 0,
                            BoxLogic = logic?.LogicName ?? string.Empty,
                            BoxCode = box.WiredCode,
                            Remaining = x.Pending.Actions.Count - x.Pending.NextActionIndex,
                            RunId = x.Pending.TraceRunId,
                        };
                    }),
            ],
            Stepped = stepped,
        };

    private bool IsLive(
        (WiredExecutionKey Key, WiredPendingStackExecution Pending, bool Registered) held
    ) =>
        held.Pending.NextActionIndex < held.Pending.Actions.Count
        && (
            !held.Registered
            || (
                _pendingStackExecutions.TryGetValue(held.Key, out var current)
                && ReferenceEquals(current, held.Pending)
            )
        );

    private async Task RunHeldAsync(
        WiredExecutionKey key,
        WiredPendingStackExecution pending,
        CancellationToken ct
    )
    {
        var now = NowMs();

        try
        {
            await RunExecutionAsync(
                async () =>
                {
                    if (await ExecuteStackChainAsync(key, pending, now, ct))
                        _pendingStackExecutions.Remove(key);
                },
                now,
                ct
            );
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Stepping wired in room {RoomId} failed", _roomGrain.RoomId);
        }
    }

    /// <summary>
    /// The gate before an effect: false when the room is paused and no step lets this one run,
    /// and the chain is held from this effect on.
    /// </summary>
    private bool GateEffect(WiredExecutionKey key, WiredPendingStackExecution pending, long now)
    {
        if (!_paused)
            return true;

        if (_stepBudget > 0)
        {
            _stepBudget--;

            return true;
        }

        var action = pending.Actions[pending.NextActionIndex];

        if (_held.Count >= Math.Max(0, WiredConfig.MaxHeldChains))
        {
            TraceEffect(pending, action, WiredTraceOutcomeType.Cancelled, "pause queue full", now);
            _pendingStackExecutions.Remove(key);

            return false;
        }

        var registered = _pendingStackExecutions.ContainsKey(key);

        if (_stepping is { } stepping && stepping.Equals(key))
            _held.AddFirst((key, pending, registered));
        else
            _held.AddLast((key, pending, registered));

        TraceEffect(pending, action, WiredTraceOutcomeType.Held, "paused", now);

        return false;
    }
}
