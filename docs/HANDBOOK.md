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

Every probe sets `AudioListener.volume = 0f` in its `Boot`: a probe runs beside
whatever else is going on and must not be heard. Keep that line in new ones.


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

A probe's `Boot` (the `RuntimeInitializeOnLoadMethod`) fires twice now that
the build opens on the title: once in the title scene and again when the
world scene loads. Guard it with `if (FindFirstObjectByType<_Probe>() != null) return;`
or every stage runs twice, fighting over the player. The newer probes do.

The editor's console can show what a build's log does not. `Assets/Editor/PlayCheck.cs`
plays the game in the editor from the command line: copy a probe to
`Assets/Scripts/_Probe.cs` (as `run-probe.sh` does), run

    Unity -batchmode -projectPath . -executeMethod PlayCheck.Go -logFile Tools/.check/playcheck.log

without `-quit` or `-nographics`, and it enters play mode for 75 s and quits
itself; the log is the console. `WaitForEndOfFrame` never returns in batch
mode, so a probe that screenshots stalls there -- everything before the
first screenshot still runs. This is how the rain's console error was found:
`Matrix4x4.lossyScale` asserts `ValidTRS()` and a planted instance's matrix
is not always a proper TRS, so the animals' rain-shelter lookup logged an
assertion every frame of every animal; the y column's length is the scale
and asserts nothing.

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

Water in the snow country is ice. `WaterSurface.IsFrozen(tile)` is
underwater-and-Snow by the unfrayed border (so a lake across the border
freezes up to a line); `IsOpenWater` is the other case, and `WalkingY` is the
height you stand on -- the level, over ice. `BuildMesh` draws the open
water and `BuildIceMesh` the ice (opaque, pale, a sheen, from `Paint.Flat`),
each an overlay in `ChunkManager`; `TerrainCollision` raises the collider to
the ice, so the capsule walks across and `Swimming` never sees any depth.
Everything that asks "is this water" for a foot, a ring or a splash asks
`IsOpenWater`. `Rain` snows in the snow country (`Rain.Snowing`, by the
camera's tile): the same arrays, at a fourteenth of the speed, swaying, drawn
as white lumps, and nothing rings.

`Splashes` (in `Atmosphere/`) is what water does about things going into it.
It draws drops as small flat-shaded lumps in one `RenderMeshInstanced` call,
leaves rings through `Tracks.Ring(at, size, lasts)`, and makes its own
sound. Water's sound is mostly bubbles, so the synthesis (`Splash(Kind,
seed)`) is a few dozen short sines that rise in pitch as they decay -- big
ones low and loud, small ones high and quick, most in the first moments --
over a spray of band-limited noise (a state-variable band-pass falling in
pitch, gurgled by slow noise rather than a steady hiss) and a slower, lower
slosh behind; a body going in adds a low knock and bigger bubbles as the
water closes. `Tools/probe/Sounds.cs.txt` writes the clips out as WAV to
`Tools/.check/sounds/`, and the Python after it in the session measures
each: peak time, spectral centroid and flatness (hiss is flat, near 0.5;
water is tonal, under 0.2). Rain rings the water too. `Rain` keeps its streaks in arrays and draws them
in one `RenderMeshInstanced` call (800 of them, in a 24 m disc 18 m high
that rides with the camera, leaning with a wind of its own that wanders by
Perlin noise); it used to be 260 cube GameObjects moved by hand. A streak
falls to the ground or the water under it (`FloorUnder`, kept per streak;
it used to stop three metres under the camera, in mid-air) and rings the
water where it lands. Since far more drops land than are drawn, the water
within sight is ringed on its own account: 5500 candidates a second at full
rain over a 64 m disc, kept with probability 1/(1+(r/16)^2) so the rings
are densest near and thin with distance, and scaled up by 1+r/40 so a far
one still reads. Measured from a shore in a downpour: about 650 rings on
the water at once, a fifth within 15 m and most between 15 and 40, and the
whole of the rain costs 0.2 ms a frame. `Splashes` keeps those rings in its
own list, up to 2400, drawn in batches of a thousand. The ring mesh is `Tracks.Annulus`: a ring wound
the other way is back-face culled from above and simply never appears,
which is what a home-made one did, with the count saying 146 on the water. The calls are `Step` (a foot in the
shallows), `Plunge` (going in), `Stroke` (a swimmer's arm) and `Wake` (a
ring only). It also keeps the patter of rain on water: a looping clip
(`RainOnWater`, a scatter of the smallest bubbles over a faint hiss) whose
volume follows the rain's intensity and how many rain rings are on the
water near the camera, so it is heard by a lake in the rain and not in a
field, and not at all in the snow. `Surveyor` calls them when a swung foot lands in water, when
`Swimming.Afloat` first goes true (harder for a fall or a run), once a
stroke, and on a timer while wading or swimming; `Animal.Place` calls `Step`
in place of a print when a foot comes down under the water level.
A ring is drawn from one of three annulus meshes by its age, so it thins as
it spreads; the paint is opaque, so that is how it fades. A stalking heron
only rings the water (`Wake`); a big animal or one at a run splashes.
`Tools/probe/Splash.cs.txt` finds a shore with shallows and deep water
beyond, walks in and out on camera, and counts drops, marks and sounds,
naming who made each sound.

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

### Small finds

`Finds.In(chunk, seed)` places the six small kinds (`FallenTree` .. `BrokenCart`,
appended to `LandmarkKind` with `Small = true` and `Chance = 0` so `Landmarks.Work`
never picks them): about one chunk in three that could have one does, by
its own table of countries, on a level dry tile at least three in from the
chunk's edge, and never in a chunk with a ruin or on ground a ruin's apron
reaches. `Landmarks.Occupies` covers them too (`StructureOccupies` is the
ruins alone), so the planting keeps off them. They are built by the same
`LandmarkBuilder.Build` through the partial class in `LandmarkFinds.cs`, get
a `LandmarkTag` like a ruin, and so can be drawn (`Sketching` lets a small
find be drawn from 2.5 to 18 m; a ruin wants 12 to 52) and read
(`Inscriptions` has five lines for each, what you notice rather than what
is written). `LandmarkSpawner` finds them within 7 m and logs them in
`LandmarkLog` under their chunk, which is what the book's page needs; the
labels, compass, map and journal skip `Landmarks.IsSmall` kinds.
`Tools/probe/Finds.cs.txt` counts them over 6561 chunks and photographs the
nearest of each kind.

### The book's words

Each animal kind carries `Found` (the journal line), `Country` and `Habit`
(what its page asks for) and `Notes`, a few plain lines on how it lives that
the page shows once there is at least one plate of it (`FieldGuideScreen`,
the plates branch). The arrival card (`Arrival`) mentions the plates, the
tracks and the calls; `Tools/probe/ArrivalCard.cs.txt` clears the taught
flag, makes a new world and photographs the card.

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
is derived from the measured leg, not tuned. The feet are planted: a foot on
the ground holds a point in the world and the leg is bent to reach it
(`PlaceLeg`, two bones, the fold direction found by trying both). A foot
lifts when its half of the stride is up, or when the hips have gone past
what a small sag can make up, and swings to land a step ahead of the hip;
standing, a foot the body has turned or drifted away from steps back under
its hip, one foot at a time. A landing crouches on a spring scaled by the
fall speed. The capsule's `isGrounded` flickers on a walk and is debounced.
`Tools/probe/Surveyor.cs.txt` walks, runs, turns, jumps and draws with a
side camera on the figure and reports foot skid (under 1% at a walk, about
2% at a run), how far a planted sole sits off the ground, and the landing
crouch. `Swimming` corrects float by
position, not force. The controller's step offset is 0.45 (`PlayerArmature`),
which a stair tread of 0.12-0.17 clears and a 0.45 step does not.

## The title and the worlds

The build opens on `Assets/Scenes/Title.unity` (made by `MakeTitleScene`,
first in the build settings), which holds only a camera, an event system and
`TitleMenu`. `WorldLibrary.Boot` no longer enters a world on its own: the
title asks. `WorldLibrary.Enter(save)` loads the game scene by name;
`WorldLibrary.LeaveToMenu()` saves and loads the title. `TitleMenu.IsUp` is
the flag the game's own interface checks so that nothing of it -- the pause
menu, the opening, the dev tools -- wakes under the title. Playing the game
scene straight from the editor still works: with nothing chosen it adopts a
world of its own.

Behind the title, four panoramas of the country -- the jetty at dawn, the
snow cabin at noon, the desert gate at dusk, the standing stones at night --
turn slowly and cross-fade every half minute; the game's wind and birds play
under them. They are rendered by `Tools/probe/Panorama.cs.txt` (a cubemap
from each spot, view radius 8, weather off, fog pulled in) into six faces
each, kept as 1024 JPEGs in `Assets/Resources/Title/`, and baked by
`MakePanorama` into DXT1-compressed cubemaps and skybox materials named
`View-*` -- about 6 MB each. Uncompressed they were 144 MB each and GitHub
refused them. To change a view, edit the spot in the probe, run it, copy the
faces in, and bake.

A world's settings live on `WorldSave` (weather, dayCycle, startHour,
dayLengthMinutes, animals, ruins) and are read once, where they apply:
`TimeOfDay.Start`, `Wildlife.Start`, and `Landmarks.In`, which answers
"nothing here" for a world made without ruins. An old save without them
plays as it always did. `Tools/probe/Title.cs.txt` walks the whole loop.

## The title's backdrop

Behind the title turns a panorama of the world: cubemaps baked from six
faces each, in `Assets/Resources/Title` (`pano_<view>_<face>.jpg` and a
`View-<view>` cubemap and material apiece). `TitleMenu` loads every material
there and cross-fades between them. To make a view, stand somewhere in play,
set the hour and weather on the dev tools' Animals page, and press "Capture
as ..." on the Title page: `PanoramaCapture` draws the world out to eleven
chunks, holds the fog to the sky's own colour at the horizon, renders each
face with the camera turned that way (the world only submits what the
camera can see, so a cubemap taken in one go had five faces of void), and
writes the faces to `title-views/` beside the saves. In the editor, Tools >
Tile World > Bake the title panorama brings captured views in, replacing
what was baked, and bakes them. `Tools/probe/Panorama.cs.txt` did the same
from fixed spots and made the five that ship.

## Dev tools

F8 (editor and development builds only): jump to the nearest region of each
kind, the nearest beach/lake/pond, the nearest structure of each kind; show
the opening again; wipe the world. `DevTools.cs` is under
`#if UNITY_EDITOR || DEVELOPMENT_BUILD`; `check.sh` defines
`DEVELOPMENT_BUILD` so it is compiled.

The panel has four pages, the tabs two a side of the heading. **Animals**
puts any kind down ten metres ahead (`Wildlife.Summon`, which marks it `Kept`
so the hours cull leaves it alone), stages a company, a wolf pair or a fox
after a rabbit, and tells everything within forty metres to walk, run, rest,
graze, alert, spook or hunt (`Animal.Direct`, which nothing in the game
itself uses). **Weather** holds the sky at clear, cloudy, light rain, rain
or a downpour (`TimeOfDay.ForceOvercast`; rain falls past `Rain.Threshold`,
0.55), lets it be its own again, gives a minute of rain, shows the overcast,
whether it is raining and how hard (`Rain.Intensity`) and whether the sky is
held (`TimeOfDay.OvercastHeld`), and has the hours and slow time. **Title**
captures the backdrop views. The probe `Tools/probe/DevAnimals.cs.txt`
presses the animal buttons by reflection and checks what they did;
`Tools/probe/DevPages.cs.txt` opens every page, photographs it, checks that
no two buttons on it overlap and none is outside the card, and presses
"rain". Every button is placed by hand at a fixed offset, so the overlap
check is the thing to run after adding one: the six small finds pushed the
structure list into the buttons under it, and the old "slow time" sat on
"rain".

## Performance

Measured with `Tools/probe/Perf.cs.txt` (frame times at the widest view,
standing and sprinting, then each system switched off in turn),
`Tools/probe/SaveTime.cs.txt` (a save under a stopwatch, and the worst
frame over a long stand) and `Tools/probe/Spike.cs.txt` (every frame over
30 ms across a sprint and a stand, with what came into the world in it).
On the development Mac at 1400x900, view radius 8, vsync off:

- a frame is about 2.5 ms standing or sprinting (p95 3 ms); switching off
  any one system -- tracks, wildlife, the animals themselves, fireflies,
  birdsong, wind, the compass, the labels, the clock, the surveyor -- moves
  it by 0.2 ms at most, and hiding every renderer barely moves it, so the
  frame is engine overhead, not the game;
- a save takes under 2 ms; a 75 s stand had no frame over 17 ms;
- the one long frame (47 ms) was `LandmarkSpawner.Refresh` building every
  ruin and find within the view radius in one frame on the first chunk
  crossing -- fifty of them. `Refresh` now queues them nearest first and
  `BuildQueued` builds three a frame.

Chunks build four a frame (`chunksPerFrame`) and gave no long frame at a
sprint. If a hitch turns up, run `Spike.cs.txt` first: it names what arrived
in the frame.

## Conventions

- Comments explain *why*, in plain prose, and say what went wrong before when
  that is why the code is the way it is.
- Commit messages: what changed and why, as prose. No trailers, no tool or
  assistant attribution anywhere in the repository.
- Commit as each piece of work completes; push.
- Do not commit probes, builds, or `Tools/.check/`.
