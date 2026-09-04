# Working on Tile World

This is the file to read before touching the code. It says how the project is
put together, what has to stay true, and how to check work from the command
line. It exists because the project is long and work on it happens in
stretches; anyone picking it up cold -- including a fresh session of an
assistant -- should be able to start from here without rediscovering any of it.

Keep it current. When a rule changes, change it here in the same commit.

## The loop

1. Edit.
2. `Tools/check.sh` -- compiles every runtime script in a few seconds with
   Unity's own compiler, editor closed. Run it after every edit.
3. `Tools/build.sh` -- a development player into `Builds/Dev` (ignored by git).
   Minutes, not seconds. Needed for anything `check.sh` cannot see (Editor
   scripts) and for anything that has to be *seen*.
4. `Tools/run-probe.sh Tools/probe/<Probe>.cs.txt [seconds]` -- drops a
   throwaway script into the game, builds, runs it until the probe says `done`,
   gathers its stage file and screenshots into `Tools/.check/shots/` (earlier
   shots are kept; a probe's own overwrite by name). Back the saves up first
   and restore after (below). `StructureTour` visits every structure kind;
   `GuideContents` opens the field guide.
5. Look at the screenshots. Measure rather than eyeball where you can.
6. Commit as each piece completes, and push.

The Unity editor must be closed for `unity.sh`, `build.sh` and `run-probe.sh`:
batchmode takes the project lock. Every batchmode run empties
`Library/LastSceneManagerSetup.txt`; `Tools/unity.sh` puts it back, so the
editor does not open on a blank scene afterwards. Use it, not Unity directly.

### Saves

The player's worlds live in
`~/Library/Application Support/DefaultCompany/Tile World/worlds/*.json`.
A probe that teleports the player, discovers things or lets the clock run
changes them. Before a probe run: `Tools/save-backup.sh`. After:
`Tools/save-restore.sh`, which puts back only the fields that differ. Never
copy a backup wholesale over a newer save; that wiped real progress once.

### Probes

A probe is a `MonoBehaviour` with a `[RuntimeInitializeOnLoadMethod]` boot,
copied to `Assets/Scripts/_Probe.cs` for one build and removed. Never commit
one. `Tools/probe/StructureTour.cs.txt` is the model; it embodies everything
that went wrong before:

- A standalone player **pauses when it is not the front app**. Set
  `Application.runInBackground = true` first thing, or nothing happens and the
  log stops after startup.
- `WaitForSeconds` does not elapse at timescale 0. Use `WaitForSecondsRealtime`.
- Write stage marks to a file, not `Debug.Log`: the player log is buffered and
  the tail is lost when the process is killed.
- `SimpleFollowCamera` re-places the camera every frame. Disable it before
  moving the camera by hand. There is no Cinemachine in this project.
- Set daylight (`TimeOfDay.Instance.SetTime(0.38f)`) before shooting; the clock
  runs on between runs.
- Chunks only build when they are in view; do not wait for a chunk before
  pointing a camera at it.
- Ring searches over chunks must walk the perimeter of each ring, not the whole
  square per ring, or a 300-ring search never finishes.

### Measuring

Most bugs here were settled by a number, not by looking harder: the palette
UV of a model to learn its colour; a frame diff between two identical frames
to measure flicker; the rim of a tile's mesh to learn its real footprint; a
count of how many chunks pass a placement rule. When something looks wrong,
find the quantity that would prove it and print it.

## The code

`Assets/Scripts/`:

- `World/` -- the ground. `WorldGrid` (tile 2 units, 15 tiles a chunk),
  `WorldHeight` (noise, terraces), `Regions` (what kind of country a place is),
  `Chunk` (which tile goes where), `ChunkManager` (loading, instanced drawing,
  the tile library), `WaterSurface`, `SnowCover`, `Undergrowth` (what stands on
  the ground), `Flora` (the shelf of plant meshes), `TerrainCollision`,
  `TileDefinition`/`TileLibrary`.
- `Landmarks/` -- the structures. `Landmarks` (where and which), `LandmarkBuilder`
  (how each is put together), `LandmarkSpawner` (built near the player,
  discovery, survey, rest), `Structures` (the shelf of pack pieces),
  `Inscriptions`, `LandmarkLog`, `LandmarkTag`.
- `Wildlife/` -- creatures, and the field guide: `Fauna`, `Animal`, `Sketching`
  (drawing with F), `SketchBook`, `FieldGuide`, `Subject` (a creature or a
  structure, as a thing to draw), `Noticing`, `Observations`.
- `Player/` -- `Surveyor` (the procedural character and its animation),
  `Swimming`, `SimpleFollowCamera`.
- `Interface/` -- map, journal, compass, screens, notices, `DevTools` (F8).
- `Systems/` -- saves (`WorldLibrary`, `SaveCoordinator`), `RegionWatcher`,
  `SceneSystems`, `Paint`, `Shaders`.
- `Atmosphere/` -- time of day, weather, sound.

`Assets/Editor/` -- tools that write assets: `GroundTiles` (sand and stone
tile definitions), `FloraIndex` (`Resources/Flora.asset`), `StructureIndex`
(`Resources/Structures.asset`), `Grown` (procedural conifers and reeds),
`PlayerBuild` (the command line build). Run them with
`Tools/unity.sh -executeMethod Class.Method`.

## What has to stay true

**Derive, don't store.** Terrain, tiles, regions, water bodies, snow, what is
planted, where structures are and which way they face: all pure functions of
`(position, seed)`. Nothing about the world is saved but what the player did.
A chunk can be asked about before it is ever loaded. Where a function is
expensive it is cached (`Regions.CharacterAt`, `Landmarks.In`), keyed on the
seed, and the cache is cleared when the seed changes.

**Systems spawn themselves.** Runtime systems use
`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` and `SceneSystems` re-runs
them on scene load. There is little in the scene to drag things onto.

**One material, one batch.** Tiles and plants draw with
`Graphics.RenderMeshInstanced`. The whole tile pack samples one palette
texture; a model's colour is *where its UVs land*. Recolouring (snow trees)
and procedural geometry (pines, reeds) choose a UV with `Grown.Swatch`.

**Flat shading needs unshared vertices.** Opposite-facing triangles sharing
corners average their normals to zero and render blank white.

**No two surfaces on one plane.** Anything laid wider than its tile (sand,
stone) gets a settle offset per tile that guarantees neighbours differ:
`((gx*2 + gz*3) % 7) * 0.0006`. A hash leaves one pair in N still level.

**The grid.** Tile 2 units; chunk 15 tiles; region cell 8 chunks (120 tiles).
Terraces every 0.25; walking surface `SurfaceY = 1.05 + terrace*0.25`; a tile
block spans a unit below its centre to 0.95 above, its centre 1.05 under the
walking surface. Water `Level = 5.55`, exactly on a terrace: the shoreline is
the terrace at *nought* above the water, and depths are 0.25, 0.50, ...

**Tile ids.** Five variants a category: 0-14 the three grass shade bands
(treed ids 4, 8, 11, 13), 15-19 Big Grass with no trees, 20-24 Very Dark (mud,
lake beds), 25-29 sand, 30-34 stone. Get a definition with
`ChunkManager.TryTile(id)`.

## Regions

Eleven characters (`Regions.Character`): Lowland, Forest, Water, Hills, Peaks,
Fungal, Desert, Snow, Stone, Dead, Reed. A cell's character is worked out
from samples of its ground, in an order where the strongest impression wins
(water, then snow-by-height as Peaks, then the rarer characters by hash).

Borders wander and fray. A tile asks `Regions.CharacterAtTile`, which moves
the tile by a slow noise (up to 14 tiles) before finding its cell, then may
hand a tile within 7 of that line to the cell across it by hash. A chunk's own
character (`CharacterAt(chunk)`, used for names and announcements) is taken at
its middle without the fray. Things that must not speckle -- which sort of
water a tile is -- pass `fray: false`.

The recurring bug shape in this project: **a rule written for one biome
applied to all of them** (sand on every shore, reeds in every lake, trees on
beaches). When adding a rule, ask which country it belongs to.

## Water

`WaterSurface.BodyAt`: a Water-character region's water is a Beach; otherwise
the deepest water within five tiles decides Lake (over 2.2) or Pond. Beds:
sand in the shallows and stone below 1.6 on a beach; mud under a lake or
pond; stone under snow. Reeds only in lakes and ponds, in water under 1.1.
Snow never lies on a water floor.

## Structures

Fifteen kinds, one country each: Forester's Watch and Hunter's Hide (Forest);
Shipwreck and Lighthouse (Water); Sand Gate and Buried Tower (Desert);
Trapper's Cabin (Snow); Fishing Jetty (Reed); Stepped Altar (Stone);
Toadstool Ring (Fungal); Charcoal Camp (Dead); Hilltop Beacon (Hills); Summit
Cairn (Peaks); Wayside Shrine and Standing Stones (Lowland).

All fifteen are built from the **kit** (`Kit.cs`): flat-shaded geometry made
here -- log, stone, timber-frame and plank walls; plank, thatch and slate
roofs with gable ends; doors, windows, chimneys, posts and rails, steps and
pavers; round towers with battlements or a cone; props (barrels, crates,
tables, lanterns, troughs, signs, wells, woodpiles, hay, banners, ladders,
cart wheels). Colour comes from the pack's palette: `KitIndex` finds the
nearest swatch to each colour wanted and writes `Resources/Kit.asset`. A
`Kit.Builder` gathers a structure into one mesh with a box collider per
solid part; `Finish` hands back the object. `Tools/probe/KitShowroom*.cs.txt`
lay every part out labelled for looking at.

Every structure stands on a **foundation** (`LandmarkBuilder.Foundation`): a
slab, flagged or packed earth, with a skirt of coursed stone down to well
below the ground and a flight of steps on the side asked. The ground under
a big footprint steps by a terrace or two and the skirt swallows it -- which
is what lets footprints be big -- and a raised court buries the ground
tiles' own rocks and logs, which no planting rule can keep out of a yard.

`Landmarks.kinds[]` is the whole catalogue: name, the one `Country` it is built
in, its `Site` (Level ground, a beach's Shallows, a lake's Shore), how it
surveys, the footprint in tiles (`Behind`, `Ahead`, `Aside`, in its own frame
where +x is ahead: the stair, the jetty, the shore), how level the ground must
be, and its `Chance` per hundred fitting chunks. Everything else follows from
the table: `Landmarks.In(chunk)` decides, `Occupies(tile)` keeps trees and
plants off the footprint (asking the neighbouring chunks too), `LandmarkBuilder`
builds, `DevTools` gets a jump button, the field guide gets an entry.

Yaw is a quarter turn, because the pieces are on the grid. Unity's +90 about
y sends +x to **-z**; never hand-map it, inverse-rotate the offset.

Heights in the builder are from the root, which sits on the walking surface:
the ground's block top is at -0.10; a stacked tile has its centre at 0.95 and
its top (`Deck1`) at 1.90; the next at 3.90; the next at 5.90. Pieces stand by
their pivots: lamp +1.0, sign +1.01, door +1.01, chest +0.17, box +0.30, fence
+0.19 above the floor; a stair's pivot is 0.94 *below* the deck it reaches,
its high end at its -z; a bridge's pivot 0.99 below its deck.

The pack lies about some pieces, and the shelf (`Structures`) says so:
*Timber* is a bundle of crossed lumber, not a log; *Busts* are all bushes;
*Rarefoot* are poles. The stone tiles taper underneath, so a wall or tier of
them looks to float; build walls and tiers from sand (pale stone) or grass.
Pack prefabs' roots can carry a position from the pack's scene (the stone
tiles' did, twenty units off); always set a placed piece's local transform
outright. Where a statue would stand, a boulder is stood on end and drawn out tall
(`Standing`); at its own proportions a boulder on end is still a boulder.
`Lying` is the same stone as it fell. Both pick only the grey boulders: the
pack's stones include bright gems, and one of those on end was a white pole
eight high.

**Weathering.** Every structure is a ruin. A `Kit.Builder` carries a `Decay`
(nought kept, one barely standing) and a `Weathering` (what the country does
to a place left to it: Vines, Snow, Sand, Char, or None on bare rock), and
every part consults them: a part with fragility f is gone when
`random < f * Decay`; walls crumble, roofs come down a section at a time,
doors hang, rails break, glass goes, floors grass over, wood greys or chars.
`LandmarkBuilder.WeatherAt` picks the weather from the ground the footprint
actually stands on (snow if most of it is snowy, sand on beaches and deserts,
char in the dead woods). Decay is set per kind in its builder, 0.6 to 0.85;
the collapse particular to each -- a fallen stilt, a breached wall, a snapped
mast -- is written into that builder by hand. To see one, run the structure
tour for its kind. Nothing about the world sets decay yet; making it come
from the placement seed, with a rare kept place, is the obvious next step.

Densities are tuned by `Chance` against a count: the tour probe reports how
many of each kind lie within forty chunks of the player, and a kind with a
small footprint in common country needs a small chance (the cairn is 6).

To add a kind: add it to `LandmarkKind` and `kinds[]` in the same order; a
`case` in `LandmarkBuilder.Build` and a method that builds it with a
`Kit.Builder` on a `Foundation`; a line set in `Inscriptions`; then
`Tools/run-probe.sh Tools/probe/StructureTour.cs.txt 240 <kind index>` and
look at it from both sides. Heights in a builder are from the root on the
walking surface; put the foundation top above 0.17 in snow country or the
snow comes up through the floor.

## Snow, planting, tiles

Snow is a thick slab per tile with a skirt over the edge, flush with a snowy
neighbour at the same terrace and hanging where there is none; no collider,
so you wade through it. `SnowCover.IsSnowy` (region or height) is what
everything asks; `SnowByHeight` exists only so a region can be worked out
without asking itself.

`Undergrowth.Sow` plants per tile from a table per character. A model that
reaches a full tile-half below its origin is drawn to stand *in* the tile;
lift the rest by their foot. `FloraIndex` reads the pack's named folders and
builds pines, snow pines and reeds procedurally.

`GroundTiles` builds sand (body and cap, laid 1.08 wide) and picks the five
plainest stone tiles by measuring coloured against grey surface area, laid
1.10 wide so their rims overlap and the V between them is roofed over.

## Player

The character and its animations are built and driven in `Surveyor`; stride
is derived from the measured leg, not tuned. `Swimming` corrects float by
position, not force. The controller's step offset is 0.45 (`PlayerArmature`),
which a stair tread of 0.12-0.17 clears and a 0.45 step does not.

## Dev tools

F8 (editor and development builds only): jump to the nearest region of each
kind, the nearest beach/lake/pond, the nearest structure of each kind; show
the opening again; wipe the world. `DevTools.cs` is under
`#if UNITY_EDITOR || DEVELOPMENT_BUILD`; `check.sh` defines
`DEVELOPMENT_BUILD` so it is compiled.

## Conventions

- Comments explain *why*, in plain prose, and say what went wrong before when
  that is why the code is the way it is.
- Commit messages: what changed and why, as prose. No trailers, no tool or
  assistant attribution anywhere in the repository.
- Commit as each piece of work completes; push.
- Do not commit probes, builds, or `Tools/.check/`.
