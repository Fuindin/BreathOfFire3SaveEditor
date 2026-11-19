# Breath of Fire III Save State Editor

A Windows Forms editor for **Breath of Fire III (USA, SLUS-00422)** save states made by
**DuckStation** (`*.sav`). It is styled after the game's own menus: wooden planks, khaki
bevelled windows, charcoal panels and the orange `EXP / LV / HP / AP` captions.

## Download

Grab **`BreathOfFire3SaveEditor.zip`** from this repository, extract it anywhere, and run
`BreathOfFire3SaveEditor.exe`. It needs the
[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (Windows x64).
Keep the `portraits` folder next to the exe.

## What it edits

**Party** (all eight records: Ryu, Nina, Garr, Teepo, Rei, Momo, Peco, Whelp)

| Editable | Shown read-only |
|---|---|
| Name (5 characters), LV, EXP | Master, apprentice level, fatigue |
| HP / max HP, AP / max AP | Willpower, surprise, reprisal, critical, dodge, accuracy |
| Base PWR, DEF, AGL, INT | Stat growth modifiers |
| Weapon, shield, helmet, armour, 2 accessories | Resistances, learned abilities |

The equipment lists only offer what that character can equip, using the equip masks
from the game's own item tables. Next to each stat the editor shows the total the game
uses, so you can see what gear adds.

**Inventory**: Zenny, and the four bags (Items, Weapons, Armor, Options), each up to 128
entries with quantities of 1-99. Key items are listed read-only.

## Using it

1. In DuckStation, save a state while the game is running (not on the title screen).
2. Open it here (`File > Open`, it starts in
   `D:\Emulation\PS1\duckstation-windows-x64-release\Saves`). The last file reopens on start.
3. Make changes and press `Ctrl+S`.
4. Load the state again in DuckStation to see them.

Each save first copies the previous file to `<name>.sav.editor.bak`. DuckStation's own
`.bak` is left alone.

### Things worth knowing

* **EXP vs LV.** Raising EXP does not raise the level straight away; the game levels the
  character up (with stat gains) after the next battle. Setting LV directly changes only
  the number, not the stats.
* **Totals.** The game stores both base stats and totals. The editor rewrites the totals
  from base + weapon ATK + armour DEF - equipment weight, and keeps any accessory bonus
  (for example Titan Belt +10 PWR) while the same accessories are worn. If you swap an
  accessory here, its bonus appears once you next change equipment in the game's menu.
* **Gear and the bag.** Changing equipment here does not move items in or out of the bag.

## Portraits

Pictures are read from `portraits\<Character>.png` next to the exe (`Ryu.png`, `Rei.png`,
`Teepo.png` are included, cropped from the in-game Items screen). Drop in `Nina.png`,
`Garr.png`, `Momo.png`, `Peco.png` or `Whelp.png` to fill in the rest; characters without
one get a coloured crest.

## How it works

### DuckStation state (`SaveFormat/DuckStationState.cs`)

A `DUCC` header (title, serial, media path, screenshot and data block descriptors), an
RGBA screenshot and a Zstandard-compressed machine state. Main RAM is found inside the
`Bus` section by the kernel's exception handler stub, which always sits at RAM address
`0x80`. On save only the data block is recompressed; the header, media path and screenshot
are copied as they were.

### Game data (`Game/`)

The game keeps its memory card block in RAM. In the USA release:

| What | Address | Notes |
|---|---|---|
| Party records | `0x80144968` | 8 x `0xA4` bytes |
| Inventory | `0x80144F50` | Zenny, then id and quantity tables, key items |

The party record layout is documented at the top of `Game/Bof3Character.cs`. If the
records are not at the USA address, the editor searches RAM for eight valid records ending
in Whelp's.

The item, weapon, armour and accessory tables in `Game/Bof3Data.cs` (names, ATK/DEF,
weight, equip masks, prices) were read from the game's own tables in RAM. Ability, key item
and master names were cross-checked against
[glitch-in-the-herring's BoF3 memory card editor](https://github.com/glitch-in-the-herring/bof3-save-editor),
whose save layout notes this editor's offsets agree with.

## Building

.NET 10 SDK, Windows. Open `BreathOfFire3SaveEditor.slnx` in Visual Studio or run
`dotnet build`. Uses the `ZstdSharp.Port` NuGet package.
