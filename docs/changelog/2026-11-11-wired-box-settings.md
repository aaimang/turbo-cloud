---
title: "Wired box settings read and saved"
date: 2026-11-11
pr: none yet (wiredbench-seams)
breaks: no
who: "plugins that copy, import or export wired (G-Presets); hosts implementing IHotelWired."
---

**What**: `IHotelWired.ReadBoxesAsync` reads every wired box of a room with its settings as stored
(`WiredBoxSettingsSnapshot`: the save packet's fields, with an effect's delay, a condition's
quantifier and a selector's filter and invert by name), as a player who may read the room's wired.
`IHotelWired.SaveBoxAsync` saves such settings onto one box as that player saving it from the box's
editor: the same permission, every check, the same refusals (`WiredSaveResult`), and the stack is
rebuilt. Picks and variable ids are the room's own, so a plugin copying wired from elsewhere maps
them first. Sources are the client's codes (100 picked furni, 200 selector), as a save packet and
a G-Presets file carry them. A snapshot box's settings carry its snapshot as Habbo writes it into
the box's text (`SnapshotText`). `IHotelWired.ReadVariablesAsync` lists a room's variables (id,
name, kind), so a copy can find a variable it made by its name. The room grain gains `GetWiredBoxSettingsAsync` and `SaveWiredBoxSettingsAsync`.

**Action**: none for existing plugins. A host or test double implementing `IHotelWired` adds the
three methods; `Turbo.Testing`'s `FakeWired` holds boxes a test adds (`AddBox`), takes a first save
onto a furni standing in the room whose name is a wired box's (`wf_...`), and stores saves without
running a box's checks (`BoxesOf`).
