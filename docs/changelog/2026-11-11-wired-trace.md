---
title: "Wired trace"
date: 2026-11-11
pr: none yet (wiredbench-seams)
breaks: no
who: "plugins that debug or explain wired stacks; hosts implementing IHotelWired."
---

**What**: `IHotelWired.SetTraceAsync` turns a room's wired trace on or off, as a player who may
change the room's wired, and `IHotelWired.GetTraceAsync` reads its steps after an id
(`WiredTraceSnapshot` of `WiredTraceEntrySnapshot`): each stack run and what it decided
(passed, failed, stopped, dropped, and why), each condition as it was asked, and what became of
each effect it chose (ran, no effect, threw, delayed, cancelled). While it is on, each stack run
also writes one line to the room's log under source 0 (System), which "View full logs" shows.
The trace is off by default, held in memory while the room is loaded, and capped by
`Turbo:Wired:MaxTraceEntries`. The room grain gains `SetWiredTraceAsync` and
`GetWiredTraceAsync`.

**Action**: none for existing plugins. A host or test double implementing `IHotelWired` adds the
two methods; `Turbo.Testing`'s `FakeWired` records who turned the trace on and returns no steps.
