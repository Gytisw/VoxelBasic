# VoxelBasic

A minimal, educational voxel world built with Unity. This project focuses on clarity over complexity so you can quickly learn how voxels are generated, meshed, and rendered in chunks.

<p align="center">
  <img src="./docs/world-generation-call-graph-dark.svg" alt="World Generation Call Graph (Dark)" width="900" />
</p>

## Table of Contents
- [Overview](#overview)
- [Key Features](#key-features)
- [Project Structure](#project-structure)
- [How World Generation Works](#how-world-generation-works)
- [Meshing and Rendering](#meshing-and-rendering)
- [Getting Started](#getting-started)
- [Configuration & Customization](#configuration--customization)
- [Extending the Project](#extending-the-project)
- [FAQ](#faq)
- [License](#license)

## Overview
VoxelBasic demonstrates a straightforward pipeline for generating a voxel terrain (think Minecraft-style), carving it into chunks, and building renderable meshes based on visible faces only. The codebase is intentionally compact and broken into single-purpose scripts so you can follow the full path from an OnClick action to a fully rendered world.

## Key Features
- Chunked world with configurable chunk size and height
- Procedural terrain via multi-octave Perlin noise
- Basic biome layer logic with water/ground/air thresholds
- Efficient greedy face exposure (only visible faces are meshed)
- Separate water submesh and collision mesh
- Simple materials/texture coordination via ScriptableObject data

## Project Structure
Relevant scripts (Assets/_Scripts):
- World.cs — Orchestrates world generation and chunk lifecycle
- TerrainGenerator.cs — Iterates columns in a chunk and delegates biome rules
- BiomeGenerator.cs — Determines block types per (x,y,z) using noise and thresholds
- Chunk.cs — Chunk utilities (set/get blocks, mesh assembly helpers)
- ChunkData.cs — Stores block data and metadata for a chunk
- ChunkRenderer.cs — Converts MeshData to Unity Mesh and MeshCollider
- BlockHelper.cs — Face culling, UV assignment, and quad generation
- MeshData.cs — Buffers for vertices, triangles, collider data, water submesh
- BlockDataManager.cs — Loads texture atlas metadata (tile sizes, per-block flags)
- MyNoise.cs — Noise helpers (octave perlin, redistribution, remaps)
- NoiseSettings.cs — ScriptableObject to tune noise params
- BlockDataSO.cs — ScriptableObject with per-block textures and flags
- BlockType.cs — Enum of available blocks
- Direction.cs / DirectionExtensions.cs — Cardinal directions and vector helpers

Docs:
- docs/world-generation-call-graph.md — Mermaid source in Markdown
- docs/world-generation-call-graph.mmd — Mermaid source used for SVG export
- docs/world-generation-call-graph.svg — Light background SVG
- docs/world-generation-call-graph-dark.svg — Dark colorful SVG

## How World Generation Works
High level flow:
1. You trigger World.GenerateWorld() (via UI Button OnClick or script)
2. World resets existing chunks and iterates a 2D grid of chunk coordinates
3. For each chunk:
   - Construct ChunkData (size, height, world reference, world position)
   - TerrainGenerator.GenerateChunkData processes all (x,z) columns
   - BiomeGenerator.ProcessChunkColumn computes ground height using MyNoise and assigns block types per y
   - ChunkData is stored in the world dictionary
4. After data is filled, World builds meshes and instantiates chunk prefabs

World.GenerateWorld() produces all chunks synchronously; when it returns, meshes and colliders are ready.

## Meshing and Rendering
- Chunk.GetChunkMeshData loops over all blocks and calls BlockHelper to emit quads only where the neighbor face is exposed (e.g., neighbor is Air or non-solid)
- BlockHelper uses World.GetBlockFromChunkCoordinates to correctly fetch neighbors across chunk boundaries
- MeshData maintains separate arrays for render and collision geometry; water is issued as a second submesh
- ChunkRenderer updates Mesh and MeshCollider and recalculates normals

## Getting Started
1. Open the project in Unity (tested with recent 2022/2023 versions)
2. Ensure you have a scene with:
   - A GameObject with World, TerrainGenerator, BiomeGenerator components
   - A GameObject with BlockDataManager and a reference to a BlockDataSO asset
   - A chunk prefab that contains a MeshFilter, MeshRenderer, and MeshCollider plus ChunkRenderer
3. Press Play and trigger world generation via the UI button or call World.GenerateWorld() from a script

## Configuration & Customization
- World.cs
  - mapSizeInChunks: size of the world grid (x,z)
  - chunkSize, chunkHeight: dimensions of each chunk
  - chunkPrefab: prefab used for each chunk instance
  - mapSeedOffset: offsets noise sampling for different seeds
- BiomeGenerator.cs
  - waterThreshold: sets sea level
  - biomeNoiseSettings: reference to a NoiseSettings asset
- NoiseSettings.asset
  - noiseZoom, octaves, persistance: control terrain roughness
  - redistributionModifier, exponent: shape the height distribution
  - offest, worldOffset: sampling offsets (typo retained for consistency with code)
- BlockDataSO.asset
  - textureSizeX/Y and per-block UV tile coordinates
  - isSolid and generatesCollider flags per block

## Extending the Project
- Add block types: extend BlockType enum and update BlockDataSO
- New biomes: implement rules in BiomeGenerator or add a handler chain via BlockLayerHandler
- Performance: add async generation (jobs or coroutines), mesh combining, or chunk streaming
- Lighting: integrate simple ambient occlusion or fake lighting per face
- Save/Load: serialize ChunkData arrays and world seed/config

## FAQ
- Why synchronous generation? Simplicity and easier debugging. It’s straightforward to adapt to coroutines or jobs later.
- How are neighbors handled across chunks? BlockHelper queries World to locate the relevant neighbor chunk and local coordinates.
- Where do textures/UVs come from? BlockDataManager loads BlockDataSO to map each face to atlas tile coordinates.

## License
MIT. See LICENSE if present, or adjust for your needs.
