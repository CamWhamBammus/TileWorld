# Map the Unknown

**Map the Unknown** is a Unity third-person exploration game built around a procedurally generated, chunk-based infinite world. The main focus of the project is the world generation system: as the player moves through the environment, nearby chunks are generated, loaded, tracked, and rendered dynamically.

## Gameplay

The player explores a randomized forest-like world and completes quests centered around discovery and exploration. The objective is to move through the generated environment, uncover new chunks, and use the quest menu to track progress.

## Main Objective

Complete the exploration quests by discovering new chunks of the infinite world.

The game is designed around the idea that the world continues beyond the player’s starting area, encouraging movement, exploration, and discovery.

## Controls

- **WASD** — Move
- **Mouse** — Move camera / look around
- **Space** — Jump
- **Q** — Open / close quest menu

## Features

- Procedural chunk-based world generation
- Randomized tile placement using noise
- Infinite exploration-style map structure
- Dynamic chunk loading around the player
- Mesh instancing with `Graphics.DrawMeshInstanced`
- Third-person player controller
- Custom camera follow script
- Quest menu and exploration objectives
- Chunk discovery tracking
- UI feedback for player progress
- Sound effect when opening/closing the quest menu

## Technical Overview

The world is generated in chunks. Each chunk contains a grid of tiles, and tile types are chosen using noise-based logic. Instead of instantiating many tile GameObjects, the project uses GPU instanced rendering through `Graphics.DrawMeshInstanced`, which makes it possible to draw many repeated environment meshes efficiently.

Because the tiles are rendered as instanced meshes rather than normal GameObjects, they do not automatically have colliders. A separate invisible ground collider is used so the player can walk across the generated world.

## Project Scene

Open the main scene here:

```text
Assets/Scenes/SampleScene.unity