namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>What one line of a room's wired trace is about.</summary>
public enum WiredTraceStepType
{
    /// <summary>A stack was run by its trigger, a signal or a stack call, and what it decided.</summary>
    Stack = 0,

    /// <summary>A condition the stack asked, and its answer.</summary>
    Condition = 1,

    /// <summary>An effect the stack chose, and what became of it.</summary>
    Effect = 2,
}
