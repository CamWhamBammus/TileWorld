# Map the Unknown

A third-person exploration game in Unity, built around a procedurally generated, chunk-based infinite world. You walk out into a forest that has never been rendered before, chart what you find, and the map fills in behind you.

![A stone circle standing in the forest](docs/images/hero.png)

## The world

The world is generated in chunks of 15×15 tiles as you approach them, and thrown away once you are well past. Nothing is stored: terrain height, tile choice and landmark placement are all pure functions of position and the world seed, so the same seed always produces the same world, and a chunk can be asked about long before it is ever loaded.

![Forested hills, with a landmark visible in the middle distance](docs/images/world.png)

Terrain is a continent mask over ridged fractal noise: mountain ranges are a regional feature you travel toward, with rolling ground between them. Height is terraced into steps of 0.25 so tiles stay level, giving about 43 units of relief — roughly twenty-four times the player's height.

![Topographic view of the height field](docs/images/terrain.png)

The awkward part is that terraced ground is unwalkable: the character controller can only step up 0.25, so anything taller stops you dead. So the collision surface does not follow the terraces. It is a separate mesh that ramps between tile centres, turning a step into about a 7° slope against a 45° limit. **Visually terraced, physically smooth.** Collision meshes overlap one tile past each chunk edge so neighbours share vertices and there is no crack at the seam.

Terrain is traversable by construction rather than by tuning — verified over 654,267 samples: no unclimbable slope, no step deeper than a tile block, no gap at any seam.

## Landmarks

About one chunk in thirty holds something someone left behind. They are placed deterministically, skipped where the ground across the whole footprint varies too much to build on, and kept clear of chunk edges so a seam never cuts one in half.

| | |
|:--:|:--:|
| ![Abandoned house](docs/images/house.png) | ![Ruined tower](docs/images/tower.png) |
| An abandoned house — chimney standing, half the thatch fallen in | A ruined tower, fifteen high, collapsed down one side |

![Watchtower](docs/images/watchtower.png)

Walk within 18 units of one and it is recorded. Density was measured rather than guessed: they average 106 units apart, about twenty seconds of running, so there is usually one in view without the horizon being cluttered.

## The map

![The map screen](docs/images/map.png)

Press **M**. Everywhere you have been, drawn as a chart: chunks shaded by elevation, unexplored ground left as blank paper, landmarks inked as diamonds, a compass rose, and an arrowhead for where you are and which way you are facing. It reframes exploration from a number in the quest log into something you can read.

## Quests

Press **Q**. Five objectives, all built on the same exploration record the map reads, so the two can never disagree: chart new ground, walk a distance from spawn, reach all four compass headings, find landmarks, and survey deeply.

## Controls

| | |
|---|---|
| **WASD** | Move |
| **Mouse** | Look |
| **Left Shift** | Sprint |
| **Space** | Jump |
| **Q** | Quest log |
| **M** | Map |

## How it works

**Streaming.** Chunks within the view radius are generated on demand and evicted once well outside it. The visible set is rebuilt only when the player crosses a chunk border, not every frame.

**Rendering.** Every tile is drawn with GPU instancing. Chunks are frustum-tested against their bounds each frame, and the on-screen ones are copied into one instance buffer per tile type — so the draw cost is set by how many tile types exist rather than how far you can see, at around 25 draw calls regardless of view distance. Buffers are sized for the worst case when the visible set changes, so the per-frame fill never allocates.

The tiles are dense, averaging about 3,900 vertices each, which makes culling the difference between submitting 70 million vertices a frame and 20 million. Tiles do not cast shadows by default for the same reason: a shadow pass would run all that geometry a second time.

**Derive, do not store.** Terrain height, tile choice, landmark placement and the map are all recomputed from the seed rather than saved. The only state in the world is which chunks you have visited and which landmarks you have found.

## Running it

Open the project in Unity **6000.2.13f1** and load `Assets/Scenes/SampleScene`. Everything is configured on the `WorldCreator` object in the scene:

| | |
|---|---|
| **World Seed** | 0 for a new world each run, or any number to replay one |
| **View Radius** | Chunks drawn in each direction. 4 is the default; 8 is supported |
| **Tiles Cast Shadows** | Off by default — the single biggest performance lever |
| **Frustum Culling** | Leave on unless you are measuring its effect |

## Project layout

| | |
|---|---|
| `WorldGrid` | Chunk size and coordinate maths — the single source of truth |
| `WorldHeight` | The shape of the land, as a pure function |
| `Chunk` | One square of world, baked into instance arrays |
| `TerrainCollision` | The ramped surface you actually walk on |
| `ChunkManager` | Streaming, instanced drawing, culling, colliders |
| `Landmarks` / `LandmarkBuilder` | Where the structures are, and what they are made of |
| `ExplorationLog` / `LandmarkLog` | What has been found |
| `WorldMap` | The map screen |
| `QuestManager` | Objectives, read from the exploration record |

The landmark structures are assembled from primitives, since the tile art has no buildings in it. Placement, discovery, the map and the quests all go through a placement struct and never touch geometry, so swapping in modelled assets is a change to `LandmarkBuilder.Build` alone.
