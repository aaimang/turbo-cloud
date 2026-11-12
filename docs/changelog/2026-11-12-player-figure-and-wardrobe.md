---
title: "A player's figure and wardrobe"
date: 2026-11-12
pr: none yet (wiredbench-seams)
breaks: no
who: "plugins that dress players (outfit commands, booths, copied looks); hosts implementing IHotelPlayers."
---

**What**: `IHotelPlayers.SetFigureAsync` dresses a player as their avatar editor would: the
hotel's figure policy takes off or swaps what they may not wear, the look is saved, and the room
sees it. Without a gender (a look copied from elsewhere has none) it is the one most of the
figure's gendered pieces are (`FigureGender.Infer` over the figure data), else the player's own. `WardrobeSlots`, `WardrobeAsync` and `SaveOutfitAsync` read the player's wardrobe and save
an outfit into a slot, as the wardrobe's own save does (the same slot range and figure checks).
A saved outfit's figure is now up to 279 characters, as long as a player's own: Habbo looks with
many parts run past the old 100 (migration `LongerOutfitFigures`, which widens
`player_outfits.figure`).

**Action**: none for existing plugins. A host or test double implementing `IHotelPlayers` adds the
four members; `Turbo.Testing`'s `FakePlayers` keeps each `FakePlayer`'s `Figure`, `Gender` and
`Wardrobe`.
