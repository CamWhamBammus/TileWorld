# Tile World

A third person exploration game in Unity. You play a surveyor in an abandoned world, walking around drawing what you find: the animals, the ruins, and the land itself. The world is generated from a seed as you walk into it, so it goes on forever and nothing needs to be stored.

![Two Forester's Watches on a reed lake](docs/images/hero.png)

## The world

Everything comes from the seed and a position. Terrain height, which tile goes where, which region you're in, where the structures are and even which planks have fallen off them are all recalculated whenever they're needed. A save file is just the seed plus where you've been and what you've found. The same seed always gives you the same world.

The ground is a grid of tiles from a low poly tile pack, streamed in chunks of 15x15 and drawn with GPU instancing. Heights come from layered noise (a large scale mask picks where the mountains go, ridged noise shapes the ranges, smaller noise rolls the ground in between) and snap to steps so the tiles stay flat. A separate collision mesh ramps between the steps so you can walk up a hill instead of catching on every ledge.

![A jetty and a lighthouse at dawn](docs/images/lake.png)

### Regions

The world is split into regions about 240m across. Each one gets a character based on its ground (how high, how wet, how much snow) and a generated name like "the Silent White" or "Weathered Holt". Borders between regions wander instead of running in straight lines, and the last few tiles of one region get mixed into the next so a forest thins out into desert rather than just stopping.

| Region | Where it shows up | What it looks like |
| --- | --- | --- |
| Lowland | low dry ground | meadow, a few trees, fireflies at night |
| Forest | mid height | dense trees over grass |
| Hills | higher | fewer trees, paler ground the higher you go |
| Peaks | mostly above the snowline | bare rock and summits |
| Water | very wet regions | open water with sandy beaches |
| Reedbed | damp but not flooded | reeds in the shallows, dark wet ground |
| Fungal | low ground, rare | giant colourful mushrooms, darker ground |
| Desert | low, dry, open | sand everywhere, cacti, palms, dead trees |
| Snowfield | a plain that stays frozen | deep snow, snow pines, frozen lakes |
| Stone barrens | higher ground, rare | bare rock and boulders, nothing growing |
| Dead wood | low ground, rare | dead standing trees, dark ground |

![The Sand Gate next to an oasis](docs/images/desert.png)

### Water

Water sits at one level across the whole world, and what kind of water it is depends on where you are. In a Water region it's open water with a beach: sand in the shallows, rock deeper down, and a strip of sand above the waterline. Everywhere else it's a lake or a pond (the difference is depth) with a mud bottom and reeds along the edges. In a snowfield the lakes are frozen with a stone bed. Lakes are deep enough to swim in. Walk in and you float, the view goes green and the fog closes in while you're under.

### Snow

Snow covers the ground above the snowline and all of a snowfield, with a ragged edge instead of a clean contour. It's a thick layer that hangs over the edges of the tiles like the grass does on the normal ones, and it has no collider, so you wade through it about a boot deep. Snow-covered trees and pines come with it.

![The Trapper's Cabin, with the desert in the distance](docs/images/snow.png)

### Time and weather

A full day and night takes twenty minutes. Dawn and dusk go orange, night is dark but you can still see, there are stars, and the sky, ambient light and fog all follow the sun. Weather drifts on its own: it clears and clouds over, and when it closes in the light goes flat, shadows soften, the fog pulls in and it rains. Wind picks up with altitude and bad weather, birds sing in the lowlands during the day. All the sound is generated in code, there are no recordings.

## Structures

There are fifteen kinds of structure and each belongs to one kind of region, so if you want to find a biome you can look for what was built there:

| Structure | Region |
| --- | --- |
| Forester's Watch, Hunter's Hide | Forest |
| Trapper's Cabin | Snowfield |
| Sand Gate, Buried Tower | Desert |
| Fishing Jetty | Reedbed |
| Stepped Altar | Stone barrens |
| Toadstool Ring | Fungal |
| Charcoal Camp | Dead wood |
| Hilltop Beacon | Hills |
| Summit Cairn | Peaks |
| Wayside Shrine, Standing Stones | Lowland |
| Lighthouse, Shipwreck | Beaches |

![The Toadstool Ring](docs/images/fungal.png)

They're built from a kit of parts: log walls, stone walls, timber framing, plank/thatch/slate roofs, doors, windows, round towers, battlements, piers, and a pile of props. All of it is generated geometry that takes its colours from the tile pack's palette texture, so the buildings use the same material as the ground.

Every structure is a ruin. Each one has a decay value from 0 (intact) to 1 (about to fall over) and every part of the kit reacts to it: wall tops crumble, roofs lose sections, doors hang open or fall off, railings break, windows lose their glass, grass grows through the floor, wood goes grey. On top of that the biome adds its own wear. Vines and moss in the forest, snow on the roofs and drifted against the walls, sand piled up against the gate, char where the camp burned down. Each structure also has its own specific damage, like a hide with one stilt gone that hangs at an angle, a breached wall, a snapped mast, or a lighthouse with the light out.

![The Lighthouse at dusk](docs/images/dusk.png)

Walk up to a structure and it gets added to your map with its name, and there's something written at each one. Climb the tall ones and you survey the area around them, which fills in a chunk of the map without having to walk it. Once it's dark you can rest at any structure you've found and skip to morning.

![The Buried Tower](docs/images/ruin.png)

## Animals

Ten kinds so far, each keeping to its own country: deer in the wooded lowlands at dawn and dusk, rabbits in the meadows by day, foxes on the same ground at night, goats on the bare rock, tortoises on the desert sand in the heat of the day, wolves on the snowfields from dusk to morning, herons in the shallows of any lake, boar rooting in the dead and mushroom woods, ravens on the ground in the dead wood, and marmots sitting up on the rock of the stone barrens and the peaks. Which animal you run into depends on where you are and what time it is.

A few of them don't behave like the rest. A tortoise never runs: get close and it stops, pulls its head into its shell and waits you out, which makes it the one animal you can draw from arm's length. Wolves travel in pairs, give ground rather than bolting, and come back; at night they howl. Herons and ravens get away by air. A boar turns to face you and stamps before it goes. A marmot whistles, bolts a few metres and vanishes down a burrow, and comes back up once you've moved on.

You usually hear them before you see them, and the sound comes from where they actually are. They stick to the ground properly: they lean along hillsides, step up terraces, slow down in the rain, lie down when the weather turns, avoid the ruins, and watch you once they've noticed you. Each one moves differently (deer trot and bound, rabbits hop, foxes stay low, goats pick their way) and the legs have actual knees. They graze, wander, drink, rest and drift towards their own kind. How close they let you get depends on how you move. Run at them and they're gone; walk or stand still and they calm down.

## The sketchbook

Press G. This is the main point of the game.

The book has an entry for every animal and every kind of structure. An animal's entry needs a drawing from up close, something seen of how it behaves, and it found in its home region. A structure's entry needs a drawing with the whole thing on the page, plus reading whatever is written there. The empty entries are what tell you where to go next.

Hold F and the view narrows to what will fit on the page, scroll to zoom. Get close enough, keep it in frame and stand still and the drawing fills in over a few seconds. Moving ruins it. The book grades the result (how much of the page it fills, whether it's cut off, whether you got the side or just the back of it walking away) relative to what's actually possible for that subject, and keeps your best one. The drawing is real: it captures the subject as it was, from where you stood, and renders it as ink on the map paper.

The book also notes things it sees on its own, like three deer together, a fox out at night, or still water after dark. There are about forty of these, none required. When it runs out it tells you what it's curious about, which usually means going out at a time of day you haven't tried.

The first few minutes, on a brand new save only, walk you through this with one animal placed in front of you and a couple of prompts.

![The Summit Cairn](docs/images/cairn.png)

## The map

Press M. Chunks you've walked through are shaded by height with water and snow marked. Ground you've only seen from a high point is faded. Structures you've found show as diamonds with their names. Click to place a marker. The compass along the top shows every structure you've found plus your marker. J opens the journal, which lists everything you've found and noticed.

## Worlds and saving

The game saves every thirty seconds and on quit. Escape then Worlds shows all your worlds with name, seed, how much you've charted and when you last played. You can make a new one with whatever name and seed you want, or leave them blank and get random ones.

## Controls

- WASD to move, mouse to look, Shift to sprint, Space to jump
- Walk into deep water to swim
- G for the sketchbook, hold F to draw, scroll to zoom while drawing
- M for the map, J for the journal
- E to rest at a structure you've found (night only)
- Click the map to place a marker, right click to remove it
- F9 saves the map as an image, F3 shows world stats
- Escape closes whatever is open, or pauses
- F8 (editor and dev builds only) opens the dev tools: teleport to the nearest region or water type, replay the intro, or wipe the save

## Running it

Open the project in Unity 6000.2.13f1 and load `Assets/Scenes/SampleScene`. World Seed on the WorldCreator object is 0 by default, which gives a random world every run. View Radius is the draw distance in chunks. The screenshots here are at the max of 8; if it runs badly, turn that down first.

## Code layout

- `World/` - the grid, terrain function, chunks and collision, water, snow, regions, planting, streaming
- `Landmarks/` - structure placement, the building kit, weathering, inscriptions
- `Player/` - the character model and animation, follow camera, swimming, underwater view
- `Interface/` - map, journal, sketchbook, compass, notices, intro, pause menu, worlds screen
- `Wildlife/` - what lives where, how animals are built and move, their behaviour, the field guide
- `Atmosphere/` - day cycle, weather, wind, birdsong, music, rain, stars, fireflies
- `Systems/` - saving, the worlds library, dev tools

For how the code is organised, what has to stay true, and how to build and screenshot the game from the command line without opening the editor, see [docs/HANDBOOK.md](docs/HANDBOOK.md). The scripts it mentions are in `Tools/`.
