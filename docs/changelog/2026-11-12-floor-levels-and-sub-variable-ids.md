---
title: "Floor levels in a room layout, and Habbo's sub-variable ids"
date: 2026-11-12
pr: none yet (wiredbench-seams)
breaks: no
who: "plugins that copy furni between rooms (presets, sandboxes) or read wired variable ids from files."
---

**What**: `RoomLayoutSnapshot` (`IHotelRooms.CaptureLayoutAsync`) also carries the room's floor:
`FloorWidth` and `FloorLevels`, the level of the floor on each tile in hundredths (as an item's
`Z`), `NO_FLOOR` (-1) where the room has none. `FloorLevelAt(x, y)` reads one tile and
`LowestFloorLevel(x1, y1, x2, y2)` the lowest floor of an area: what G-Presets measures a copy's
heights from.

`WiredVariableId.TryParse` reads a sub-variable id as Habbo's files write it (G-Presets):
`s<parent>-<add-on furni id>` and `s<parent>-<add-on furni id>-<index>` for the add-on's
sub-variables past its first, the parent being any variable id (`-120` and `~5` included).
It is the add-on's `OfBox(addonId, index)`. `TryParseSub` gives the parts and `SubText` writes
one.

**Action**: none. The new snapshot members have defaults, so a layout made without them (a
test double's) reads as no floor.
