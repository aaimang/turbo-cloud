---
title: "Room layout capture and restore"
date: 2026-11-11
pr: none yet (wiredbench-seams)
breaks: no
who: "plugins that reset rooms (WIRED sandboxes, games); hosts implementing IHotelRooms."
---

**What**: `IHotelRooms.CaptureLayoutAsync` reads a room's floor furni as they stand (each one's
tile, height, rotation and state, a `RoomLayoutSnapshot`), and `IHotelRooms.RestoreLayoutAsync`
puts the furni still in the room back that way, lowest first, through the room's own move so each
furni's logic hears of it. With `putBackPickedUp`, a captured furni picked up since and now in the
player's inventory is placed again first, through the client's own placement (rights, the spot,
the room's limits and the placement policies decide). It answers a `RoomLayoutRestore`: how many
furni it changed and how many it put back. Both act as a player who may
change the room's wired, and answer null otherwise or when no one is in the room. A wired box's
state (its flash) is not restored. The room grain gains `CaptureFloorLayoutAsync` and
`RestoreFloorLayoutAsync`.

**Action**: none for existing plugins. A host or test double implementing `IHotelRooms` adds the
two methods; `Turbo.Testing`'s `FakeRooms` captures its furni's tiles (height, rotation and state
are 0) and moves them back; with `putBackPickedUp` it takes a furni out of the player's
`FakeInventory` and stands it on its tile.
