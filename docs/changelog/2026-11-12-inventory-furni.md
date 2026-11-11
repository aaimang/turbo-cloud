---
title: "A player's furni by item id"
date: 2026-11-12
pr: none yet (wiredbench-seams)
breaks: no
who: "plugins that place or point wired at furni a player owns; hosts implementing IHotelInventory."
---

**What**: `IHotelInventory.FurnitureOfAsync` lists the furni in a player's inventory by item id
and definition (`HotelOwnedFurni`), rentals and other expiring furni left out, and
`IHotelInventory.GrantFurnitureItemAsync` grants one furni as `GrantFurnitureAsync` does and answers
the item id it made.

**Action**: none for existing plugins. A host or test double implementing `IHotelInventory` adds the
two methods. `Turbo.Testing`'s `FakeInventory` now keeps each held furni as an item
(`NextItemId`, `Take`); `Hold`, `Held` and the grants behave as before.
