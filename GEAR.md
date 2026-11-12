# Gear

What gear looks like: the models, how they're made, how they reach a hero, the ground and the interface. Read `BUILDS.md` for what gear does. This is what it looks like.

Status: rebuilt on 2026-11-12, and looked at by machine and in stills from the real game. See "Where it stands" at the end for what nobody has judged yet.

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

1. **Prior.** What the base says, as ranges and a making: `ItemLook.Forms` gives each base a `GearForm` (size, slenderness, curve, cover, fancy, wear) and, where its name says, a `GearMake` (a Mail Coif is mail, a Gambeson quilted). This is the only hand-written data.
2. **Look.** `GearLook` is drawn once per item and shared by all its parts: its families' shares, craft, ornament, wear, heft, angularity, flare, spikes, curl, a motif (plain, rope, scroll, facet, fang, flute), whether it's legendary, and its colours (`GearColours`). This is what makes an item one thing rather than a pile of random parts.
3. **Parts.** Which parts exist and what they're like, weighted by the look (`GearLook.Likely`, `Blend`). Every part is continuous underneath: a guard isn't one of three guards but a curve with a length, a droop, a flare and a section.
4. **Primitives.** `GearMesh` lofts, sweeps, lathes, bands of crisp colour (`Bands`), plates with a thickness and rolled edges (`Plate`), straps that follow a surface (`Strip`), helical wraps, rivets, buckles and blocks (`GearMesh.Shapes.cs`), and `GearKit`'s finials, fangs, scrolls, settings, tassels and trophies.

**Each parameter draws from its own named fork** (`genes.Range("blade.length", ...)`), which is the rule `WorldRandom` already has: drawing another number never disturbs the ones after it. A new parameter can be added without re-rolling every item in every save, and a part gets a `Genes.Part` of its own so one part can't move another.

Variety is measured, not claimed: see Testing.

## Slots

| Place | Takes | Notes |
|---|---|---|
| MainHand | Weapons | Two-handed ones empty the off hand |
| OffHand | Shields, wards, or a second one-handed weapon | Not while something two-handed is held. A weapon here is held in the left hand (`HumanoidGenerator.SecondHand`), and only goes here when put here |
| Head, Neck, Body, Legs, Feet, Hands | One thing each | |
| Finger1, Finger2 | Rings | A ring is the same item on either |

So there are two enums. `ItemSlot` is what an item is worn as (nine values, with one `Finger`, and `Book`, which isn't worn at all and goes on the bar), and it's what affixes and bases talk about. `WornSlot` is a place on a hero (ten values), and it's what `HeroState.Equipped` is keyed by.

## Weapons

| Kind | Hands | Made by | Notes |
|---|---|---|---|
| Sword | One or two by base | `SwordGenerator` | Greatsword, Claymore, Flamberge and the Executioner's Sword take two |
| Axe | One or two by base | `HaftGenerator.Axe` | Felling Axe, Greataxe, Bardiche, Champion Axe take two |
| Hammer | One or two by base | `HaftGenerator.Hammer` | Smith's, Horseman's, War Pick and Knight's are one-handed, Maul and up two |
| Dagger | One | `SwordGenerator` | |
| Spear | One | `HaftGenerator.Spear` | A javelin, thrown point first (`GhostPresenter.Tumble`) and usable with a shield |
| Bow | Two | `BowGenerator` | |
| Staff | Two | `CasterGenerator.Staff` | A caster's: spells, not attacks |
| Wand | One | `CasterGenerator.Wand` | A caster's |

Whether a base takes two hands is on the base (`BaseItem.TwoHanded`), and `ItemGenerator.TakesTwoHands` is the only thing anything asks. A two-handed sword, axe or hammer is a `Greatsword`, `Greataxe` or `Greathammer` to the forge and is carried as one (`SkillMotions.CarryOf`). Old saves are brought across by `SaveGame.Resay`: maces became hammers, and the Boar Spear, Partisan and Glaive became the Hunting Spear, Winged Spear and Pilum with their damage scaled to one hand (`ItemGenerator.Renamed`). `BuildLab check` holds the grips, the pace of two hands against one, and a version-13 save coming across.

What every weapon has: a blade or a head grown from a family of outlines (tapering, parallel, leaf, cleaver; bearded, bardiche, double; flat, crowned, toothed, beaked), fullers and ridges as real section geometry with crisp edges where the polish stops, a guard or socket or langets, a grip wrapped in leather or wire or cord over risers, and a pommel or butt, all from the look. Rust, nicks and runes come from wear and power; etched and gilt panels from ornament.

## Families and makings

Structure in the randomness: you should be able to read a hero from across the screen.

| Family | Looks | Gives | Leans towards |
|---|---|---|---|
| Heavy | Metal: plate, scale, lamellar, mail, and quilting under them | Armour | Armour, health, block, reduction |
| Leather | Hide: soft, hardened (boiled) or studded, with straps, buckles and laces | Evasion | Evasion, speed, crit, attack |
| Cloth | Robes, hoods, sashes, long lines, trim, charms; fine cloth is silk | Barrier and mana | Mana, barrier, spells, cooldowns |

A family is never stored. It comes from the item's base numbers as three **shares**, so a Brigandine at half armour and half evasion really is halfway: plates riveted inside a cloth or leather cover. How a piece is made is its **making** (`GearMake`): a base's name can say it, and otherwise `ArmourKit.Choose` picks it from the shares and the craft (fine heavy work leans to plate, poor heavy work to quilting, and so on).

The making decides what every part of a piece is made of, through the roles in `ArmourKit`: its shell, the shell in its own shadow, what shows under it, its edging, what holds it on (always leather), its fittings (always metal) and its trim. So a belt is always leather and a buckle always metal, whatever else is drawn.

| Making | Body | Legs | Feet | Hands | Head |
|---|---|---|---|---|---|
| Plate | Cuirass with keel, fauld, tassets, lamed pauldrons, couters, gorget | Cuisses, poleyns with fans | Greaves, lamed sabatons | Gauntlets with finger lames | Helms |
| Mail | Hauberk, often a surcoat | Chausses, cops or a garter | Mail boots | Mail mitts | Coif |
| Scale | Coat of scales, laced front, mirrors | A panel down each thigh | Boots | Gloves | Helms |
| Lamellar | Rows of plates laced in colour, skirt and all | Rows down each thigh | Boots | Gloves | Helms |
| Brigandine | Riveted cover over mail | Riveted panels | Boots | Gloves | Helms |
| Padded | Quilted coat with toggles | Quilted, with knee pads | Boots | Gloves | Arming cap |
| Hide | Jerkin, laced, often over a shirt | Trousers, laced or patched, cross-gartered | Boots, two-tone, laced or strapped | Gloves, laced, contrast cuffs | Hood |
| Hardened | Boiled plates over a tunic | Guards (a plate or splints), cops | Boots | Bracers, laced | Cap of panels |
| Studded | Studded coat | Studs or a studded panel | Boots | Studded gloves | Cap |
| Cloth, Silk | Robe, over-robe, tabard or stole, sash | Trousers, wrapped or not | Shoes | Gloves, wound | Hood, kasa or circlet |
| Wraps | | | Sandals and wraps | Wound cloth | Kasa or hood |

Cloth giving mana is a rule in `Item.Mods`, worked out from its barrier, so old items get it too and nothing new is saved.

## What the climb shows

**The higher an item's level, the more powerful it looks**, so that a glance tells someone starting out from someone who's been far, and a hero looks more and more powerful as their gear goes up. An item's level goes into its look as one of 16 steps (`ItemLook.LevelStep`, `GearSpec.Level` from 0 at the first level to 1 at the last), and `GearLook.Power` is what every part reads of it. Rarity then says how fine a piece is for its level.

- **Every part reads its base's form through the climb** (`GearLook.Climbed`): bigger and covering more, finer and less worn the higher up, and the reverse low down, within the base's own range. So a level-100 Iron Helm is more of a helm than a level-8 one, and a blade longer and cleaner.
- **Craft, ornament and flare rise with it and wear falls**: low on the climb things are plain, worn and patched, rusted on a blade; high they're clean, flared at their ends and worked.
- **What it's made of follows** where a base doesn't say: heavy goes from quilting and mail to plate, scale and lamellar, leather from raw hide to hardened and studded, cloth from plain to silk (`ArmourKit.Choose`). Most bases say, so the rest of this is what carries a hero from the last new base, at about level 15, to level 100.
- **Its colours climb**: iron low and bright or blued steel high; undyed wool and linen with a darker shade of themselves as trim low, deep dyes and contrasting trims high (see How a look's coloured).
- **Its silhouette grows**, as that's what's seen from the camera: shoulders go from bare to something on nearly everything and pauldrons from small to broad in more lames; collars and fur come in; a cape comes in a third of the way up and is nearly always there near the top, down to the knees; a helm grows a plume, horns or wings; a mail coif is worn under a helm high up, with its mail hanging from the helm's rim; a hide tunic gets a jerkin of a second hide over it and a robe an over-robe, a tabard or a stole; a medallion is likelier.

A base can't be told from the climb, though: a Leather Tunic stays a tunic. `GearLab dress --levels 0,0.5,1` shows the same pieces up the climb side by side, and the real game `--wear heavy --wearlevel N --panel hero`.

## How a look's coloured

**Everything is coloured from things that could really be worked** (`GearSwatches`): metals (iron to gold), tanned hides (tan to smoked, and rarely dyed), what dyers had (undyed, woad, madder, weld, walnut and the rest, with kermes, indigo and lichen purple for the finest), woods, furs and stones, each with how dear it is. A look draws from these and never from the whole wheel, which is what makes a piece look made of something rather than painted.

**Restraint is a rule, not a taste.** Most cloth is its own colour or a homely dye, and a strong dye is a chance that rises with the climb and with how fine the work is (and is likeliest on a robe), so that low down nearly everything is undyed and its trim a shade of itself; most hide is a natural brown, any of them about as likely; padding is quilted from undyed cloth; and a trim goes with its dye as a neighbour on the wheel or a plain one far more often than as an opposite, as a hero wears five pieces from five hands and their colours don't agree. A person called the old gear "RuneScape with extra colour", and this is the answer to that.

**Nothing a hero wears is in the Reich's colours** (`LORE.md`). Black iron, oxblood and brass are its own, and `GearSwatches.IsReichs` says what counts. There's no black dye, no red hide and no brass, and every colour asked for goes through `GearColours.Of`, which lightens it just out of the Reich's if a shade or a wear darkened it in (`ClearOfReich`): a worn iron's shadow stops before black iron, a red's fold before oxblood, a dull gold's before brass. `GearLab looks` holds it on what's made.

**A colour asked for is an albedo**, and it's divided by what its material's texture averages (`GearMaterials.AlbedoOf`), as the shader multiplies the two: so what's asked for is what's seen. A picture drawn with no textures multiplies it back (`SoftRender`, `PoseRender`).

**Crafting will one day choose for a person.** `GearTailoring` on a `GearSpec` names a metal, a fitting, a hide, a dye, a trim, an ornament and a wear, each a place in a swatch list or along a scale, and 0 leaves it to the seed. Nothing a hero has sets any of it yet, and everything left at 0 is drawn exactly as it would be, so a piece keeps its look until someone chooses otherwise. That's why **a new swatch goes on the end of its list**: tailoring names them by place. The land's people are dressed in it already (`GearTailoring.Named`, see The land's people).

**The Reich's own wear its livery**, and only they do: `GearTailoring.Reich` dresses a piece in black iron, brass, a charcoal greycoat with oxblood at its edges and black and oxblood leather, the same colours as its beasts' harness (`SaurianHarness`), never cleared out of the Reich's. Nothing a hero can have says so.

## Fit

Pieces are made one at a time and never know what else is worn, so the only thing keeping them apart is `GearFit`: a band for every place, how far out from `BodyForm` its pieces may come, region by region. Every generator stays inside its own band, and the bands are arranged so that what's worn over something is always outside it:

- Below the knee a boot goes over the leg's covering (`LegsUnder` inside `BootInner`), and a knee's plate comes down over a boot's top, which stays inside `BootTopOuter`.
- At the waist the body's piece goes over the legs' seat (`WaistUnder` inside `BodyBase`), and a skirt clears both legs in whatever they have on: its hem is the ellipse round both legs' `LegsShell` (`HemAt`), deep enough that its curve at the side is no tighter than a leg's, or it cut into them at the front corners.
- On the forearm a glove's cuff goes over a fitted sleeve, and a belled sleeve is all the way out before a cuff can begin (`CuffReach`).
- At the neck a helm's or a hood's neck tucks inside the collar (`NeckTuck` inside `CollarInner`); anything wider stays above `HelmLow`, and below a shoulder's plate's top nothing of the head's comes across further than `HeadSide`, where a shoulder's plate begins (`PauldronIn`). A great helm sits above the shoulders, over the mail on the chin, as they were worn.
- Between the legs, which stand too close for everything on both to fit, each leg's things are drawn in flat short of the body's middle, each layer a little shorter than the one it goes over (`SideCover`, `SideWraps`, `SideBoot`, `SideStrap`), so the order things are worn in holds there too. Only surfaces with no thickness are flattened; plates and straps are laid out short of it in the first place, as flattening folded them onto themselves.

What's held and hung keeps clear too:

- **A shield** is held by a grip in the middle of the fist, `GripPast` past the wrist, and its board lies along the back of the forearm `ShieldStandoff` off its middle, over anything a glove or a sleeve puts there (a gauntlet's cuff comes to 0.29), from the grip out to `ShieldArmRoom` either side. The grip is a bar across the fist on posts from the board, and a shield without a boss has a strap round the forearm above it, loose enough for what's worn on it. A board that went through the arm was the first thing a person saw.
- **A ward** isn't strapped on at all: it's held up out of the fist as a weapon is (`HumanoidGenerator.SecondHand`, `Carry.Ward`, `ShieldGenerator.IsWard`), its head on a stem long enough to clear the fist and the forearm with what hangs from it.
- **A necklace as worn** isn't the loop it's shown on alone (`Outfit.Necklace`): its pendant hangs at the breastbone clear of whatever's there, every part of it, and its chain is two strands laid over the body's piece, read from a `FrontField` its triangles are drawn into, which go in under what's round the neck, as a chain round a neck goes under a collar, since every body's piece rises up the neck there. On a bare chest the loop goes on round the neck over the shoulders' slope. A loop that only moved forward cut through the neck of anything deep.

`GearLab fit` measures every vertex of every piece against its bands across thousands of specs and fails on any that leaves them, every shield and ward against the forearm it's on, and every necklace worn over a sampled body or a bare chest. It found a body's surface normal pointing into the body (capes fell inside, buckles faced in, plates showed their undersides), knee and elbow fans lying flat, brims drooping onto the shoulders and two boots meeting between the knees, so keep it passing.

## Legendaries

A unique has one look, seeded by its id, and it must never look like anybody's. A legendary piece is a master's and a lord's work: its craft and ornament are full, its main metal is often blued, silvered or gilt, its edges are rolled in gold or silver and its fittings and trims are of the same (`ArmourKit.Gilt`), and every kind adds what nothing plain has: stones on pauldrons, helms, gauntlets and boots' toe caps, gilt scrollwork on a breastplate, a rosette on the knee, a medallion with a lit stone, a crown or a hood's circlet, runes lit in its power's colour on blades, bands and shields, filigree and halos on jewellery and wards, a beast's teeth hung from a shield's foot. `GearLab looks` holds that on every kind, legendary pieces carry several times the gold, silver, stone and light of plain ones, and that even the plainest tenth of them carries more than a plain one does.

## Shields, wards and jewellery

**A shield** is an outline in one space (a widest point and a fall either side, convex to a point for a heater or a kite, round for a buckler, tall for a tower) and a bow across it, and its making is its face: boards, leather over wood, or steel. Heraldry is laid on as fields of their own with exact edges, never colour smeared across a mesh, in two tinctures, one light and one dark. **It never makes a cross, never puts white and red together and never uses black** (`ShieldGenerator.Tinctures`). Its charges are a Defender's: a beast's fang, three claws, a lozenge, a ring, three roundels. Rims, bosses, studs in rings, nailed bands on a tower and straps behind all follow the making.

**A ward** is a caster's off hand, held up out of the fist like a lantern: withies twisted into a hoop with a charm hung on two cords, an open ring of metal with lit runes holding a stone on a bar across it, or a cage of bands round a stone set at even turns. Never spokes all round, as a wheel of them reads as a sun wheel.

**A ring** is a band (flat, rounded, wire, twisted, or a signet's swelling shoulders) and what sits on it: a stone in a bezel or claws, a cluster, two or three in a row, a beast's fang, or a signet's face with a stone or something raised. **An amulet** is a chain of real links or a thong, and a medallion, a cut stone, a bound fang or a pierced charm of carved bone. Worn, its chain is laid over what's on the chest (see Fit).

**A hand** is closed round what it holds: the back and the palm out to the knuckles, four fingers each curled down from there and back under the palm, and the thumb across their front, so it reads as a hand from any side and in a picture. A gauntlet's fingers are faceted plates.

## Geometry

`Dino Engine/Modelling/Procedural/Gear/` is headless and knows nothing of the game: it takes a `GearSpec` and returns a mesh. It runs on worker threads, so it can't touch anything shared, and it can't read `TextureGenerator`, as any static on that class compiles shaders. Materials come through `GearMaterials`, a few integers the client fills in.

**On the body.** Everything is built in rig space around `BodyForm`, an analytic description of the body's surface. Armour is that surface pushed outwards, with features placed in the surface's own coordinates, so nothing is positioned by eye and fit can be tested as a number (see Fit).

What the rig decides:

- **Fourteen bones and two weights a vertex.** The torso is a single bone.
- **Legs go a long way in a run.** So anything long below the hips is skinned to the thighs (`BodyForm.Skirt`), and a sleeve, a leg and a knee's plate bend with their own halves.
- **What's on the body is skinned by how high it is.** Generators just say "on the chest", and `BodyForm.Spread` shares it out between pelvis, spine and chest, and neck and head. A shoulder's plate is skinned partly to the chest and partly to the arm by where it is (`GearFit.Shoulder`).
- **Limbs are separate rigid pieces.** Whatever a piece covers is left out of the body under it (`BodyRegions`), so skin can't poke through.
- **Characters are 4.7 m tall**, seen from 33 m up at 67°. What's seen in play is masses and silhouette, so that's what gets designed first: a kasa, a plume, pauldrons and a shield's paint read from up there. Fine detail is for the icon and the character screen, and anything smaller than a pixel there is paid for and not seen, which is why small rivets have four sides.

**Materials.** Tint is vertex colour, so a metal is one material in many colours. Gear has `plate` (metal, all metallic), `mail`, `leather`, `cloth`, `bone`, `gem`, the glow, and four of its own (`MaterialRecipes.Gear.cs`, read `TEXTURES.md`): `timber` (hafts, bows, staves and shield boards, grain along their length), `scales` (lapped rows of metal scales), `quilt` (cloth sewn in channels over padding) and `fur`. Leather, cloth, quilt, bone, timber and fur each have a colour of their own, which their recipe averages exactly and `GearMaterials.AlbedoOf` divides by.

**Budgets.** Nothing is over 9,000 triangles (`check`, over every level of the climb, as the top of it has the most), and `check --costs true` says what each named part of a kind costs (`GearMesh.Counted`), which is where to look when something's over. A hero in everything is 20,000 to 30,000 triangles on one draw call. The land's people wear the same gear (see The land's people) within 20,000 for what comes in bands and 34,000 for one met alone, which `BuildLab check` holds; minions only hold their weapons.

## On the hero

`GearWorkshop` builds meshes on worker threads and uploads two a frame on the main one, as a boss drops ten things at once and a mesh mustn't cost a frame. `Client/Wardrobe` is what everything asks: it knows what's lying down, what's being worn, and what's in a picture. A hero is one merged mesh (`Outfit`), so one draw call, rebuilt when what they wear changes. `CharacterModelComponent` just gets a new model.

What a hero has on goes out in a reliable `Worn` message keyed by player, as `GroundItem` is keyed by find, to everyone when it changes and to whoever arrives later. It's two numbers a slot.

Whatever uses a model has to keep asking for it every frame, as that's what keeps it, and what nobody has asked for in a while is freed a few frames late, as commands for this frame may still name it. The `Gear:` line of a scripted run says how many are alive.

## On the ground

The same mesh, laid flat. Paired things are drawn as a pair, and a body piece is laid out as a garment. Gloves are laid side by side on their palms, backs up, fingers the same way and thumbs in (`GearDisplay.PairLaid`): end to end, as the bind pose has them, they read as one long tube. `PickupComponent` carries the look, as it carries everything else a drop needs. A find is scaled to a readable size for the camera, so a ring on the ground is big enough to see. That's a scale on the same model, not a different model.

## Icons

`ItemStudio` (`Rendering/Renderers/Studio/`, `Shaders/Studio/`) keeps one atlas and a queue, and draws two icons at the start of a frame. Until an icon is ready the cell shows its slot glyph. Same mesh, same material arrays, its own shading: a warm key, a cool fill, a rim, stepped diffuse, metal looking at a painted sky, and an outline found from depth and normals in a finishing pass. A picture goes to the interface premultiplied and already tone mapped. The same studio draws the hero for the character screen (`Doll`). Each kind is turned for its picture by `GearDisplay.PictureOf`: gloves are seen from nearly overhead, as from lower down their cuffs filled the picture. `GearLab icons --kind Hands` draws exactly what the studio draws, with no GPU.

## The character screen

`UI/HeroPanel.cs`. The hero stands in the middle in everything they're wearing, drag to turn them, with the ten places around them head to toe, the pack beside them and stats to the side. `Ui.DrawTooltip` is the one description of an item.

Nothing about this can be proved by a bot. To drive it with real mouse messages on a hidden run, post `WM_SETFOCUS` to the window first: the interface ignores the mouse unless it thinks it has the focus, and a hidden window never does. Read modifiers from `Context.Modifiers`, never from the mouse event.

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

**The stash** is the same cells with gaps, in four tabs, beside the pack when the strongbox is open, and moves by the same clicks. Its rules are on `Stash` and both ends run them: read `LANDS.md`.

**A merchant's table** (`MerchantTable`) is one per hero and site, and stays as it is too: what's bought leaves a gap, so a double click buys one thing. What's sold goes on it to be bought back at what it paid, so a slip costs nothing. The oldest goes once it's full. It lasts as long as `ServerPlayer.Tables`, which is until they leave the land or the game.

**Something put down** has `PickupComponent.Laid`, which leaves out everything that says a find is exciting: the long flight, the sting, the flash, the callout. The drop catcher is `GroundCatcher`, which only stops the mouse with something on the cursor, and only really over the game.

**Searching** is `ItemSearch`, which reads an item's own card, so whatever can be read on one can be typed, in any order. What doesn't match dims where it is.

`Dino BuildLab check` holds the rules, including 200,000 random moves with everything counted, and `Dino NetLab pack` holds what reaches a client. To try it in the real game: `--carry N` fills that many cells, `--panel merchant` opens a real table, and `--snap file` takes a picture whenever the file appears.

## The land's people

**Villagers and enemies wear what a hero could** (`Client/Uniforms.cs`): a few outfits for each kind of person, made of real bases (`ItemLook.FormOf`), every piece of an outfit in one palette (`GearTailoring.Named`) and from a level that fits who they are, so a band of outlaws or a line of greycoats reads as one at a glance and nobody is in a mix of whatever came to hand. Whose each is decides its look (`LORE.md`):

| Who | What they wear |
|---|---|
| Villagers | Linen robes and short tunics, a leather one now and then, in undyed and homely colours, worn; a straw hat, a hood or nothing; hands bare |
| Outlaws, cutthroats, brutes | Turncoats in worn leather and homespun, an iron cap or a hood, and the madder-red rag they're all known by; never the Reich's colours |
| The cults | Long robes in their power's colour with a hood and a staff; their acolytes in the colour gone dark, with a blade |
| Reich legionaries, marksmen, wranglers | The greycoats, in the Reich's livery: black iron over charcoal, brass and oxblood, plain helms; a wrangler in the oxblood leather of its beasts' straps |
| The warlord | An outlaw lord in good plate, with a crest and a cape in the outlaws' red, gold and never brass |
| Grave knights | Once-fine harness, rusted and tattered |

Where an outfit needs a particular part, a hood, a straw hat, pauldrons, a helm with nothing on top, it takes the first seed of its own that grows it, so it's fixed and the same on every machine. An outfit is the same whatever level a land is. `BuildLab check` holds that each is within its budget, that nobody but the Reich's own wears its colours, and that the Reich's own plainly do. Models are prepared with everything else after the menu, and dressing people this way cost no measurable time there.

## Saves

Testers carry saves across builds, so this can't lose anything. Looks aren't saved, so changing a generator changes what old items look like. That's accepted: it's how rebalancing an affix already works. What a rename of a base or a kind needs is a line in `SaveGame.Resay`, which runs on every save and stash before anything is parsed, as an unknown enum name throws and a save that can't be read starts a new journey.

## Testing

`Dino GearLab` is headless, as `WorldLab` is.

```
dotnet "Dino GearLab/bin/Release/net8.0/Dino GearLab.dll" check [--costs true]  # Every kind, thousands of seeds: is it valid, and within budget?
dotnet "Dino GearLab/bin/Release/net8.0/Dino GearLab.dll" fit                   # Does every piece of armour keep to its bands (GearFit)?
dotnet "Dino GearLab/bin/Release/net8.0/Dino GearLab.dll" looks                 # No Reich's colours; legendary pieces far more splendid than plain ones
dotnet "Dino GearLab/bin/Release/net8.0/Dino GearLab.dll" variety [--twins true] # Are they really different? Run it with --heavy 1, --leather 1 and --cloth 1 as well
dotnet "Dino GearLab/bin/Release/net8.0/Dino GearLab.dll" sheet --kind sword    # A contact sheet, drawn on the CPU, in true colour
dotnet "Dino GearLab/bin/Release/net8.0/Dino GearLab.dll" dress --heavy 1       # Whole people in one family's gear, standing in a T (--levels 0,0.5,1 for the climb, --neck true, --without Body)
dotnet "Dino GearLab/bin/Release/net8.0/Dino GearLab.dll" icons --kind Hands    # Things as the game pictures them: laid down, turned as the studio turns them
dotnet "Dino GearLab/bin/Release/net8.0/Dino GearLab.dll" describe --kind Body  # What a look chose, in words: its making, craft, metals, hides, dyes
dotnet "Dino GearLab/bin/Release/net8.0/Dino GearLab.dll" one --kind Legs --index N  # One of check's pieces taken apart, for a fault it found
dotnet "Dino GearLab/bin/Release/net8.0/Dino GearLab.dll" bench                 # What a mesh costs to make
dotnet "Dino AnimLab/bin/Release/net8.0/Dino AnimLab.dll" sheet --body knight --do walk --view game  # A whole person in armour, moving, from the game's camera (also ranger, mage, axeman, greataxe, greatsword, axes, warden; --seed, --grade)
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" gear                    # That everyone sees the same thing
```

`Dino BuildLab check` holds the rules: places, families, grips, the pace of two hands against one, what a look is packed into with its level, what the land's people wear, and that old saves come across. To see it in the real game: `--wear heavy` (or `leather`, `cloth`) puts something of one family in every place, `--wearlevel N` makes it from that item level, `--equip id,id` puts uniques on (without `--wear`, which replaces them), `--panel hero` opens the screen, and `--parade legionary,outlaw,warlord` stands the land's people in front of the hero.

- **`check`:** the same spec gives the same mesh, vertex for vertex. Nothing is NaN, nothing is degenerate, and it stays inside its budgets of triangles and size. A fault names the part it's in.
- **`fit`:** see Fit.
- **`looks`:** see How a look's coloured, and Legendaries.
- **`variety`:** many of one base are compared, as they drop (any rarity but unique, their power showing or not), as that's the hard case: a leather tunic against every other leather tunic. Two are alike if they share an outline, what's drawn inside it and which parts they're made of (`GearMesh.Parts` and `Mark`, so trousers with leather over the knees aren't trousers without). Colour is measured as the game draws it. It fails if more than one pair in 2,000, picked at random, look alike. `--twins true` says what the alike ones mostly are, which is what that kind wants more of. A fall means a kind needs more parameters: raising the threshold is the wrong fix. One family alone is the hardest case, as metal's greys leave nothing but its parts to tell two apart: heavy legs, boots and coifs failed it unseen after the rework, while only mixed families were being run, until mail got garters and quilted thighs, scale a cop, a cloth border and a lame at its foot, lamellar tassels and riveted rows, plate cusps, fluting and riveted edges, greaves straps and spurs, and a coif its edging, a peak and an iron cap. Books are held to one in 500, as their generator still colours as gear did before `GearLook`.

A lab proves a thing is valid, not that it's good. Looks are judged from sheets, from AnimLab's armoured bodies and from the real game, at play distance and up close. Nobody but a person can say it looks right.

## Where it stands

**Rebuilt on 2026-11-12**, and taken further the same day: looks climb with item level, edges lead and two-handers rest on the shoulder (`ANIMATION.md`), shields stand off the forearm, wards are held up, necklaces lie over what's worn, fists have fingers and gloves are pictured as a pair, and the land's people wear real gear in uniforms. Every weapon, armour piece, shield, ward, ring and amulet is grown from a `GearLook`; books, pages, sentences and orders still come from `GearStyle`, as they did.

**What's been proved, and how.** All 24 kinds pass `check` over every level; armour, shields, wards and necklaces pass `fit` over 3,000 specs each; every kind passes `looks` and `variety`. `BuildLab check` (1,197), `NetLab gear`, `build`, `books`, `pack`, `loot`, `combat`, `foes` and `game`, and `AnimLab check` (with an edge leading every swing and no forearm through the body) pass. Stills from the real game, hidden, show heavy gear from item level 1 to 100 on the character screen and from the game's camera, and the land's people paraded in the Haven.

**What nobody has judged.** No person has seen any of it. Every look was tuned from CPU sheets, AnimLab's poses and stills, so whether it's good is still open, and their opinion outranks every number here. In particular: whether the restrained colours read as their family under the game's light; how a long robe, a cape, a tabard and tassets move in a run; whether a heater's paint and a kasa read from the camera as well as they did in stills; whether legendary gilt is splendid or gaudy; whether a boiled-leather breastplate reads as moulded leather or as an apron, which it was closest to in the stills; whether the climb shows enough between levels 20 and 60, where it's weakest; and whether a cape, a crest and broad pauldrons near the top read as powerful or as much.

**What wasn't built.**

- Bosses don't show what they wear. They fight with real items and drop them, but a tyrant is a dinosaur, and the warlord wears his own uniform.
- What the land's people wear doesn't change with a land's level.
- Books, pages, sentences and orders keep their old generator.
- Levels of detail for gear: nothing has needed them yet.
- Crafting itself: `GearTailoring` is ready for it, and nothing sets it.

## Decided, and open to change

Choices made without asking, as none of them block anything. Say if any are wrong.

- **A one-handed weapon goes in either hand.** It stays a `MainHand` item, and `WornSlots.For(item)` is what says where it can go. The rules are in `BUILDS.md`.
- **Every melee weapon has one- and two-handed bases**, as asked, and the spear is a one-handed javelin, as chosen.
- **A unique has one look**, seeded by its id, so the boss's sword is the sword you then hold.
- **A caster's tall pointed hat is gone.** It read as a cartoon wizard. A caster wears a kasa, a wide shallow cone of stiffened cloth, lacquered wood or hardened leather tied under the chin, which is the game's Tsushima side and reads clearly from above.
- **Colour is restrained** (see How a look's coloured), at the cost of some variety, which comes back through real features instead.
- **Heraldry never makes a cross**, and a hero never wears a skull, which is the Reich's mark.
- **Mana comes on top of barrier for cloth.**
- **The climb's thresholds are guesses**: capes from a third of the way up and nearly always near the top, crests likelier from the middle, a helm over a coif from halfway, a jerkin over hide near the top.
- **The land's people**: outlaws wear a madder-red rag as their mark; the warlord, who was in near-oxblood and a brass-like gold, is an outlaw lord in the outlaws' red and real gold; villagers are undyed and homely; their gear is from a fixed level each.

## Where this came from

- [How I make infinite procedural swords](https://bmaczero.itch.io/icon-machine/devlog/357002/how-i-make-infinite-procedural-swords): tuned ranges not open ones, a mirrored half for balance, and one set of shading rules and one outline to make unrelated parts into one object.
- [Borderlands' parts system](https://borderlands.fandom.com/wiki/Manufacturer): where a part comes from is what you can read off it. Here the making does that job, without the parts being a list.
- [Path of Exile 2's armour bases](https://www.poe2wiki.net/wiki/List_of_body_armours): the same split of armour, evasion and a magical defence, with hybrids between.
- [Guilty Gear's toon line techniques](https://docswell.com/s/ASW_Academy/5LVY67-GG-Toonline-Eng): why icons find their outline from depth and normals.
- Real armour and arms: the construction of a cuirass, fauld and tassets, lamellar laced in rows, brigandine rivets, cuir bouilli, splinted guards, poleyns and couters with their fans, spangenhelms, kettle hats, sallets and great helms worn over a coif, gambesons closed with toggles, cross-gartering, and a heater's convex flanks are what the parts are modelled on.
- Nothing useful was found on fitting procedural armour to a body: `BodyForm` and `GearFit` are our own design, made possible by the body already being analytic.

## Books and what's thrown

A book is gear like anything else: one mesh from `BookGenerator`, by `GearKind.Book`, in the pack, on the bar and on the ground. `Item.LookSeed` says how it looks, and nothing written in it does.

The one that's being read shows open, wherever it shows (`BookPanel.IsOpen`, `Ui.DrawItem`). That's the only other mesh anything has: `GearKind.OpenBook` draws from the genes of `Book`, by the same names, so it's the same book laid open and `check` fails if it isn't. No item is ever that kind, so nothing but `Wardrobe.Picture` asks for it, and until there's a picture of it the shut one shows.

A page is `GearKind.Page` and a sentence `GearKind.Sentence`, one mesh each like anything else: a page a sheet with lines on it, and a sentence a narrow slip cut from one, its ends curling, with one line written along it, an inked capital and a stop (`BookGenerator.Slip`). Nothing about a look says what's on one, so `Ui.DrawPlaces` puts what matters on the picture.

Orders are `GearKind.Orders` (`BookGenerator.Orders`): parchment rolled and tied in oxblood and sealed in oxblood wax pressed with brass, as they're the Reich's own and those are its colours. Nothing about the look says what's in them: their card does.

A hero with nothing that shoots throws what's in their hand, and that's the same mesh again: `ThrownComponent` carries the weapon's seed and traits, and `GhostPresenter.Tumble` keeps asking for it, end over end, or point first for a javelin. Something with no item to show is thrown as `Thrown.Of` says, from one model for each kind of weapon (`CharacterFactory.ThrownThing`).

## Adding to it

- **A new base:** a line in `ItemGenerator.Bases` and a line in `ItemLook.Forms`, with its making if its name says one. `check` fails without the second.
- **A new parameter:** draw it from a name of its own with `genes`. Nothing else moves, so old items keep their look.
- **A new part:** behind `look.Likely` or `kit.Likely`, which is how it comes to belong to a family; inside its place's band in `GearFit`, or `fit` fails; named with `mesh.Counted` (or `mesh.Mark` for something that isn't geometry of its own), so `check --costs` and `variety` can see it.
- **A new making:** a value at the end of `GearMake`, its roles in `ArmourKit`, and what each place's generator builds of it.
- **A new colour:** a swatch on the end of its list in `GearSwatches`, never in the middle (`GearTailoring` names them by place), and clear of the Reich's (`looks` fails otherwise).
- **A new kind of thing:** a value in `GearKind`, a case in `GearForge.Make` and `GearDisplay`, and a line in `Outfit.Dress` for where it goes.
- **A new material:** it costs about 17 MB with its top level dropped, needs a line in `MaterialCatalog` (on the end), a recipe in `MaterialRecipes.Gear.cs`, a line in `BindGearMaterials`, and, if it has a colour of its own, a line in `GearMaterials.AlbedoOf` that its recipe averages exactly. Read `TEXTURES.md`.

Changing anything generators read changes how old items look, which is accepted. Nothing about a look is saved but a seed.
