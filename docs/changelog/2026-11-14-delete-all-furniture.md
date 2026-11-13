---
title: "Delete everything in a player's inventory"
date: 2026-11-14
pr: none yet (wiredbench-seams)
breaks: no
who: "plugins that clear a player's inventory; hosts implementing IHotelInventory."
---

**What**: `IHotelInventory.DeleteAllFurnitureAsync(player, cause, ct)` deletes every furni in a
player's inventory that is theirs to keep (rentals and other expiring furni stay, as in
`FurnitureOfAsync`) and answers how many. The rows are deleted in batches of 100 in one
transaction each, through the inventory grain's own consume, so they are recorded in the movement
log under the plugin's cause and the player's client sees each leave its list. Furni in rooms or in
a trade are not touched.

**Action**: none for existing plugins. A host or test double implementing `IHotelInventory` adds
the method; `Turbo.Testing`'s `FakeInventory` implements it.
