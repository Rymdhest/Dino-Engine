# Gear

What gear looks like: the models, how they're made, how they reach a hero, the ground and the interface. Read `BUILDS.md` for what gear does. This is what it looks like.

Status: **this is the plan.** Each phase below says what has to be true before the next one starts. Where this and the code disagree, the code has moved on and this needs updating.

## The one idea

**An item is a seed, and everything that shows it grows from that seed.** No model is ever stored or sent. The same few numbers make the same mesh on any machine, so one mesh is what's in the hero's hand, what lies on the ground and what the icon is a picture of. They can't disagree, because there is only one of them.

```
          Item (saved, sent)                    what's drawn
  +-----------------------------+
  |  Seed     64 random bits    |      +--> worn:   skinned to the rig, merged into the hero
  |  Base     "Longsword"       | ---> | 
  |  Armour / Evasion / Barrier |  |   +--> ground: the same mesh, laid down
  |  grade, lines (as ever)     |  |   |
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

So there are two enums. `ItemSlot` is what an item is worn as (nine values, with one `Finger`), and it's what affixes and bases talk about. `WornSlot` is a place on a hero (ten values), and it's what `HeroState.Equipped` is keyed by. `Weapon`, `Shield`, `Ring` and `Amulet` are renamed to match the words above.

## Families

Structure in the randomness: you should be able to read a hero from across the screen.

| Family | Looks | Gives | Leans towards |
|---|---|---|---|
| Heavy | Metal: plate, mail, lames, big silhouettes | Armour | Armour, health, block, reduction |
| Leather | Hide: straps, studs, buckles, laces, close fit | Evasion | Evasion, speed, crit, attack |
| Cloth | Robes, hoods, sashes, long lines, trim, charms | Barrier and mana | Mana, barrier, spells, cooldowns |

A family is never stored. It comes from the item's base numbers as three **shares**, so a Brigandine at half armour and half evasion really is halfway: plates riveted to leather. Affixes lean the same way as the look, by weighting what an item can roll, so what you see is what you get. Every line can still appear anywhere.

Cloth giving mana is a rule in `Item.Mods`, worked out from its barrier, so old items get it too and nothing new is saved.

## Geometry

`Dino Engine/Modelling/Procedural/Gear/` is headless and knows nothing of the game: it takes a `GearSpec` and returns a mesh.

It can't touch `TextureGenerator`. Reading any static on that class compiles shaders, which crashes without a GL context, and that would rule out a lab. Materials come through `GearMaterials`, a few integers the client fills in.

**On the body.** Everything is built in rig space around `BodyForm`, an analytic description of the body's surface (a radius by height and angle for each limb). Armour is that surface pushed outwards, with features placed in the surface's own coordinates. So nothing is positioned by eye, it fits whatever `Bulk` a body has, and clipping can be tested as a number.

What the rig decides:

- **Fourteen bones and two weights a vertex.** The torso is a single bone.
- **The run swings the legs ±89°.** So anything long has to be skinned to the thighs: the hem belongs to the legs and the waist to the chest, and a skirt twists as cloth does. Bound to the chest, the knees come straight through it.
- **Limbs are separate rigid pieces.** Whatever a piece covers is left out of the body under it (`BodyRegions`), so skin can't poke through, and there's less to draw. Bare, a hero wears plain undergarments.
- **Characters are 4.7 m tall**, seen from 33 m up at 67°. What's seen in play is masses and silhouette, so that's what gets designed first. Fine detail is for the icon and the character screen.

**Materials.** Tint is vertex colour, so a metal is one material in many colours. `gold`, `copper`, `crystal`, `rustyMetal`, `glass` and `plastic` are declared in `TextureGenerator` but never created: they're all index 0, which is `grain`. A material costs about 64 MB of video memory at 2048², so this adds as few as it can: fully metallic `plate`, `mail` with a real ring pattern, and `gem`. Each needs a porosity line.

**Colour.** Albedos stay deep and saturated, as everywhere else in the game: under a sun of about 13, anything pale goes white.

## On the hero

`Client/Workshop` builds meshes on worker threads and uploads them on the main one, as a boss drops ten things at once and a mesh mustn't cost a frame. A hero is one merged mesh, so one draw call, rebuilt when what they wear changes. `CharacterModelComponent` just gets a new model.

What a hero has on goes out in a reliable `Worn` message keyed by player, as `GroundItem` is keyed by find, to everyone when it changes and to whoever arrives later. It's two numbers a slot. A component would have needed thirty hand-written fields, as a replicated struct can only hold plain numbers.

Models used to be few and live forever. Now every find has its own, so they need a lifetime: counted while in use, kept for a while in case they come back, then freed a frame late, as commands for this frame may still name them. `ModelRenderSystem` and the shadow systems keep a dictionary entry per model for good, and walk it every frame, so those need pruning or the game slowly gets worse the longer it runs.

## On the ground

The same mesh, laid flat. Paired things are drawn as a pair, and a body piece is laid out as a garment. `PickupComponent` carries the look, as it carries everything else a drop needs. A find is scaled to a readable size for the camera, so a ring on the ground is big enough to see. That's a scale on the same model, not a different model.

`CharacterFactory.Find` goes away.

## Icons

A studio like the imposters' one, and for the same reason: draw the model, keep the picture. `TextureStudio` itself can't be reused, as its dilation pass runs 1.5 x resolution full-screen passes per image, for texture filtering an icon doesn't need.

`ItemStudio` keeps one atlas and a queue, and draws a few icons at the start of a frame. Until an icon is ready the cell shows its slot glyph. Same mesh, same material arrays, but its own shading: a warm key, a cool fill, a rim, stepped diffuse and a drawn outline. The outline is found in a finishing pass, from depth and normals, and not by the inverted hull that Guilty Gear uses, as that method breaks on hard edges and blades are nothing but hard edges.

The same studio draws the hero for the character screen.

## The character screen

Rebuilt. The hero stands in the middle in everything they're wearing, with the ten slots around them, the pack beside them, and stats to the side. Items show as their icons, with a tooltip at the cursor that compares against what's worn. With something two-handed held, the off hand shows it's taken. It keeps everything the prototype does: buying, selling, flasks.

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
dotnet "Dino GearLab/bin/Release/net8.0/Dino GearLab.dll" bench            # What a mesh costs to make
```

- **`check`:** the same spec gives the same mesh byte for byte. Nothing is NaN, nothing is degenerate, it stays inside its budgets of triangles and size, bones and materials are valid, and armour stays outside `BodyForm`, in the worst poses of every clip as well as at rest.
- **`variety`:** silhouettes compared pairwise, so that no two of thousands of one kind come out alike, and within a family spread is measured so that "endless" isn't just noise on one shape. Families are checked to be told apart by shape and colour alone.

A lab proves a thing is valid, not that it's good. Looks are judged from sheets and from the real game, at play distance and up close. Nobody but a person can say it looks right.

## The plan

Vertical first: one item all the way through before there are thirty. The riskiest things come early, and every phase ends with something that runs.

| # | Phase | Done when |
|---|---|---|
| 1 | **Rules.** Ten places, two enums, Legs and Hands bases and uniques, families, leaning affixes, cloth's mana, seed and base on items, save version 4, protocol. Refit `balance` for three more items on a hero | `BuildLab check`, `NetLab build`, `loot`, `offering` pass. A copy of a real save loads with nothing lost. The balance table matches its baseline |
| 2 | **Kernel.** `GearSpec`, genes, style, palette, profiles, sections, loft and sweep with bones, `Dino GearLab` with its CPU sheet. Swords as the proving ground | A sheet of 64 swords that look like 64 swords. `check` and `variety` pass for them |
| 3 | **Slice.** Materials, `Workshop` and model lifetime, the sword in the hero's hand, on the ground, and as an icon from `ItemStudio`, across the network | One seed, seen in all three places in a real run, from two clients. No hitch when a boss drops its pile |
| 4 | **Arms.** The other seven weapon kinds, shields and wards, rings and amulets | Each passes `check` and `variety`, and reads at 33 m |
| 5 | **Armour.** `BodyForm`, shells, `BodyRegions`, thigh skinning, then Head, Body, Legs, Feet and Hands in each family, with shares blending between them | Clipping passes in every pose. A hero in each family is told apart at play distance. Seen running |
| 6 | **Character screen.** Hero in the studio, ten slots, icons everywhere, tooltip, merchant | Driven with real mouse messages: equip, unequip, both fingers, two-handed against off hand, buy, sell |
| 7 | **Everyone else.** Bosses in what they really wear, enemies and villagers holding generated gear, the old `WeaponGenerator` gone. Flask and chest icons from the same studio. Docs, AGENTS.md | All labs pass. A long `--bot --sweep --hidden` run shows no leak |

## Decided, and open to change

Choices made without asking, as none of them block anything. Say if any are wrong.

- **The off hand is shields and wards only.** There's no dual wielding in the rules today, and adding it is a build-system change, not a gear one.
- **A unique has one look**, seeded by its id. It's one of a kind, and it means the boss's sword is the sword you're then holding. Everything else is unique per drop.
- **Mana comes on top of barrier for cloth.** Barrier is already the third defence, and removing it would break builds.
- **Two-handed weapons are held in one hand for now.** There's one swing clip, and a real two-handed grip is an animation job of its own.
- **Friends lose their tunic dyes** as a way to tell each other apart, since gear covers them. Dye stays on undergarments only. If that's not enough in play, it needs something of its own, such as a coloured mark.

## Where this came from

- [How I make infinite procedural swords](https://bmaczero.itch.io/icon-machine/devlog/357002/how-i-make-infinite-procedural-swords): tuned ranges not open ones, a mirrored half for balance, and one set of shading rules and one outline to make unrelated parts into one object.
- [Borderlands' parts system](https://borderlands.fandom.com/wiki/Manufacturer): where a part comes from is what you can read off it. Here the family does that job, without the parts being a list.
- [Path of Exile 2's armour bases](https://www.poe2wiki.net/wiki/List_of_body_armours): the same split of armour, evasion and a magical defence, with hybrids between. Nothing found covers how PoE ties that to looks, so the visual rules here are our own.
- [Guilty Gear's toon line techniques](https://docswell.com/s/ASW_Academy/5LVY67-GG-Toonline-Eng): why an inverted hull needs a second set of normals on hard edges, which is why icons find their outline another way.
- Nothing useful was found on fitting procedural armour to a body: what turned up was forum advice about shrinkwrapping in modelling tools. `BodyForm` is our own design, made possible by the body already being analytic.
