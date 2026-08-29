# Map the Unknown

A third person exploration game in Unity. The world is generated procedurally in chunks as you walk into it, and you fill in a map as you go.

![A stone circle in the forest](docs/images/hero.png)

## About

I built this to learn how procedural world generation works. The world is infinite and comes entirely from a seed, so nothing gets saved to disk and the same seed always gives you the same world.

## The world

Terrain is layered noise. A large scale mask decides which regions get mountains, ridged noise shapes the ranges themselves, and smaller noise rolls the ground in between. Heights snap to steps of 0.25 so the tiles stay flat instead of tilting.

![Forested hills](docs/images/world.png)

Collision does not follow those steps. The character controller can only step up 0.25, so a terraced collider would catch you on every ledge. There is a separate collision mesh that slopes between tile centres instead, which turns each step into a shallow ramp you can walk up. The meshes overlap by one tile at chunk borders so there is nothing to fall through at the seams.

![The height field from above](docs/images/terrain.png)

## Landmarks

Some chunks have a structure in them. There are four: an abandoned house with the roof half gone, a ruined tower, a stone circle, and a timber watchtower.

![Abandoned house](docs/images/house.png)
![Ruined tower](docs/images/tower.png)

They are placed from the seed like everything else, so they are always in the same spot for a given world. The game checks the ground across the whole footprint first and skips anywhere too uneven to build on, and keeps them away from chunk edges so one never gets cut in half by a border.

Walk up to one and it gets marked on your map.

## The map

![The map screen](docs/images/map.png)

Press M. Chunks you have been through are shaded by height, everything else is left blank, and landmarks you have found show up as diamonds. The arrow is you.

## Quests

Press Q. Five of them, all based on where you have been: explore new chunks, get a certain distance from spawn, reach all four compass directions, find landmarks, and survey a larger area.

## Controls

- WASD to move, mouse to look
- Left Shift to sprint, Space to jump
- Q for quests, M for the map

## Running it

Open the project in Unity 6000.2.13f1 and load `Assets/Scenes/SampleScene`.

Most of the settings are on the WorldCreator object. World Seed is 0 by default, which picks a random world each run; set it to a number if you want the same one every time. View Radius controls draw distance. If it runs badly, turn that down before anything else.

## Notes on the code

Chunks are generated when you get near them and dropped once you are well past. The visible set only gets rebuilt when you cross a chunk border rather than every frame.

Tiles are drawn with GPU instancing. Chunks get frustum tested each frame and the visible ones are copied into one buffer per tile type, so the number of draw calls depends on how many kinds of tile there are and not on view distance. The tile meshes are heavy, around 3900 vertices each, so culling matters a lot here and tiles do not cast shadows by default.

Nothing derived is stored. Terrain height, tile choice and landmark placement are all recalculated from the seed whenever they are needed. The only things actually tracked are which chunks you have visited and which landmarks you have found.

The landmark structures are built out of primitives because the tile pack does not have any buildings in it. Placement and discovery go through a struct and never touch the geometry, so swapping in real models only means changing `LandmarkBuilder.Build`.

## Layout

- `WorldGrid`, `WorldHeight` - chunk maths and the terrain function
- `Chunk`, `TerrainCollision` - one chunk of tiles, and the surface you walk on
- `ChunkManager` - streaming, drawing, culling, colliders
- `Landmarks`, `LandmarkBuilder` - where structures go and what they look like
- `ExplorationLog`, `LandmarkLog` - what you have found
- `WorldMap`, `QuestManager` - the map screen and the objectives
