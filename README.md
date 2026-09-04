# Tile World

A third-person exploration game in Unity about surveying an abandoned world. The land is generated from a seed as you walk into it — eleven kinds of country, water of three sorts, snow above the treeline, and the ruins of the people who were here before you — and your job is to draw it: the animals, the structures, and what the place is like at each hour.

![The Forester's Watch, twice over, on a reed lake at morning](docs/images/hero.png)

## The world

Everything is a pure function of the seed and a position. Terrain height, which tile stands where, which region a place belongs to, where the structures are and what has fallen off them — none of it is stored. A save is the seed plus where you have been and what you have found, and the same seed always gives the same world, to the last fallen plank.

The ground is a grid of the tile pack's blocks, streamed in chunks of fifteen and drawn with GPU instancing. Heights come from layered noise — a continental mask decides where the mountains are, ridged noise shapes the ranges, smaller noise rolls the ground between — and snap to terraces so the tiles stay flat. A separate collision mesh ramps between the terraces, so you walk up a hillside instead of catching on every step.

![A jetty and a lighthouse on a lake at dawn](docs/images/lake.png)

### Regions

The world is divided into regions about 240 metres across, each with a character worked out from its ground — how high it is, how wet, how much of it lies under snow — and a name in the manner of a place ("the Silent White", "Deepspires", "Weathered Holt"). The borders between regions wander rather than run straight, and the last few tiles of one are scattered into the next, so a wood frays out into the sand instead of stopping on a line.

| Region | Where it forms | What it looks like |
| --- | --- | --- |
| **Lowland** | low, dry ground | meadow, scattered trees, fireflies at night |
| **Forest** | middling ground | dense broadleaf woods over grass |
| **Hills** | higher ground | thinning trees, the ground paling as it climbs |
| **Peaks** | ground a good deal of which is under snow | bare rock and summits |
| **Water** | a region wet enough to be named for it | open water with sand shores — the beaches |
| **Reedbed** | damp ground short of open water | reeds standing in the shallows, dark wet ground |
| **Fungal** | low ground, rarely | toadstools in every colour, the ground darker under them |
| **Desert** | low, dry, open ground | sand over everything, cacti, palms, dead trees |
| **Snowfield** | a plain that never thaws | thick snow, snow pines, frozen lakes with stone beds |
| **Stone barrens** | higher ground, rarely | bare rock, boulders, nothing growing |
| **Dead wood** | low ground, rarely | trees that died standing, dark ground |

![The Sand Gate and an oasis](docs/images/desert.png)

### Water

Water is one level across the world, and what a body of it is depends on where. In a Water region it is open water with a **beach**: sand in the shallows, rock below, and a strand of sand above the waterline. Anywhere else it is a **lake** or a **pond** — the difference is depth, not width — with a soft mud bottom and reeds standing in the shallows. In a snowfield the water is frozen and its bed is bare stone. Lakes are deep enough to swim: walk in and you float, with the view going green and the fog closing in while your head is under.

### Snow

Snow lies where the ground is high enough or the region is a snowfield, with a ragged edge rather than a contour. It is a thick blanket rather than a sheet — it hangs over the edge of each tile the way the pack's grass does, and it has no collider, so you walk through it, a boot deep. Snow trees, snow pines and a stone bed under the frozen lakes come with it.

![The Trapper's Cabin in a snowfield, with the desert beyond](docs/images/snow.png)

### Time and weather

A day and a night take twenty minutes. Dawn and dusk go orange and the light rakes across the country, night is dark but navigable with stars out, and the sky, the ambient light and the fog follow along. Weather drifts on its own noise: it clears and clouds over, the sun goes flat and grey, shadows soften, fog pulls the horizon in, and it rains when the overcast gets heavy. Wind rises with altitude and with the weather; birds call in the daytime lowlands. All of the sound is synthesised in code.

## The structures

Fifteen kinds of structure stand in the world, each belonging to one kind of country, so finding a biome means finding what was built in it:

| Structure | Country |
| --- | --- |
| Forester's Watch, Hunter's Hide | forest |
| Trapper's Cabin | snowfield |
| Sand Gate, Buried Tower | desert |
| Fishing Jetty | reedbed |
| Stepped Altar | stone barrens |
| Toadstool Ring | fungal woods |
| Charcoal Camp | dead wood |
| Hilltop Beacon | hills |
| Summit Cairn | peaks |
| Wayside Shrine, Standing Stones | lowland |
| Lighthouse, Shipwreck | beaches |

![The Toadstool Ring](docs/images/fungal.png)

They are built from a kit of parts — log walls, coursed stone, timber framing, plank and thatch and slate roofs, doors, windows, round towers, battlements, jetties, props — all generated as flat-shaded geometry that takes its colour from the tile pack's own palette, so a cabin is the same material as the ground it stands on.

Every one of them is a ruin. Each carries a *decay* — nought is kept, one is barely standing — and every part of the kit consults it: walls crumble along their tops, roofs come down a section at a time, doors hang open or lie where they fell, rails break, glass goes, floors grass over, wood greys. On top of that, the country does what it does to a place left alone: **vines and moss** in the woods, **snow** capping what is left of a roof and drifting against the walls, **sand** banked against a gate and lying in mounds across its court, **char** where a camp burned. Each structure has its own collapse written in as well — a stilt gone under a hunter's hide so the whole thing hangs at a tilt, a curtain wall breached, a mast snapped, a lighthouse dark.

![The Lighthouse at dusk](docs/images/dusk.png)

Walk up to a structure and it goes on your map with its name; something is written at each one. Climb the ones with height and you survey the country round them, which fills in a circle of your map without walking it. Once it is dark you can rest at any you have found, and wake at morning.

![A tower half under the sand](docs/images/ruin.png)

## What lives there

Deer in the wooded lowlands at either end of the day, rabbits in open meadow while it is light, foxes over the same ground after dark, and goats on the high bare rock where none of the others go. Which animal you meet is a question of where you are standing and what time it is.

You hear them before you see them, from where they actually are. They belong to the ground: an animal lies along a hillside rather than standing upright on it, eases onto the terraces, goes slower in the rain, beds down when the weather closes in, keeps clear of the ruins, and holds your eye when it has seen you. Each kind moves in its own way — a deer trots and bounds, a rabbit hops, a fox stays low, a goat picks its way — with knees that fold and straighten. They graze, wander, drink, rest and drift toward their own kind. How close they let you depends on how you move: run and they are gone; walk, or stand still, and they settle.

## The sketchbook

Press G. This is the game.

The book has an entry for every creature and every kind of structure. A creature's wants a drawing made from close by, something seen of how it lives, and it found in the country it belongs to. A structure's wants a drawing that fits the whole of it on the page, and what is written there read. What you have not done yet is the only thing that tells you where to go next.

Hold F and the view narrows to what the page will take, with the wheel to zoom. Get near enough, keep it in sight and stand still, and the drawing fills in over a few seconds; real movement spoils it. The book judges the result — how much of the paper the thing fills, whether it runs off the edge, whether you caught its side or its back end going away — against what is actually possible for that subject, and keeps your best. The drawing is a real one: the subject is caught as it stood, from where you were, and worked into ink and hatching on the map's paper.

The book also keeps its own record of what it has noticed — three deer on the same ground, a fox abroad in the dark, still water at night — forty-odd true things about how the place works, with the region and the hour. When it runs dry it says what it is wondering about, which sends you out at an hour you had not tried.

The first few minutes, once ever, teach this by doing: a page at the start, one animal put out in front of you, and nothing said again until it would be useful.

![The Summit Cairn on its hill](docs/images/cairn.png)

## The map

Press M. Chunks you have walked are shaded by height with water and snow marked; ground you have only surveyed from a height is drawn faded; structures you have found are diamonds with their names. Click to mark a spot, and a compass across the top carries every found structure and your mark. J opens the field journal, which lists everything found and noticed.

## Worlds and saving

The world saves itself every thirty seconds and when you quit. Escape, then Worlds, lists what you have made — name, seed, how much charted, what found, when last there — and you can make a new one with any name and seed, or none, and take what you are given.

## Controls

- WASD to move, mouse to look, Left Shift to sprint, Space to jump
- Walk into deep water to swim
- G for the sketchbook; hold F to draw what you are looking at, wheel to zoom
- M for the map, J for the journal
- E to rest at a structure you have found, once it is dark
- Click the map to mark a spot, right click to clear it
- F9 saves the map as an image, F3 shows world statistics
- Escape backs out of whatever is open, then pauses
- F8, in the editor and development builds only, opens the dev tools: jump to the nearest region of any kind or water of any sort, replay the opening, or wipe the world

## Running it

Open the project in Unity 6000.2.13f1 and load `Assets/Scenes/SampleScene`. World Seed on the WorldCreator object is 0 by default, which picks a random world each run. View Radius is the draw distance, in chunks; the screenshots here were taken at its maximum of 8, and if it runs badly that is the first thing to turn down.

## The code

- `World/` — the grid and the terrain function, chunks and their collision, water, snow, regions, planting, and the streaming
- `Landmarks/` — where structures go, the kit they are built from, how they are weathered, and what is written at them
- `Player/` — the surveyor's figure and its animation, the follow camera, swimming, the underwater view
- `Interface/` — the map, journal, sketchbook, compass, notices, the opening, pause menu and worlds screen
- `Wildlife/` — what lives where, how it is built and moves, what it does when it sees you, and the book
- `Atmosphere/` — the day cycle and weather, wind, birdsong, music, rain, stars and fireflies
- `Systems/` — saving, the library of worlds, and the dev tools

How it is organised, what must stay true, and how to build, run and photograph it from the command line without opening the editor: [docs/HANDBOOK.md](docs/HANDBOOK.md). The scripts it refers to are in `Tools/`.
