# Valheim Crafting Search

BepInEx mod for [Valheim](https://www.valheimgame.com/) that adds a search
box and a sort button to every crafting window - workbench, forge, cauldron,
mead ketill, the upgrade tab and the rest - so you can find a recipe without
scrolling through the whole list.

## What it adds

- **Search box** above the recipe list: type part of a name and the list
  narrows down as you type. It ignores letter case and most accents (`zelazo`
  finds `Żelazo`), and clears when you close the window. While you type,
  keys like E or Tab don't close the window and your character doesn't move -
  the same way the game handles typing in chat.
- **Sort button** next to the Craft / Upgrade tabs, cycling through
  **Default → A-Z → Z-A → Type → Weight**. Recipes you can craft right now
  still come first, like in the game.

## Built on the game's own sorting

Valheim already sorts crafting lists - most players just never find it: the
console command `sortcraft [Original|Name|Type|Weight]` (no cheats needed)
saves a sort method on your character. The sort button uses exactly that
setting, so the button and the console command always agree. The game has no
Z-A order; the mod adds it on top, keeping the game's own ordering rules
(craftable first, then recipe group).

## Requirements

- Valheim (tested on 1.0.15)
- [BepInEx](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/) 5.4.x

## Installation (players)

1. Install BepInEx for Valheim if you haven't already (see link above, or
   use [r2modman](https://valheim.thunderstore.io/package/ebkr/r2modman/)).
2. Download `CraftingSearch.dll` from the
   [latest release](../../releases/latest).
3. Drop it into `<Valheim install folder>\BepInEx\plugins\CraftingSearch\`.
4. Launch the game and open any crafting station.

## Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) (or newer)
and a local Valheim install with BepInEx installed.

```bash
git clone https://github.com/Ab5oluteZer0/valheim-crafting-search.git
cd valheim-crafting-search
dotnet build -c Release -p:ValheimPath="C:\Path\To\Valheim"
```

If you don't pass `-p:ValheimPath`, the build looks for a `VALHEIM_PATH`
environment variable, then falls back to the default Steam location
(`C:\Program Files (x86)\Steam\steamapps\common\Valheim`).

The build automatically copies the built DLL into
`<Valheim>\BepInEx\plugins\CraftingSearch\` for quick in-game testing.

## Notes on how it works (and a few gotchas found along the way)

- Filtering and Z-A are applied right after the game builds and sorts the
  list (`InventoryGui.UpdateRecipeList`), so the game's own selection and
  gamepad navigation keep working on the filtered list.
- While the search box has focus, the mod reports it through
  `Chat.HasFocus()` - the game already skips inventory hotkeys and character
  input while chat is focused, so no key blocking of its own is needed.
- The sort button is a copy of a game button. Copies keep the game's
  `Localize` component, which rewrites the label (here: "Place stacks")
  every time the window opens - it has to be removed from the copy.
- Controls are positioned relative to the game's own UI elements (the recipe
  list, the Upgrade tab, the Craft button), not fixed pixels, so they follow
  the game's UI scale.

## License

MIT - see [LICENSE](LICENSE).
