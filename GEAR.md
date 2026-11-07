# Gear

What gear looks like: the models, how they're made, how they reach a hero, the ground and the interface. Read `BUILDS.md` for what gear does. This is what it looks like.

Status: built, and looked at by machine. See "Where it stands" at the end for what nobody has judged yet.

## The one idea

**An item is a seed, and everything that shows it grows from that seed.** No model is ever stored or sent. The same few numbers make the same mesh on any machine, so one mesh is what's in the hero's hand, what lies on the ground and what the icon is a picture of. They can't disagree, because there is only one of them.

```
          Item (saved, sent)                    what's drawn
  +-----------------------------+
  |  Seed     64 random bits    |      +--> worn:   skinned to the rig, merged into the hero
  |  Base     "Longsword"       | ---> | 
  |  Armour / Evasion / Barrier |  |   +--> ground: the same mesh, laid down
  |  rarity, lines (as ever)    |  |   |
  +-----------------------------+  |   +--> icon:   the same mesh, in a studio
                                   |
                              GearSpec  (Seed + traits: 3 integers on the wire)
```

## Nothing is a fixed model

A model is never picked from a list, and a shape is never written down as numbers. Generation runs in four layers, and each only reads the one below it.

1. **Prior.** What the base and the family say, as *ranges*. A Stiletto is long and thin and a Kris is wavy, but neither is a shape. Heavy means thick and plated, as a tendency. This is the only hand-written data, and it holds distributions.
2. **Style.** A handful of values drawn once per item and shared by all its parts: heft, angularity, ornament, wear, flare, spikes, curl, and a palette. This is what makes an item look like one thing rather than a pile of unrelated random parts. It's the lesson from every generator looked at: constrained ranges and shared rules are what keep random output good.
3. **Parts.** Which parts exist and what they're like. Choices are weighted by the style, and every part is continuous underneath: a guard isn't one of three guards but a curve with a length, a droop, a flare and a section.
4. **Primitives.** Profiles are splines through random control points. Sections are superellipses and lenses. Surfaces are lofted along spines or offset from the body.

**Each parameter draws from its own named fork** (`genes.Range("blade.length", ...)`), which is the rule `WorldRandom` already has: drawing another number never disturbs the ones after it. A new parameter can be added without re-rolling every item in every save.

Variety is measured, not claimed: see Testing.

## Slots

| Place | Takes | Notes |
|---|---|---|
| MainHand | Weapons | Two-handed ones empty the off hand |
| OffHand | Shields, wards | Not while something two-handed is held |
| Head, Neck, Body, Legs, Feet, Hands | One thing each | Legs and Hands are new |
| Finger1, Finger2 | Rings | A ring is the same item on either |

So there are two enums. `ItemSlot` is what an item is worn as (nine values, with one `Finger`, and `Book`, which isn't worn at all and goes on the bar), and it's what affixes and bases talk about. `WornSlot` is a place on a hero (ten values), and it's what `HeroState.Equipped` is keyed by. `Weapon`, `Shield`, `Ring` and `Amulet` are renamed to match the words above.

## Families

Structure in the randomness: you should be able to read a hero from across the screen.

| Family | Looks | Gives | Leans towards |
|---|---|---|---|
| Heavy | Metal: plate, mail, lames, big silhouettes | Armour | Armour, health, block, reduction |
| Leather | Hide: straps, studs, buckles, laces, close fit | Evasion | Evasion, speed, crit, attack |
| Cloth | Robes, hoods, sashes, long lines, trim, charms | Barrier and mana | Mana, barrier, spells, cooldowns |

A family is never stored. It comes from the item's base numbers as three **shares**, so a Brigandine at half armour and half evasion really is halfway: plates riveted to leather. Affixes lean the same way as the look, by weighting what an item can roll, so what you see is what you get. Anything a slot can have can still roll on it, whatever it's made of. `ItemLook.UniqueGrade` keeps a unique at the top of what a look is grown from, where it's always been: bring it down and every unique looks different.

Cloth giving mana is a rule in `Item.Mods`, worked out from its barrier, so old items get it too and nothing new is saved.

## Geometry

`Dino Engine/Modelling/Procedural/Gear/` is headless and knows nothing of the game: it takes a `GearSpec` and returns a mesh.

It can't touch `TextureGenerator`. Reading any static on that class compiles shaders, which crashes without a GL context, and that would rule out a lab. Materials come through `GearMaterials`, a few integers the client fills in.

**On the body.** Everything is built in rig space around `BodyForm`, an analytic description of the body's surface (a radius by height and angle for each limb). Armour is that surface pushed outwards, with features placed in the surface's own coordinates. So nothing is positioned by eye, it fits whatever `Bulk` a body has, and clipping can be tested as a number.

What the rig decides:

- **Fourteen bones and two weights a vertex.** The torso is a single bone.
- **Legs go a long way in a run.** So anything long has to be skinned to the thighs: the hem belongs to the legs and the waist to what's above it, and a skirt twists as cloth does. Bound to the chest, the knees come straight through it.
- **What's on the body is skinned by how high it is.** Generators just say "on the chest", and `BodyForm.Spread` shares it out between pelvis, spine and chest by `Back`, and neck and head by `Throat`, so a back can bend under armour.
- **Limbs are separate rigid pieces.** Whatever a piece covers is left out of the body under it (`BodyRegions`), so skin can't poke through, and there's less to draw. Bare, a hero wears plain undergarments.
- **Characters are 4.7 m tall**, seen from 33 m up at 67°. What's seen in play is masses and silhouette, so that's what gets designed first. Fine detail is for the icon and the character screen.

**Materials.** Tint is vertex colour, so a metal is one material in many colours. `gold`, `copper`, `crystal`, `rustyMetal`, `glass` and `plastic` are declared in `TextureGenerator` but never created: they're all index 0, which is `grain`. A material costs about 64 MB of video memory at 2048², so this adds as few as it can: fully metallic `plate`, `mail` with a real ring pattern, and `gem`. Each needs a porosity line.

**Colour.** Albedos stay deep and saturated, as everywhere else in the game: under a sun of about 13, anything pale goes white.

## On the hero

`GearWorkshop` builds meshes on worker threads and uploads two a frame on the main one, as a boss drops ten things at once and a mesh mustn't cost a frame. `Client/Wardrobe` is what everything asks: it knows what's lying down, what's being worn, and what's in a picture. A hero is one merged mesh (`Outfit`), so one draw call, rebuilt when what they wear changes. `CharacterModelComponent` just gets a new model.

What a hero has on goes out in a reliable `Worn` message keyed by player, as `GroundItem` is keyed by find, to everyone when it changes and to whoever arrives later. It's two numbers a slot. A component would have needed thirty hand-written fields, as a replicated struct can only hold plain numbers.

Models used to be few and live forever. Now every find has its own, so they have a lifetime: whatever uses one has to keep asking for it every frame, as that's what keeps it, and what nobody has asked for in a while is freed a few frames late, as commands for this frame may still name it. So nothing is counted and nothing can be forgotten about. `ModelRenderSystem`, `CharacterRenderSystem` and the shadow system drop models they didn't draw last time, or their dictionaries would grow for a whole session. The `Gear:` line of a scripted run says how many are alive.

## On the ground

The same mesh, laid flat. Paired things are drawn as a pair, and a body piece is laid out as a garment. `PickupComponent` carries the look, as it carries everything else a drop needs. A find is scaled to a readable size for the camera, so a ring on the ground is big enough to see. That's a scale on the same model, not a different model.

`CharacterFactory.Find` goes away.

## Icons

A studio like the imposters' one, and for the same reason: draw the model, keep the picture. `TextureStudio` itself can't be reused, as its dilation pass runs 1.5 x resolution full-screen passes per image, for texture filtering an icon doesn't need.

`ItemStudio` (`Rendering/Renderers/Studio/`, `Shaders/Studio/`) keeps one atlas and a queue, and draws two icons at the start of a frame. Until an icon is ready the cell shows its slot glyph. Same mesh, same material arrays, but its own shading: a warm key, a cool fill, a rim, stepped diffuse, metal looking at a painted sky, and a drawn outline. The outline is found in a finishing pass, from depth and normals, and not by the inverted hull that Guilty Gear uses, as that method breaks on hard edges and blades are nothing but hard edges. A picture goes to the interface premultiplied and already tone mapped.

The same studio draws the hero for the character screen (`Doll`), with the skinning matrices of the hero as they stand, every frame the panel is open.

## The character screen

`UI/HeroPanel.cs`. The hero stands in the middle in everything they're wearing, drag to turn them, with the ten places around them head to toe, the pack beside them and stats to the side. Things show as their pictures. `Ui.DrawTooltip` is the one description of an item, at the cursor here and over a label on the ground: a picture, what it is, and what it would change. With something two-handed held the off hand shows it, crossed out, and places an item could go light up as it's looked at. A merchant's table takes the place of the sheet.

Nothing about this can be proved by a bot. To drive it with real mouse messages on a hidden run, post `WM_SETFOCUS` to the window first: the interface ignores the mouse unless it thinks it has the focus, and a hidden window never does. Read modifiers from `Context.Modifiers`, never from the mouse event: GLFW asks Windows for those, and a posted key doesn't reach it.

## The pack

**Everything stays where it was put.** `HeroState.Inventory` is `InventorySize` cells with gaps in them, and the only thing that ever moves what's in one without being told where is `HeroState.Sort`, which is only done when asked. Selling leaves a gap. Whatever comes off when something's put on goes into the cell it came from, so the two change places: the one exception is something two-handed taking both hands, where the second thing needs a cell of its own and goes in the first that's free. `HeroState.Move` is how anything moves by hand. It says no and changes nothing if a swap can't go both ways, and `CanMove` tries it on a copy, so what's shown of a move can't disagree with it.

**What's on the cursor is only something the panel has noticed.** `HeroPanel._hand` is where the item really is, and it stays there until it's put somewhere, so closing the panel, falling or losing the server can't lose it. The source cell shows it faintly. `PlanFor` says what a click would do with it on anything under the mouse, and it's both what the caption by the cursor says and what's carried out, so the two can't disagree. A plan that's refused leaves it on the cursor, and letting go of a drag ends it either way.

| On a thing | What it does |
|---|---|
| Left click | Picks it up. Clicking something else puts it there, and whatever's there changes places with it. Dragging does the same |
| Left click on a place on the hero | Lists what could go there, by `SlotPicker`, and dims the rest of the pack. Dragging takes it off |
| Ctrl-click | Wears it, or sells it at a merchant. Takes off what's worn. Buys what's on a table |
| Right click | Wears it, even at a merchant. Takes off what's worn. Buys what's on a table. With something on the cursor, puts it back |
| Shift with either of those | Puts a ring on the second finger |
| Over the hero | Wears it |
| Over a merchant's side | Sells it, from anywhere in it |
| Off the panel | Puts it on the ground beside the hero, by `GroundLoot.Lay` |
| Escape | Puts back what's on the cursor, or closes the list, before it means anything else |
| Ctrl+F | The search box |

Ctrl-clicks and right clicks happen on the press, so a run of them needn't wait for each to be let go. A card in the pack is `loose`, for why: read `TOOLTIPS.md`.

**A merchant's table** (`MerchantTable`) is one per hero and site, and stays as it is too: what's bought leaves a gap, so a double click buys one thing. What's sold goes on it to be bought back at what it paid, so a slip costs nothing. The oldest goes once it's full. It lasts as long as `ServerPlayer.Tables`, which is until they leave the land or the game: what's for sale has always worked that way, and nothing about a table is saved.

**Something put down** has `PickupComponent.Laid`, which leaves out everything that says a find is exciting: the long flight, the sting, the flash, the callout. It can be picked up by anyone like anything else, and is lost on leaving the world like anything else. The drop catcher is `GroundCatcher`, which only stops the mouse with something on the cursor, and only really over the game: losing the focus part way through a drag mustn't count as bringing it to the ground.

**Searching** is `ItemSearch`, which reads an item's own card, so whatever can be read on one can be typed, in any order. It goes by the start of each word, or "ring" finds everything of the Spring. What doesn't match dims where it is, as nothing moves for a search either, and it covers what's worn and what's on a table.

`Dino BuildLab check` holds the rules, including 200,000 random moves with everything counted, and `Dino NetLab pack` holds what reaches a client. To try it in the real game: `--carry N` fills that many cells, `--panel merchant` opens a real table, and `--snap file` takes a picture whenever the file appears, as the window can't be read from outside.

## Saves

Testers carry saves across builds, so this can't lose anything. `SaveGame` goes to version 4:

- Slots are renamed in items and in `Equipped`, before anything is parsed, since an unknown enum name throws and a save that can't be read starts a new journey.
- An item with no seed gets one hashed from what it is, and its base is read from its name, so an old sword keeps a look of its own, and the same one every time.
- `save.json.before-gear` is kept beside it, as `.before-talents` was.

Looks aren't saved, so changing a generator changes what old items look like. That's accepted: it's how rebalancing an affix already works.

## Testing

`Dino GearLab` is headless, as `WorldLab` is.

```
dotnet "Dino GearLab/bin/Release/net8.0/Dino GearLab.dll" check            # Every kind, thousands of seeds: is it valid?
dotnet "Dino GearLab/bin/Release/net8.0/Dino GearLab.dll" variety          # Are they really different?
dotnet "Dino GearLab/bin/Release/net8.0/Dino GearLab.dll" sheet --kind sword  # A contact sheet, drawn on the CPU
dotnet "Dino GearLab/bin/Release/net8.0/Dino GearLab.dll" dress --heavy 1  # Whole people in one family's gear
dotnet "Dino GearLab/bin/Release/net8.0/Dino GearLab.dll" bench            # What a mesh costs to make
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" gear               # That everyone sees the same thing, with clients on the other end of a line
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" pack               # What's carried, across a line: see The pack
```

`Dino BuildLab check` holds the rules: places, families, leaning, what a look is packed into, and that old saves come across, real ones included if there are any on the machine. To see it in the real game: `--wear heavy` (or `leather`, `cloth`) puts something of one family in every place, and `--panel hero` opens the screen.

- **`check`:** the same spec gives the same mesh, vertex for vertex. Nothing is NaN, nothing is degenerate, and it stays inside its budgets of triangles and size.
- **`variety`:** many of one base are compared, as that's the hard case: a leather tunic against every other leather tunic. Each is drawn as its icon shows it, and two are alike if they share both an outline and what's inside it, as armour has to fit a body and only so much of it can show at the edge. It fails if more than one pair in 2,000, picked at random, look alike. Run it with `--heavy 1`, `--leather 1` and `--cloth 1`. A fall in any of these means a kind needs more parameters: raising the threshold is the wrong fix.

A lab proves a thing is valid, not that it's good. Looks are judged from sheets and from the real game, at play distance and up close. Nobody but a person can say it looks right.

## Where it stands

All seven phases of the plan were built, in the order rules, kernel, a single sword all the way through, the other held things, armour, the screen, everyone else.

**What's been proved, and how.** A six minute bot sweep of a whole world made 82 models and ended with 13 alive, with nothing on the main thread costing more than 0.77 ms in any frame, and no faults. Sixteen kinds pass `check` and `variety` in all three families. The rules and saves pass `BuildLab check`, with every real save on the development machine loaded. Two clients agree on every look (`NetLab gear`). Clicks and a tooltip on the screen were driven with real mouse messages: equipping, taking off, the doll re-dressing itself and the numbers changing. Balance was refitted to its table for the three extra things a hero wears.

**What nobody has judged.** No person has seen any of this. Every look was tuned from CPU sheets and stills, so whether it's good is still open, and their opinion outranks every number here. In particular: how a long skirt behaves in a run (the only run looked at had a hero in deep reeds), whether heroes in the game's own light read as their family as well as they do on a sheet, and whether leather has become too colourful for what it is.

**What wasn't built.**

- Bosses don't show what they wear. They fight with real items and drop them, but they're drawn as they were. Every enemy and villager does carry a generated weapon now.
- Icons for anything that isn't gear, such as flasks. The studio will take any model.
- Dragging things between places. It's all clicks.
- Levels of detail for gear. A hero in everything is about 14,000 triangles on one draw call, which is fine for five heroes, and nothing has needed less yet.

## Decided, and open to change

Choices made without asking, as none of them block anything. Say if any are wrong.

- **The off hand is shields and wards only.** There's no dual wielding in the rules today, and adding it is a build-system change, not a gear one.
- **A unique has one look**, seeded by its id. It's one of a kind, and it means the boss's sword is the sword you're then holding. Everything else is unique per drop.
- **Mana comes on top of barrier for cloth.** Barrier is already the third defence, and removing it would break builds.
- **Friends lose their tunic dyes** as a way to tell each other apart, since gear covers them. Dye stays on undergarments only. If that's not enough in play, it needs something of its own, such as a coloured mark.

## Where this came from

- [How I make infinite procedural swords](https://bmaczero.itch.io/icon-machine/devlog/357002/how-i-make-infinite-procedural-swords): tuned ranges not open ones, a mirrored half for balance, and one set of shading rules and one outline to make unrelated parts into one object.
- [Borderlands' parts system](https://borderlands.fandom.com/wiki/Manufacturer): where a part comes from is what you can read off it. Here the family does that job, without the parts being a list.
- [Path of Exile 2's armour bases](https://www.poe2wiki.net/wiki/List_of_body_armours): the same split of armour, evasion and a magical defence, with hybrids between. Nothing found covers how PoE ties that to looks, so the visual rules here are our own.
- [Guilty Gear's toon line techniques](https://docswell.com/s/ASW_Academy/5LVY67-GG-Toonline-Eng): why an inverted hull needs a second set of normals on hard edges, which is why icons find their outline another way.
- Nothing useful was found on fitting procedural armour to a body: what turned up was forum advice about shrinkwrapping in modelling tools. `BodyForm` is our own design, made possible by the body already being analytic.

## Books and what's thrown

A book is gear like anything else: one mesh from `BookGenerator`, by `GearKind.Book`, in the pack, on the bar and on the ground. `Item.LookSeed` says how it looks, and nothing written in it does.

The one that's being read shows open, wherever it shows (`BookPanel.IsOpen`, `Ui.DrawItem`). That's the only other mesh anything has: `GearKind.OpenBook` draws from the genes of `Book`, by the same names, so it's the same book laid open and `check` fails if it isn't. No item is ever that kind, so nothing but `Wardrobe.Picture` asks for it, and until there's a picture of it the shut one shows.

A page is `GearKind.Page` and a sticker `GearKind.Sticker`, one mesh each like anything else. Nothing about a look says what's on one, so `Ui.DrawPlaces` puts what matters on the picture: which kind of page, and how many places something has with how many taken.

A hero with nothing that shoots throws what's in their hand, and that's the same mesh again: `ThrownComponent` carries the weapon's seed and traits, and `GhostPresenter.Tumble` keeps asking for it, as anything using a model from `GearWorkshop` has to. Something with no item to show, such as an enemy whose sword is only a word, is thrown as `Thrown.Of` says, from one model for each kind of weapon (`CharacterFactory.ThrownThing`).

## Adding to it

- **A new base:** a line in `ItemGenerator.Bases` and a line in `ItemLook.Forms`. `check` fails without the second. Base numbers are only ever said between two copies of one build, so bases can go anywhere.
- **A new parameter:** draw it from a name of its own with `genes`. Nothing else moves, so old items keep their look.
- **A new part:** it goes behind `cut.Likely`, which is how it comes to belong to a family.
- **A new kind of thing:** a value in `GearKind`, a case in `GearForge.Make` and `GearDisplay`, and a line in `Outfit.Dress` for where it goes.
- **A new material:** it costs about 64 MB, and needs a line in `SetPorosity` and in `BindGearMaterials`. Most new looks are a colour in `GearPalette`.

Changing anything generators read changes how old items look, which is accepted. Nothing about a look is saved but a seed.
