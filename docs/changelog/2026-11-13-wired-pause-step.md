---
title: "Pause and step wired"
date: 2026-11-13
pr: none yet (wiredbench-seams)
breaks: no
who: "plugins that debug wired stacks; hosts implementing IHotelWired."
---

**What**: `IHotelWired.SetPausedAsync` pauses a room's wired or resumes it, `StepAsync` runs the
next N held effects, and `GetPauseAsync` reads the pause (`WiredPauseSnapshot` of
`WiredHeldEffectSnapshot`). A paused room still fires triggers and asks conditions, but each
stack's effects wait: the chain is held from its next effect on, and traced as `Held`. A step runs
one effect as its own execution; the rest of its chain returns to the front, so a step goes down
one stack before the next. Resuming runs what is held, in order. A held chain runs against the
room as it is when stepped, and its execution budgets start afresh. `Turbo:Wired:MaxHeldChains`
(200) caps what a pause holds; one more is cancelled and traced. Setting and stepping need wired
modify permission, reading wired read. The room grain gains `SetWiredPausedAsync`,
`StepWiredAsync` and `GetWiredPauseAsync`.

**Action**: none for existing plugins. A host or test double implementing `IHotelWired` adds the
three methods; `Turbo.Testing`'s `FakeWired` records the pause and holds no effects.
