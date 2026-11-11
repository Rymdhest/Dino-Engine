# Textures, relief and how a surface is read

Every tiling material is grown at start from seamless noise by a recipe, and every surface in the world reads it the same way: through a frame worked out per
pixel from its texture coordinates, with a relief marched to a depth in metres that the normal map was made for. One number per material, its relief over
its tile's width, is what the normals, the ambient occlusion and the parallax are all made from, so the light, the shadows in the relief and the parallax can
never disagree.

## A material

- `Textures/MaterialCatalog.cs` (headless) says what a material is to anything without a GPU: its name, the metres a tile spans at the size it was made for
  (`TileMetres`), how deep its relief goes in metres at that size (`ReliefMetres`, 0 for flat, which gets no parallax) and how much water it drinks
  (`Porosity`). Its index is its layer in the arrays, from its place in the catalogue. A new material is a line there and a recipe in `MaterialRecipes`.
  The world is about twice real scale (a person is four metres tall), so a real material's sizes are doubled.
- `Textures/MaterialRecipes*.cs` are the looks, one method each, grouped by family (`Ground`, `Built`, `Bark`, `Gear`). A recipe draws on a `Canvas` and
  returns a finished `Surface`.
- `TextureGenerator` makes every material straight into its layer of three arrays at start (`MaterialBaker`), and keeps the old names (`TextureGenerator.cobble`
  and so on) as the catalogue's indices, known before anything is drawn.
- Some materials are one biome's or one way of building's own: a conifer wood's floor of fallen needles (`Needles`), the red earth of the hot dry country with
  its cracks fading out and stones trodden into it (`RedEarth`), a broadleaf wood's leaf litter (`Litter`), and the curved clay tiles of a roof in the sun's
  country (`RoofTiles`, `BuildingStyle.Tiled`). They're on the end of the catalogue, so adding them moved no other material's layer. The ground's palette has
  at most `TerrainSurface.MaxMaterials`, and a material a world can't fit falls back to its cousin (`TerrainSurface`).

## Making one: `Textures/Synthesis/`

- A `Canvas` is one tile, wrapping, at the arrays' size (2048). Its generators: `Noise` (Perlin, value, ridged, billow, optionally warped), `Offsets` (two noises
  for a warp), `Cells` (Voronoi with the true distance to the border, Quilez), `Bricks` (courses of bricks, blocks, boards or tiles, running or random bond,
  random lengths, upright for boards), `Scatter` (up to four elliptical domed stones a cell, the higher of two overlapping showing), `Weave`, `Mail`, `Facets`
  (broken rock), `Grain` (the growth rings of each board of a `Bricks`), `U` and `V`, and `Ramp` (a gradient map, the main way a material gets its colour).
- Everything tiles: every lattice is a whole number of cells across the tile, and a frequency is rounded to one. `U` and `V` jump at the edge, so they only
  tile through something that repeats a whole number of times across. Every neighbourhood operation wraps. Any whole number of cells tiles, not only
  powers of two, as long as a cell is wrapped by `wrapInt`, which never floors a quotient that lands on a whole number: a GPU's division is only nearly
  exact, and once every frequency that wasn't a power of two left a seam at the tile's edge, with other stones beyond it. `--texturecheck` measures every
  generator and neighbourhood operation across the tile's edges (`Canvas.CheckSeams`) and FAILS on a step there bigger than one anywhere else.
- Noise is standardised: mean 0.5 and a deviation of 1/6 whatever its kind and octaves, from one octave's measured statistics (`Canvas.OctaveStatistics`,
  measured by `--texturecheck true`, which prints them: change the noise and measure again). Octaves finer than a few texels fade out instead of aliasing.
  `--texturecheck` also measures what recipes rely on (`Canvas.CheckFields`): standardised noise of every kind, `Equalize` spreading a field evenly, and a
  threshold after it covering just its share, and prints FAILS where any is off. Run it after any change to `Synthesis/` or `Shaders/Texture/`.
- Anything that makes or changes a texture binds it to `Synthesizer.ScratchUnit` (`Synthesizer.Edit`), never whatever unit is active, which may hold a pass's
  input: `Equalize` once uploaded its curve over its own input that way, and every patch of moss, turf, lichen and rust in every material came out empty.
- A `Field` is a number everywhere, a `Paint` a linear colour everywhere. Arithmetic, `Remap`, `Smooth` (which may fall as well as rise), `Threshold`,
  `Profile` (a distance from a border as a rising edge: `Rise.Round` for a stone's rim), `Random` (a random of each cell from its id), `Warp`, `Blur`,
  `SlopeBlur`, `Erode`, `Dilate`, `Normalize`, `Equalize` (after which a threshold of 0.3 leaves exactly 30% above it), `Occlusion` and `Curvature`. Constants
  fold on the CPU.
- Nothing is drawn as a recipe runs: it builds a graph, and `MaterialBaker` draws only what the material needs, holding each image only while something is
  left to read it (`Evaluation`), in floats (heights in 32 bits: 16 shows as terraces in the normals).
- Seeds come from the material's name and how many generators were asked for before, so a material is the same every run and on every machine. Adding a
  generator changes the seeds of the ones after it in that recipe, and no other.
- A `Surface` is height, albedo, alpha, roughness, metallic, emission, subsurface and occlusion. Build it as the thing was built: `Stack` lays a surface over
  another wherever it stands higher (stones in mortar, moss over rock), blending within a softness and rounding the height between them; `Mix` blends
  everything by a share; `Coat` changes what it's made of and keeps its height; `Shade` darkens or lightens by a mask.
- Height runs from 0, the bottom of the relief, to 1, where the mesh is. The relief always hangs below the mesh, so nothing stands out of a silhouette.

## What a recipe has to keep to

- Colours are the engine's linear albedo, given as numbers out of 255 (`Colours.Rgb`). Under a sun of about 13 anything pale goes white.
- Bark is pale and neutral, as a tree's colour and moss are in its vertices. Metal is white, as gear colours it. Leather, cloth and bone average exactly
  `GearMaterials.AlbedoOf`, as a colour meant as an albedo is divided by it: move their colour only by noise, whose mean is a half, and put their structure in
  their height.
- From the game's camera a metre is thirty pixels: the large shapes carry a material, and what's finer is for close up. Judge it there.
- A material repeats every tile, a few metres, and a dozen tiles of ground are in view at once: anything as big as a tile and strong in contrast is seen
  repeating, as wallpaper. Keep what's coarse soft and close in shade, and put the contrast in what's many to a tile. Ash once had a few big dark patches of
  crust to a tile, and the whole land was a pattern. Variety at the scale of a land is the terrain's palette, never a material's.
- A cell's border distance creases along the cell's middle: a profile wider than the cell is goes to a ridge. Blur the distance a little, or keep the profile
  narrow and add a crown from `Centre`.
- Cells read as crazy paving unless the thing really is pieces: stones, plates, crust. Give those joints a width of their own (inset the border) and edges that
  wander (warp it). What braids or runs, as an oak's fissures do, is where a noise stretched along it crosses its middle (`(noise - 0.5).Abs()`), with few
  octaves so it runs smooth; what crowds in cushions, as moss does, is `Scatter`'s domes.
- A mask of patches covering a share of the tile is `MaterialRecipes.Patches`: equalized noise through a threshold, so its cover is exact. A patch tuned while
  it was empty, as every one was until `Equalize` was fixed, is worth looking at again.

## Baking

- The normal is the height's at exactly the material's `ReliefRatio` (Sobel, normalised to a true derivative), x along +u and y along +v.
- The ambient occlusion is the horizon-based integral of Jimenez et al. over the height at the same scale, cosine-weighted about each point's own normal.
- Every mip is made in floats from the one below and only then packed. Normals are averaged as vectors, and how far they disagreed widens GGX roughness
  through a von Mises-Fisher fit (Neubelt and Pettineo): a bumpy material far off is a rough one, not a glittering one. Colour is averaged in linear light.
- Arrays: albedo (sRGB: colour, alpha), normal (normal x, y, subsurface, occlusion), material (roughness, emission, metallic, height). Anisotropic filtering
  is the quality's (`QualityPreset.Anisotropy`) on colour, at most 2 on normals and none on the rest.
- `--materialsheet folder` writes every material's maps as pictures, the height tiled two by two where a seam would show as a line.

## Reading one: `Shaders/Surface/`

- `SurfaceFrame.glsl`: the gradients of u and v along the surface from the pixel's two screen steps (Schüler; Mikkelsen's surface gradients), exact for any
  mapping, stretched, skewed or mirrored, on anything: skinned, bending in the wind or stretched. Nothing reads a vertex tangent. `surfaceNormal` reads a map
  in that frame, with the axes scaled by the tile's metres so a stretched mapping keeps the slopes of its stretched relief. A card's z is the mass's normal,
  unturned. Take it before anything is discarded: derivatives after that mean nothing.
- `Relief.glsl`: parallax occlusion mapping with the depth in metres (the material's ratio times the tile's metres here), the view ray followed below the
  surface as it curves (the shape operator from how the smooth normal turns across the pixel, `surfaceShape`), so a round thing's silhouette doesn't swim; the
  relief brought up to nothing near a face's edges (`Vertex.Edges`), where it would move nothing by a pixel, and on what curves round tighter than a few of
  its depths (a branch thinner than its bark is deep, where it costs as much as on a trunk and could only show as a rim); as many steps as pixels crossed; false position
  to finish; and a soft shadow of the relief on itself towards the sun, written to `gMaterials.a` and applied to the sun alone (`Ambient_Light.frag`).
- `MaterialTable.glsl`: each material's relief ratio and tile metres (UBO 12, `MaterialTableBuffer`).
- `Terrain.frag` marches in the world, each material at its own size and depth, their depths blended by share, and writes the hit's depth so what stands on
  the ground stands in it. A terrain material's relief stays within about `TerrainRenderer.SurfaceOffset` (0.15 m), or its deepest parts are shaded by the
  shadow map's surface.
- Models don't write depth: their relief's own shadow is the shadow that matters, and a deeper depth would put the shadow map's surface in front of it.
- Rock (`TextureGenerator.cliff`, `isRock`) is read from where it is by `Rock.glsl`, never through texture coordinates, so it has no relief to march: a stone
  is drawn with none, and on the ground rock has no depth in the blend. Marching one once shaded stones with shadows of a surface that wasn't drawn.
- `gAlbedo.a` holds what a thin surface lets through, less what its thickness takes (`transmittance` in `gBufferUtil.glsl`).
- Quality: Low draws no relief (`QualityPreset.Relief`), only the normals. `--render relief=N,aniso=N` overrides both.

## Roads

A road's own materials are read in the road's frame, not the world's: across it and along it, so a track's ruts follow every bend and a street's setts lie
square across it. Painting a road's texture the world's way is what made the old roads look like a splat map.

- `World/RoadFrames.cs` (headless) gives any point its `RoadFrame`: metres across the nearest road (signed, towards `Side`) and along it, the road's way and
  half width, and `Junction`, 1 where another road meets it, where it ends, or where the nearest road is about to swap. There across and along jump, so
  nothing may be laid by them. It's measured from road segments through a grid, a straight line within a texel, so `Terrain.frag` carries it exactly through
  the march (`RoadAt`).
- Each chunk bakes it per texel (`TerrainChunkSurface.RoadFrame` and `RoadInfo`, full floats, as along runs to thousands of metres) into two arrays on the
  units the card arrays would take: the ground reads no cards, and a stage is only promised sixteen.
- A street (`SurfaceMaterial.Setts`, `MaterialRecipes.Setts`) spans its tile kerb to kerb: u is across from one kerb's back to the other's, so its tile is a
  street's width (6 m) and the kerbs, gutters and courses are where the recipe puts them. Past either kerb it has no height at all, so whatever's beside a
  street meets its kerbs, and the paved margin past them is gravel. Where roads meet, where a street ends or runs into a square, it's `Cobble`, which has no
  direction to keep. Squares (`GroundPaint.Plaza`) are cobbled and fray out through gravel.
- Paving (`GroundPaint.Paving`, `SurfaceMaterial.Flagstones`, `MaterialRecipes.Flagstones`) is the waygates' own stone laid in slabs, read the world's way, so
  its courses run east and west, along the Haven gate's steps: the faces are `RockSurface`'s facets and grain at its own size, in the Haven gate's colour with
  its shade near the ground (`WaygateDesign.GreatStone` and `GroundShade`, which the gates' blocks read too, as both read `RockDark` and `RockPale`), and every joint has the gates' 5 cm chamfer, so a gate's steps run on into it as one stone. It's laid over
  whatever was painted there, roads included, keeps everything from growing through it, and its edge is where its slabs end. A world only gives it a slot in
  its palette if something's paved (`WorldData.Paved`). Its tile is 8 m, as slabs a metre across repeat as wallpaper on anything smaller. Paint alone is never the
  design of a place: the Haven's square lays real stone over it, kerbs, gutters and spokes standing a finger proud of the drawn ground (`PlazaKind.Line`, `Ring`), as
  a painted stripe a metre's paint cell wide only ever reads as a blur (`SQUARE.md`).
- A tended bed (`GroundPaint.Garden`) changes no material: it's the land's own ground, with its grass kept short, green and in flower (`TerrainSurface.Tend`).
- A dirt road (`SurfaceMaterial.Road`, `MaterialRecipes.RoadDirt`) is the road's earth and trodden stones, with grooves along v; its ruts, the crown between
  them, a footpath's worn line and the verge are its cross-section, `RoadProfile` on the CPU and `roadShape` in `Terrain.frag`, which have to agree: the
  grass of a track's crown is laid on the CPU (`RoadProfile.CrownGrass`, and the road channel lowered there so the grass grows), and the ruts are sunk into
  the height the march reads, lit by their own slope, and fill with rain first. Ruts are only on what carts use (`RoadProfile.RuttedFrom`). Trodden ground
  in a village is the same earth read the world's way.
- Where the frame fades out (`Junction`) a road's material is cross-faded to the world's way of reading it, so no seam shows where the nearest road swaps.

## Walls and roofs

- A wall is seen from the game's camera at about 2.4 depths of slant (67 degrees down onto a vertical face), so its relief always runs sideways: what makes
  a wall look built is deep joints and rounded stones, and what tears it is cliffs in its height (a few texels stretched over many pixels) and fine detail on
  every stone (pits, grime, pinned pebbles), which at twenty pixels a stone reads as noise. Keep a stone's face big, pale and quiet, round its edges with
  `Rise.Sine` rather than `Rise.Round`, and let the joints carry the shape.
- Stones lie in rough courses: `Canvas.Cells` takes a jitter for each way, and much across with little down keeps sites in rows.
- `Relief.glsl` reads what casts a shadow one and a half mips coarser and ignores anything less than 6% of the depth above the ray: the chisel marks right
  beside a point otherwise blacked it out in specks wherever light raked across. Past three depths of slant the relief flattens, and near a face's edge it
  comes up to full depth only as far in as the ray can run sideways at this slant, so a wall seen square keeps its relief nearly to its corner.

## Texture coordinates

- `MeshBuilder` and `GearMesh` map every material at its own size unless told otherwise (`TilesPerMetre`, NaN by default): stones are the size of stones
  whatever they're built into. A tube and a loft wrap a whole number of tiles round, so they close without a seam. A box's sides run on from each other
  round it, so courses carry round every corner but the first. v runs up a wall and up a roof's slope.
- Every face `MeshBuilder` makes gives its vertices their distances to its own edges (`Vertex.Edges`, attribute 11): a distance to a line is a straight
  function across a flat face, so interpolating them is exact. A grid surface's edges are the grid's boundary, a tube's its ends. A mesh without them reads
  the default, which is no edges.
- `--materials a,b` (or `all`) stands a wall, a column, a log and a floor in each material beside the hero, at its natural size, for judging the relief round a
  corner and a curve under the game's own light.
