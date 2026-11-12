---
title: "Wired usage across the hotel"
date: 2026-11-12
pr: none yet (wiredbench-seams)
breaks: no
who: "plugins with staff pages about wired load; hosts implementing IHotelWired."
---

**What**: `IHotelWired.UsageAsync` reads the wired load of every loaded room
(`WiredRoomUsageSnapshot`): the wired monitor's figures (`WiredRoomStatsSnapshot`: usage against
its cap, heavy, furni and permanent variable counts), the room's wired boxes and stacks, and the
executions since it loaded with their server time (total and longest) and the wired errors
counted. The room grain gains `GetWiredUsageAsync`, which answers only the system's context.

**Action**: none for existing plugins. A host or test double implementing `IHotelWired` adds the
method; `Turbo.Testing`'s `FakeWired` answers its `Usage` list.
