# Tile World

A third person exploration game in Unity. You play a surveyor in an abandoned world, walking around drawing what you find: the animals, the ruins, and the land itself. The world is generated from a seed as you walk into it, so it goes on forever and nothing needs to be stored.

![Two Forester's Watches on a reed lake](docs/images/hero.png)

*Two Forester's Watches on a reed lake.*

**Contents:** [The world](#the-world) · [Structures](#structures) · [Animals](#animals) · [The sketchbook](#the-sketchbook) · [The map](#the-map) · [Worlds and saving](#worlds-and-saving) · [Controls](#controls) · [Running it](#running-it) · [Code layout](#code-layout)

## The world

Everything comes from the seed and a position. Terrain height, which tile goes where, which region you're in, where the structures are and even which planks have fallen off them are all recalculated whenever they're needed. A save file is just the seed plus where you've been and what you've found. The same seed always gives you the same world.

The ground is a grid of tiles from a low poly tile pack, streamed in chunks of 15x15 and drawn with GPU instancing. Heights come from layered noise (a large scale mask picks where the mountains go, ridged noise shapes the ranges, smaller noise rolls the ground in between) and snap to steps so the tiles stay flat. A separate collision mesh ramps between the steps so you can walk up a hill instead of catching on every ledge.

![A jetty and a lighthouse at dawn](docs/images/lake.png)

*A jetty and a lighthouse at dawn.*

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

*The Sand Gate next to an oasis.*

### Water

Water sits at one level across the whole world, and what kind of water it is depends on where you are. In a Water region it's open water with a beach: sand in the shallows, rock deeper down, and a strip of sand above the waterline. Everywhere else it's a lake or a pond (the difference is depth) with a mud bottom and reeds along the edges. In a snowfield the lakes are frozen with a stone bed. Lakes are deep enough to swim in. Walk in and you float, the view goes green and the fog closes in while you're under.

### Snow

Snow covers the ground above the snowline and all of a snowfield, with a ragged edge instead of a clean contour. It's a thick layer that hangs over the edges of the tiles like the grass does on the normal ones, and it has no collider, so you wade through it about a boot deep. Snow-covered trees and pines come with it.

![The Trapper's Cabin, with the desert in the distance](docs/images/snow.png)

*The Trapper's Cabin, with the desert in the distance.*

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

*The Toadstool Ring.*

They're built from a kit of parts: log walls, stone walls, timber framing, plank/thatch/slate roofs, doors, windows, round towers, battlements, piers, and a pile of props. All of it is generated geometry that takes its colours from the tile pack's palette texture, so the buildings use the same material as the ground.

Every structure is a ruin. Each one has a decay value from 0 (intact) to 1 (about to fall over) and every part of the kit reacts to it: wall tops crumble, roofs lose sections, doors hang open or fall off, railings break, windows lose their glass, grass grows through the floor, wood goes grey. On top of that the biome adds its own wear. Vines and moss in the forest, snow on the roofs and drifted against the walls, sand piled up against the gate, char where the camp burned down. Each structure also has its own specific damage, like a hide with one stilt gone that hangs at an angle, a breached wall, a snapped mast, or a lighthouse with the light out.

![The Lighthouse at dusk](docs/images/dusk.png)

*The Lighthouse at dusk.*

Walk up to a structure and it gets added to your map with its name, and there's something written at each one. Climb the tall ones and you survey the area around them, which fills in a chunk of the map without having to walk it. Once it's dark you can rest at any structure you've found and skip to morning.

![The Buried Tower](docs/images/ruin.png)

*The Buried Tower.*

## Animals

Nineteen kinds, each keeping to its own country and its own hours, so which one you meet is a question of where you're standing and what time it is.

![A deer walking](docs/images/deer.png)

*A deer walking.*

| Animal | Where | When | What it does |
| --- | --- | --- | --- |
| Deer | wooded lowland | dawn and dusk | grazes in loose herds; a stag bellows at dusk |
| Rabbit | open meadow | day | hops, sits up, bolts; hunted by foxes |
| Fox | lowland | night | trots low, pounces; hunts rabbits, marmots and frogs |
| Goat | bare rock, high up | any | picks its way along ground you'd fall off |
| Tortoise | desert sand | the heat of the day | never runs: pulls into its shell and waits you out |
| Wolf | snowfields | dusk to morning | travels in pairs, gives ground and comes back, howls at night; hunts hares |
| Heron | the shallows of any lake | day | stands still, spears at fish, sometimes comes up with one; flies off |
| Boar | dead wood and mushroom woods | most of the day | roots with its snout; faces you and stamps before it goes |
| Raven | dead wood | day | hops and pecks on the ground; flies off |
| Marmot | stone barrens and peaks | day | sits up on a rock keeping watch; whistles and vanishes down a burrow |
| Crab | beaches | any | scuttles sideways, stands its ground with its claws up; under the sand when pushed |
| Owl | low woods | night | perches on a ruin or a dead snag and turns its head, one way then the other |
| Frog | pond shallows | dusk and night | sits at the edge, calls in chorus, plops under when you come near |
| Bat | over water | dusk and night | never lands; jinks about over the water after insects |
| Hedgehog | low woods, leaf litter | night | curls into a ball when you're near, then unrolls and trundles off |
| Fish | deep water | any | out of sight below the surface; rises for a few seconds and leaves a ring |
| Eagle | over the peaks | day | circles a long way up on set wings; never comes down |
| Hare | snowfields | day | goes flat and still when it sees you, then bolts faster than anything |
| Scorpion | desert sand | night | raises its sting at you; goes under the sand |

![An eagle circling](docs/images/eagle-over-the-snow.png)

*An eagle circling.*

They move properly. Each leg is two bones bent to put its foot on the ground under it, so a planted foot stays where it was set down while the body passes over it, and an animal across a hillside stands with its uphill legs folded and its downhill legs straight. Ears and tails lag the head and body on springs. No two of a kind are the same size or the same shade, and herds have this year's young in them, small and kept close.

![A hare gone flat](docs/images/hare.png)

*A hare gone flat.*

They react to each other, not just to you. An alarm carries: whatever puts one animal to flight sends everything within earshot off too, a beat later, so a herd goes in a wave and one marmot's whistle empties the slope. Foxes hunt rabbits and wolves hunt hares. The quarry runs, the hunter lunges at the last stride and misses, and the chase is the thing worth watching. Groups have a leader the rest keep up with, a wolf's howl gets taken up by the pair, a call gets an answer, and birds roost at night rather than disappearing.

![A fox after a rabbit](docs/images/chase.png)

*A fox after a rabbit.*

They leave signs. Prints in snow and sand that fade over a few minutes, ground turned over where a boar has been rooting, a feather where a bird went up, a ring where a fish rose, and trails worn where animals keep passing. You can find an animal by what it left.

![Tracks in the snow](docs/images/prints.png)

*Tracks in the snow.*

How close they let you get depends on how you move. Run at them and they're gone; walk, or stand still, and they settle. Some never run at all: a tortoise shuts its shell, a hedgehog curls up, a scorpion puts its sting up.

![A scorpion with its sting up](docs/images/scorpion.png)

*A scorpion with its sting up.*

## The sketchbook

![The sketchbook's contents](docs/images/sketchbook.png)

*The sketchbook's contents.*

Press G. This is the main point of the game.

The book has an entry for every animal and every kind of structure. An animal's entry needs a drawing from up close, something seen of how it behaves, and it found in its home region. A structure's entry needs a drawing with the whole thing on the page, plus reading whatever is written there. The empty entries are what tell you where to go next.

Hold F and the view narrows to what will fit on the page, scroll to zoom. Get close enough, keep it in frame and stand still and the drawing fills in over a few seconds. Moving ruins it. The book grades the result (how much of the page it fills, whether it's cut off, whether you got the side or just the back of it walking away) relative to what's actually possible for that subject, and keeps your best one. The drawing is real: it captures the subject as it was, from where you stood, and renders it as ink on the map paper.

![Three drawings from the book: the Toadstool Ring, the Hilltop Beacon, the Fishing Jetty](docs/images/drawings.png)

*Three pages from the book, as the game drew them: the Toadstool Ring, the Hilltop Beacon and the Fishing Jetty, each from where the player stood.*

The book also notes things it sees on its own, like three deer together, a fox out at night, or still water after dark. There are about forty of these, none required. When it runs out it tells you what it's curious about, which usually means going out at a time of day you haven't tried.

The first few minutes, on a brand new save only, walk you through this with one animal placed in front of you and a couple of prompts.

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
