---
title: "Clothing a player may wear is shown as theirs; a plugin's saved outfit reaches the client"
date: 2026-11-13
pr: none yet (wiredbench-seams)
breaks: yes
who: "plugins or hosts that replace IPlayerClothingService."
---

**What**: the client's avatar editor takes off any sold piece the player is not told they own,
even when the hotel lets them wear it. `IPlayerClothingService.GetShownAsync` is what the client
is told at login and after every change (`SendOwnedAsync`): what the player owns and, for a player
with `figure.any`, every sold set in the figure data. `GetOwnedAsync` is still only what they own.

`IHotelPlayers.SaveOutfitAsync` now sends the player their wardrobe (`WardrobeMessageComposer`)
after a save. The client asks for its wardrobe once and keeps it, so an outfit saved by a plugin
used to show only after logging in again.

**Action**: a replacement `IPlayerClothingService` adds `GetShownAsync`; returning
`GetOwnedAsync`'s answer keeps the old behaviour.
