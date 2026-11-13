namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>How one step of a room's wired trace ended.</summary>
public enum WiredTraceOutcomeType
{
    /// <summary>A stack took its positive side; a condition matched.</summary>
    Passed = 0,

    /// <summary>A stack took its negative side; a condition did not match.</summary>
    Failed = 1,

    /// <summary>A stack stopped before either side: an add-on refused, the room's budget ran out, or the trigger could not fire.</summary>
    Stopped = 2,

    /// <summary>A stack run was dropped before it began: its visit limit, the usage cap, or a stack call back into a stack that already ran.</summary>
    Dropped = 3,

    /// <summary>An effect ran and did what it does.</summary>
    Ran = 4,

    /// <summary>An effect ran and had nothing to do.</summary>
    NoEffect = 5,

    /// <summary>An effect threw; the chain went on.</summary>
    Threw = 6,

    /// <summary>An effect waits for its delay; it is traced again when it runs.</summary>
    Delayed = 7,

    /// <summary>An effect did not run: the execution was cancelled at the usage cap, or its delay hit the delayed-events cap.</summary>
    Cancelled = 8,

    /// <summary>The room is paused: the effect waits to be stepped.</summary>
    Held = 9,
}
