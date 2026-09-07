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

A probe's `Boot` (the `RuntimeInitializeOnLoadMethod`) fires again on every
scene load, since `SceneSystems` re-runs all the AfterSceneLoad hooks then:
once at the start, under the title, and again when a world is entered.
Guard it with `if (FindFirstObjectByType<_Probe>() != null) return;`
or every stage runs twice, fighting over the player. The newer probes do.
A probe that starts at the title is looking at the title's own world (seed
24, radius 8, the player switched off); enter a world first for anything
that needs the player.

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

On the ice: `WaterSurface.BuildIceCrackMesh` lays chains of thin dark strips
(one tile in nine seeds a chain of three to six segments that stays on the
ice) and `BuildIceDriftMesh` lays thin snow slabs where Perlin noise says and
along every edge with the bank; both are overlays in `ChunkManager` like the
ice. `Fauna.Ground` and `Animal` ask `IsOpenWater` and `WalkingY`, so a frozen
lake is ground to an animal: a hare put down on it stands on it
(`Fauna.Ground` true there). The surveyor on ice: `Surveyor.OnIce` drops the
controller's `SpeedChangeRate` from 10 to 1.6, so getting going and stopping
take a while (about a metre and a half of slide from a walk), and a step has
a two-in-five chance of a creak (`Splashes.Creak`, a low bending tone).
Boot prints: `Tracks.Boot` on every planted foot, in snow and sand, drawn
longer than a paw and skipped by `Tracks.Near` so they are not read as an
animal's. Breath: in the snow country `Surveyor.Water` puffs every three
seconds standing, every second and a half running (`Splashes.Puff`: lumps
that slow, rise, swell and go). Drips: for seven seconds after leaving the
water, drops off the figure that fall to the ground under it
(`Splashes.Drip` with a floor). The map draws a chunk mostly under ice in
an ice colour.

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
water is tonal, under 0.2). Rain is heard everywhere it falls: `Rain` keeps a looping bed (`RainBed`,
noise through a low and a high band-pass, gurgled) at 0.38 of the
intensity, silent in snow. A downpour (intensity past 0.7, not snowing)
strikes every nine to twenty-six seconds: `TimeOfDay.Flash` lifts the sun
and the ambient for a tenth of a second, and a `Rumble` (low noise that
rolls) plays one to four and a half seconds later, quieter the later. The
ground wets at 0.12 a second of intensity and dries at 0.028 a second
(`ChunkManager.SetWetness`), which raises `_Smoothness` toward 0.6 on runtime
copies of the ground tiles' materials -- copies, so the assets are not
changed by a play session in the editor.

Rain rings the water too. `Rain` keeps its streaks in arrays and draws them
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

## The picture

`Grading` (in `Atmosphere/`) puts a global `Volume` over the game camera with
a runtime `VolumeProfile` -- `Tonemapping` (Neutral), `Bloom`, `Vignette`,
`ColorAdjustments`, `WhiteBalance`, `LiftGammaGain` -- and turns on the
camera's post-processing and SMAA through `UniversalAdditionalCameraData`.
Every frame it drives the grade from `TimeOfDay` (the sun's height, the
gold hour near the horizon, night) and the weather (`Overcast`,
`Rain.Intensity`) and the camera's region (snow): exposure, contrast,
saturation, a colour filter, white balance, a blue lift in the shadows at
night, more bloom at the gold hour. `Grading.Enabled = false` is the raw
picture, for comparing. The PC renderer's `postProcessData` is set, which is
what keeps the post-processing shaders in a build. The title, being the
same scene, is graded too.

The water is `Assets/Shaders/TileWorldWater.shader`, kept in the build by
`Resources/Water.mat` (a material referencing it -- a shader found only by
`Shader.Find` is stripped), which `WaterSurface.CreateMaterial` instances.
It needs the depth and opaque textures (on in `PC_RPAsset`, and forced on
the camera by `Grading`). Everything is worked from world position and
`_Time`: the mesh is bare quads. Depth of water = scene eye depth minus
surface eye depth; colour lerps shallow to deep over `_DepthFade`; the bed
is the opaque texture sampled through a refraction offset, unless that
would drag in something above the surface; foam where the depth is under
`_FoamDepth`, broken by a moving pattern; the sun's glint is a Blinn
highlight on a procedural normal; the sky at a low angle is the fog colour
by a Fresnel term; fog mixed in by hand. It writes opaque (`Blend Off`,
`ZWrite Off`) since it composes the bed itself, so rings and drops just
above the surface still win the depth test. `Tools/probe/Look.cs.txt`
photographs the same shore graded and ungraded, at dusk, at night, in the
rain, close on the water with the surveyor wading, and a frozen lake, and
counts magenta pixels -- the colour of a shader that failed to compile.

### Ten more, none of them touching a tile

All of these act on the image, the lights, the air or on things drawn by
code, so the tiles can be replaced under them.

- **Pipeline** (`PC_RPAsset`, `PC_Renderer`): MSAA 4, shadow distance 130 m
  with cascade splits 0.05/0.15/0.4, normal bias 0.65, SSAO intensity 0.7
  radius 0.55.
- **Sky** (`TimeOfDay.Apply`): the procedural sky's sun swells and softens
  near the horizon (`_SunSize`, `_SunSizeConvergence`), the tint warms at the
  gold hour, and `_GroundColor` is the haze colour so the horizon is a band.
- **Cloud shadows** (`TimeOfDay.Clouds`): three wrapping fbm cookies of
  rising contrast on the sun (`Light.cookie`, `lightCookieSize` 260 m),
  chosen by overcast band, drifting with `Rain.Wind` through
  `lightCookieOffset`; none when clear or fully overcast.
- **Moon** (`TimeOfDay.Moon`): a disc and a halo 850 m off along the moon
  light, drawn with `TileWorld/Glow` (unlit, additive, no fog; kept in the
  build by `Resources/Glow.mat`), fading under cloud; `_MoonDir` and
  `_MoonColor` are set as shader globals and the water shader adds a moon
  glint from them.
- **Grade additions** (`Grading`): Bokeh depth of field when
  `Sketching.Working` at `Sketching.FocusDistance` (the distance to what is
  under the glass); film grain by night and rain; and under the water
  (`Submerged`) a blue-green filter, lens distortion, chromatic aberration
  and a closed vignette.
- **Lanterns** (`Kit.Builder.Lamps`, `LanternLights`): the kit records each
  lamp it hangs, `Finish` attaches `LanternLights`, and at night the lamps
  within 55 m of the camera get a warm point light and a glow disc, up to
  eight burning across the world, flickering by noise.
- **Motes** (`Motes`): leaves in the woods (three tints, a folded lozenge,
  tumbling), seeds over the low ground, dust over the sand -- by the
  camera's country, about a hundred up within 22 m, riding `Rain.Wind` and
  a sway, gone when they land or drift off, most knocked down by rain.
- **Light shafts** (`LightShafts`): when the sun is a little above the
  horizon and the sky clear, up to fourteen quads beside trunks near the
  camera (`Undergrowth.NearestTree`), each running twelve metres down the
  light's line to the ground, facing the camera about that line, fading to
  nothing at the top, drawn with the glow shader.
- **Wind** (`Undergrowth.Draw`): everything within 40 m of the player leans
  from its foot toward the wind by a gust of Perlin noise that moves over
  the ground -- grass and reeds up to about 12 degrees, trees an eighth of
  that -- by rewriting the instance matrices into a second array each frame
  (`SwayDegrees` for the probes). Further off, nothing moves.

`Tools/probe/Look2.cs.txt` reads the pipeline back, then photographs a
wood at dawn, a partly cloudy noon, the low ground, a shrine and the moon
at night, the moon on a lake, a deer under the glass, and the world from
under the water, with counts for shafts, motes, lean, lanterns and the
depth of field.

### Clouds

`Clouds` (in `Atmosphere/`) is three layers, all by `TimeOfDay.Overcast`.
Cumulus: up to 95 clusters within 1250 m at about 210 m up, each one mesh
from a bank of 24 shapes (five to ten jittered, bottom-flattened icosahedron
lumps), drawn with `Graphics.RenderMesh` one at a time, drifting on
`Rain.Wind` at two and a half times the ground wind and wrapping back to the
windward side when they pass the reach; they come and go a few a second so
a change of weather is a drift, not a switch, and they are lost under a
full ceiling. The sheet: a 3 km disc at 340 m whose cover is a four-octave
value noise in the shader thresholded by coverage, from half overcast up.
Cirrus: the same disc at 620 m, squashed and turned, thin, in clear
weather only. The shader is `TileWorld/Cloud` (kept in the build by
`Resources/Cloud.mat`): mode 0 lights lumps by the sun on their faces,
darker underneath, with a silver lining when the sun is behind them, and by
the moon through the `_MoonDir`/`_MoonColor` globals; mode 1 is the sheet.
Neither takes the fog; both melt toward the fog colour with distance and
fade out toward the horizon. `Grading` pushes the camera's far plane to
3000 m, which also brings the stars (900 m off) back into view -- they were
past the old 500 m plane. `Tools/probe/Sky.cs.txt` photographs the same
sky clear, cloudy, overcast, at sunset and at night and counts the clouds.

### The sky's company

Five more in `Atmosphere/`, all on the glow or cloud shaders and all
tile-independent. `NightSky`: about 2300 stars in one mesh on a 900 m
sphere round the camera (900 of the sky's own, the brightest big and a few
coloured, and 1400 faint ones within a few degrees of a tilted great
circle for the Milky Way, with a band of soft quads glowing behind it),
each star twinkling on a phase kept in its second uv (`_TwinkleRate` on the
glow shader), and a shooting star -- a glow disc slerped across the sky in
0.7 s -- every 18 to 55 s. The old `Starfield` is no longer spawned: its
quads used the unlit shader, which fogs, and at 900 m they were fogged to
nothing. `SunGlow`: a core and a halo 880 m along the sun's line, the halo
swelling near the horizon, and four ghost discs strung from the sun's
viewport position through the frame's centre when the sun is in it.
`Mist`: the cloud shader's sheet mode on a 260 m disc at the water level
plus 1.1 m, which is also the floor of the hollows, so it lies on lakes and
in dips and nowhere the ground rises through it; by the clock (0.19 to
0.35), heavier after rain. `Rainbow`: two rings of quads about the
anti-solar axis at 40.4-42.4 and 50.4-53.4 degrees, six colour bands, the
second reversed and fainter, shown by ground wetness with the sun up and
under forty-two degrees, and eased in and out; the terrain hides what is
under the horizon. `Flocks`: a V of seven to fifteen dark chevrons 90 to
170 m up, 250 to 450 m off, each beating its wings by mirroring the mesh
through its plane, every 45 to 110 s by day; `Flocks.Summon()` calls one
across the view. `Tools/probe/Sky2.cs.txt` photographs them all and waits
up to a minute for a shooting star.

### The wash, and the fireflies

`Surf` (in `World/`) lays two sheets on an open shore: the strand -- dry sand
within 0.7 m of the water level, the same rule as the sand tiles -- gets
quads 6 cm above the sand, and the shallows just out from it get quads 4 cm
above the water. Each vertex carries its distance from the waterline in
metres in uv.x (negative in the water; the corners take the mean of the
tiles round them so the value runs smoothly across a tile) and a Perlin
phase in uv.y. Both sheets use the water shader (`TileWorld/Water`) in its
wash mode (`_Wash` 1): real water, with the shader's refraction, sheen
and glint, and a floor of 0.45 on its tint so a thin sheet still reads as
water. The rule is that the white line is the water's edge. The sheet
stops at the wave's front (`edge`, soft over half a metre), the line sits
on that edge (`edgeFoam`, a narrow bump centred 20 cm behind it), a trail
of foam fades out behind the line (`trail`, gone within two metres, never
ahead of it), and nothing is drawn past the front, water or foam. For that
to hold on the sea side as well, the shallows have no fixed surface:
`Surf.IsSurfShallows` (open water in a Water region, no deeper than 0.6 m,
sand within five tiles) is left out of `WaterSurface.BuildMesh`, so those
tiles' only water is the shallows' sheet, which draws back with the front
and bares their sand on every wave. Deeper water keeps the sea's own
surface, so on a steep shore the wave never draws back far. On the way
out the sheet on the sand thins toward its edge (`thin`, 0.55 at the front
to full four and a half metres behind it), so the sand shows through the
last of it as it slides away. The wave has a crest: the vertex shader lifts
the sheet by `_Crest` (0.16 m) in a bump at the front, taller coming in, a
third as tall going out with a shallow trough behind it, and each tile is
cut three by three so the bump rolls rather than steps. The front runs
from five metres out to six and a half up the sand on a fourteen-second
cycle: in over the first third, held a moment at the top, drawn back over
the rest, offset by a Perlin phase per tile so the coast does not move as
one. The wave's clock is `_WashTime`, a global that `SurfSound` sets every
frame from `Surf.Now`; `Surf.Covered(point, seed)` runs the same front on
the CPU, so the Surveyor's wading and the rain's rings know whether there
is water over the shallows right now instead of trusting the tile map, and
the Beach probe's `covered ahead` line is there to check that the two
agree with the frames (they did not until the shader stopped using
`_Time`, whose zero is not the game's). `Covered` knows the strand as well
as the shallows, so the wash running over the sand counts as water
underfoot: the Surveyor's `InWater` skips its below-the-level test on the
strand, `Surf.SurfaceAt` gives the height of the water's face (the wash
sheet's, up the sand) so `Splashes` puts its rings on it (a step's ring on
the sand's lump, not under it), and `Swimming` only goes in where the
water is actually there, so nobody wades at swimming pace over sand the
wave has left. The Beach probe's `wade` line walks the shore for a wave and
counts: ten seconds of sixteen with water over the player, none of them
swimming, forty splashes over thirty-two metres. Two things that cost time: `line`
is an HLSL keyword, and a variable by that name fails the shader with
"unexpected token"; and when the water shader fails, `CreateMaterial`
returns null and the wash silently draws nothing while the sea, on the
build's fallback, still looks fine -- the build log's `Shader error` lines
are the only sign. `_Wash` 2 (foam only) is still in the shader but
unused. Lakes get none. `SurfSound` looks for the nearest strand tile twice a second and
plays a made loop of two swells on the same period, louder the nearer.
Two things that cost a few runs: one distance per tile left the foam band
-- a metre wide -- falling between two-metre steps and never landing on a
tile, hence the corner means; and never `pow(x, 2)` on a value that can go
negative in a shader, since that is NaN on the GPU and the whole quad
vanishes. Square by hand. The way to tell a shader that draws nothing from
a mesh that is not there is to swap in the glow material: if the quads
light up, the mesh is fine.

`Fireflies` was rewritten: each fly has a place in the world and a heading
that wanders by noise, and hangs where it is as the player walks by (the
old ones were offsets from the player and came along). New ones come up
ahead as the player moves and the ones left behind go. They are glow discs
drawn instanced -- the glow shader takes `multi_compile_instancing` now --
and pulse. `Tools/probe/Beach.cs.txt` photographs a wave at three moments
and moves the player twelve metres to check the first firefly does not
move with them.

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

The game has one scene, `Assets/Scenes/SampleScene.unity`, and the title is
that scene with no world chosen: `TitleMenu.IsUp` is `!WorldLibrary.HasCurrent`,
decided before anything loads, so every system can ask it from its own boot
hook whatever order the hooks run in. `TitleMenu.Spawn` (AfterSceneLoad)
puts the menu up when it is true. `WorldLibrary.Enter(save)` sets the world
and reloads the scene; `WorldLibrary.LeaveToMenu()` saves, clears it and
reloads the scene, which is the title again. Playing the scene from the
editor with nothing chosen opens on the title as well.

Behind the title is the country itself, live. Under the title
`ChunkManager.Start` takes `TitleMenu.Seed` (24, picked with
`Tools/probe/TitleSpot.cs.txt`, which looks through seeds for a beach near
the origin and says how much sea and sand is round it), draws to radius 8
whatever the view distance is set to, files nothing, and stands the player
-- switched off, unseen -- where `TitleMenu.Viewpoint` says: eight metres
back from the nearest strand to the origin, so the world is drawn round the
shore. The title's own Start, a frame later, turns the follow camera off
(`SimpleFollowCamera.Start` stands down under the title too), stands the
camera on the sand looking out to sea, turned a quarter along the shore so
the waterline crosses the picture, holds the hour at 0.69 and the sky at
0.45 overcast (clouds, no rain: rain starts at 0.55), and puts the name up.
The interface and the player's own systems stay out from under it:
`CompassBar`, `WorldLabels`, `Notices`, `Journal`, `WorldMap`,
`FieldGuideScreen`, `WorldsScreen`, `DebugOverlay`, `Sketching`, `Surveyor`,
`Swimming`, `Underwater`, `SaveCoordinator`, `RegionWatcher`, `PauseMenu`,
`Arrival` and `DevTools` all return from their boot hooks when the title is
up. The atmosphere does not: clouds, grading, the sun, mist, flocks, motes,
shafts, the rainbow, the night sky, splashes and the surf all run, so the
title is the game's own picture, wave and all. The cursor is the menu's:
`TitleMenu.Update` frees it every frame, since the player's controls lock
it whenever the window gets focus.

The name is `TitleLogo`: "TILE WORLD" in a five-by-seven block font, one
cube a cell, sand with a green top, on layer 30 three thousand metres up,
drawn by a camera of its own into a 1600 by 400 texture with nothing behind
it (fog is switched off round that camera's render and put back after),
laid at the top of the canvas over a soft shadow. The main camera's mask
leaves layer 30 out. The camera is stood back from the letters' measured
bounds so the word fills the width; the letters tilt and bob a little, and
the sun lights them, so they warm with the hour. One thing that cost an
hour: a reference to a GameObject's Transform taken before a Canvas (or any
component that wants a RectTransform) is added to it is dead afterwards,
because adding the component swaps the Transform out, and the logo went
under nothing. `canvasRoot` is taken after `AddComponent<Canvas>`.

`Tools/probe/TitleLive.cs.txt` photographs the title across a wave and once
with the card hidden, reads the logo's picture back and says how much of it
the word fills, checks what is and is not running under the title, enters a
world, comes back and checks again (17 ms a frame at radius 8);
`Tools/probe/Title.cs.txt` walks the worlds pages. The old panoramas --
cubemaps baked from six faces, 34 MB of them -- are gone with their capture
tool, the dev tools' Title page, `MakePanorama`, `MakeTitleScene`,
`PlayFromTitle` and the Title scene.

A world's settings live on `WorldSave` (weather, dayCycle, startHour,
dayLengthMinutes, animals, ruins) and are read once, where they apply:
`TimeOfDay.Start`, `Wildlife.Start`, and `Landmarks.In`, which answers
"nothing here" for a world made without ruins. An old save without them
plays as it always did. `Tools/probe/Title.cs.txt` walks the whole loop.

## Dev tools

F8 (editor and development builds only): jump to the nearest region of each
kind, the nearest beach/lake/pond, the nearest structure of each kind; show
the opening again; wipe the world. `DevTools.cs` is under
`#if UNITY_EDITOR || DEVELOPMENT_BUILD`; `check.sh` defines
`DEVELOPMENT_BUILD` so it is compiled.

The panel has three pages, the tabs either side of the heading. **Animals**
puts any kind down ten metres ahead (`Wildlife.Summon`, which marks it `Kept`
so the hours cull leaves it alone), stages a company, a wolf pair or a fox
after a rabbit, and tells everything within forty metres to walk, run, rest,
graze, alert, spook or hunt (`Animal.Direct`, which nothing in the game
itself uses). **Weather** holds the sky at clear, cloudy, light rain, rain
or a downpour (`TimeOfDay.ForceOvercast`; rain falls past `Rain.Threshold`,
0.55), lets it be its own again, gives a minute of rain, shows the overcast,
whether it is raining and how hard (`Rain.Intensity`) and whether the sky is
held (`TimeOfDay.OvercastHeld`), and has the hours and slow time. The Places page's water row has a fourth button,
the nearest frozen lake (`NearestFrozen`, `GoToFrozen`: stand on the bank
facing the ice). The probe `Tools/probe/DevAnimals.cs.txt`
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
