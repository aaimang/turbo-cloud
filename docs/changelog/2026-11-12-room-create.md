---
title: "Plugins create rooms"
date: 2026-11-12
pr: none yet (wiredbench-seams)
breaks: no
who: "plugins that make rooms for players (WIRED sandboxes, imports); hosts implementing IHotelRooms."
---

**What**: `IHotelRooms.CreateAsync` makes a room for a player as the client's "create room" does:
the name and player cap checked, the player's room limit kept, the name filtered. It starts from a
room model, and with a floor plan (the floor plan editor's heightmap) saves that plan over it as
the owner would in the editor, its door on the plan's first tile. It answers `HotelRoomCreated`:
the room, and whether the floor plan was taken. No one is sent into the room; a plugin forwards a
player with `IHotelPlayers.SendAsync` and the client's `RoomForwardMessageComposer`.

**Action**: none for existing plugins. A host or test double implementing `IHotelRooms` adds the
method; `Turbo.Testing`'s `FakeRooms` adds a room and keeps the floor plan as given
(`FakeRoom.FloorPlan`).
