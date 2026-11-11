using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// The wired trace: while it is on, every stack run, each condition it asks and each effect it
/// chooses is recorded as a step a reader pages through by id (<see cref="GetTrace"/>), and each
/// stack run also writes one line to the room's log under source 0 (System), so the client's
/// "View full logs" shows it. Off by default and per room; turned on and read by plugins through
/// <c>IHotelWired</c>. The steps are kept in memory, at most <c>WiredConfig.MaxTraceEntries</c>,
/// and never change what a stack does: a condition is traced as it is asked, so the ones never
/// asked after the first that failed are missing from the trace as they are from the run.
/// </summary>
public sealed partial class RoomWiredSystem
{
    /// <summary><c>wiredmenu.logs_overview.log_source.0</c>: a line the room itself wrote.</summary>
    public const int LOG_SOURCE_SYSTEM = 0;

    // A thrown message is cut to this in a step; the silo log has the whole exception.
    private const int MAX_TRACE_DETAIL_LENGTH = 200;

    private readonly LinkedList<WiredTraceEntrySnapshot> _trace = [];
    private bool _traceOn;
    private long _nextTraceId = 1;
    private long _nextTraceRunId = 1;

    /// <summary>Turns the room's trace on or off; what was recorded is kept either way.</summary>
    public void SetTrace(bool enabled) => _traceOn = enabled;

    /// <summary>The steps recorded after <paramref name="afterId"/>, oldest first.</summary>
    public WiredTraceSnapshot GetTrace(long afterId) =>
        new()
        {
            Enabled = _traceOn,
            Entries = [.. _trace.Where(x => x.Id > afterId)],
            LastId = _nextTraceId - 1,
        };

    /// <summary>A new run id for a stack run while tracing is on; 0 (not traced) otherwise.</summary>
    private long BeginTraceRun() =>
        _traceOn && WiredConfig.MaxTraceEntries > 0 ? _nextTraceRunId++ : 0;

    /// <summary>
    /// What <paramref name="ctx"/>'s stack decided, with the summary line in the room log. A
    /// condition step of the run was recorded before it, as the condition was asked.
    /// </summary>
    private void TraceStackRun(
        WiredProcessingContext ctx,
        WiredTraceOutcomeType outcome,
        string detail
    )
    {
        if (ctx.TraceRunId == 0 || !_traceOn)
            return;

        var now = NowMs();
        var asked = 0;
        var matched = 0;

        for (var node = _trace.Last; node is not null; node = node.Previous)
        {
            if (
                node.Value.RunId != ctx.TraceRunId
                || node.Value.Step != WiredTraceStepType.Condition
            )
                break;

            asked++;

            if (node.Value.Outcome == WiredTraceOutcomeType.Passed)
                matched++;
        }

        AddTraceStep(
            ctx.TraceRunId,
            ctx.Stack.StackId,
            WiredTraceStepType.Stack,
            outcome,
            ctx.Trigger,
            detail,
            now
        );

        var by = ctx.Trigger is { } trigger ? BoxName(trigger) : "a stack call";
        var conditions =
            ctx.Stack.Conditions.Count == 0
                ? "no conditions"
                : $"conditions {matched}/{asked} matched of {ctx.Stack.Conditions.Count}";
        var tail = detail.Length > 0 ? $", {detail}" : string.Empty;

        AppendRoomLog(
            WiredLogLevelType.Debug,
            LOG_SOURCE_SYSTEM,
            $"Trace {ctx.TraceRunId}: stack {ctx.Stack.StackId} by {by}: {outcome}, {conditions}{tail}"
        );
    }

    /// <summary>Asks <paramref name="condition"/> and records its answer when the run is traced.</summary>
    private bool EvaluateTraced(WiredProcessingContext ctx, IWiredCondition condition)
    {
        var matched = condition.Evaluate(ctx);

        if (ctx.TraceRunId != 0 && _traceOn)
            AddTraceStep(
                ctx.TraceRunId,
                ctx.Stack.StackId,
                WiredTraceStepType.Condition,
                matched ? WiredTraceOutcomeType.Passed : WiredTraceOutcomeType.Failed,
                condition,
                string.Empty,
                NowMs()
            );

        return matched;
    }

    /// <summary>What became of one effect of a traced chain.</summary>
    private void TraceEffect(
        WiredPendingStackExecution pending,
        IWiredAction action,
        WiredTraceOutcomeType outcome,
        string detail,
        long now
    )
    {
        if (pending.TraceRunId == 0 || !_traceOn)
            return;

        AddTraceStep(
            pending.TraceRunId,
            pending.Stack.StackId,
            WiredTraceStepType.Effect,
            outcome,
            action,
            detail,
            now
        );
    }

    /// <summary>A box as a trace line names it: its logic and furni id.</summary>
    private static string BoxName(IWiredBox box) =>
        box is FurnitureWiredLogic logic
            ? $"{logic.LogicName} #{logic.ObjectId.Value}"
            : box.GetType().Name;

    private static string TraceDetail(Exception ex)
    {
        var text = $"{ex.GetType().Name}: {ex.Message}";

        return text.Length <= MAX_TRACE_DETAIL_LENGTH ? text : text[..MAX_TRACE_DETAIL_LENGTH];
    }

    private void AddTraceStep(
        long runId,
        int stackId,
        WiredTraceStepType step,
        WiredTraceOutcomeType outcome,
        IWiredBox? box,
        string detail,
        long atMs
    )
    {
        var cap = WiredConfig.MaxTraceEntries;

        if (cap <= 0)
            return;

        while (_trace.Count >= cap)
            _trace.RemoveFirst();

        var logic = box as FurnitureWiredLogic;

        _trace.AddLast(
            new WiredTraceEntrySnapshot
            {
                Id = _nextTraceId++,
                RunId = runId,
                StackId = stackId,
                Step = step,
                Outcome = outcome,
                BoxId = logic?.ObjectId.Value ?? 0,
                BoxLogic = logic?.LogicName ?? string.Empty,
                BoxCode = box?.WiredCode ?? -1,
                Detail = detail,
                AtMs = atMs,
            }
        );
    }
}
