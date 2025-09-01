# World Generation — Function Call Hierarchy

This diagram traces the call flow from the first function invocation that triggers world generation through to completion, based on the current project scripts.

```mermaid
graph TD
  Start([Entry: UI Button OnClick or Code]) --> WG[World.GenerateWorld]

  WG --> CLR[Clear chunkDataDictionary and destroy existing chunk objects]

  WG --> LOOP_CHUNKS{For each chunk x=0..mapSizeInChunks-1, z=0..mapSizeInChunks-1}
  LOOP_CHUNKS --> CD[Create ChunkData with size, height, world, worldPosition]
  CD --> TG[TerrainGenerator.GenerateChunkData]

  TG --> LOOP_COLS{For each column x=0..chunkSize-1, z=0..chunkSize-1}
  LOOP_COLS --> BG[BiomeGenerator.ProcessChunkColumn]
  BG --> GSN[GetSurfaceHeightNoise]
  GSN --> OP[MyNoise.OctavePerlin]
  OP --> RD[MyNoise.Redistribution]
  RD --> RV[MyNoise.RemapValue01ToInt]

  BG --> LOOP_Y{For y=0..chunkHeight-1}
  LOOP_Y --> ST[Select BlockType by groundPosition and waterThreshold]
  ST --> SB[Chunk.SetBlock]

  TG --> RET1[Return chunk data]
  RET1 --> ADDDICT[Add to chunkDataDictionary]

  WG --> BUILD{Build meshes and instantiate chunks}
  BUILD --> LOOP_DATA{For each ChunkData}
  LOOP_DATA --> GM[Chunk.GetChunkMeshData]

  GM --> LTB[Chunk.LoopThroughTheBlocks]
  LTB --> BH[BlockHelper.GetMeshData]

  BH --> NBRCALL[Neighbor sample via Chunk.GetBlockFromChunkCoordinates]
  NBRCALL --> IN_RANGE[Return block in same chunk]
  NBRCALL --> WGB[World.GetBlockFromChunkCoordinates]
  WGB --> CCP[Chunk.ChunkPositionFromBlockCoords]
  WGB --> GIC[Chunk.GetBlockInChunkCoordinates]
  WGB --> NRCH[Chunk.GetBlockFromChunkCoordinates]

  BH --> FDI[BlockHelper.GetFaceDataIn]
  FDI --> GFV[BlockHelper.GetFaceVertices]
  FDI --> AQT[MeshData.AddQuadTriangles]
  FDI --> FUV[BlockHelper.FaceUVs to BlockDataManager dictionary]

  LOOP_DATA --> INST[Instantiate chunkPrefab at data.worldPosition]
  INST --> CRA[ChunkRenderer.Awake]
  LOOP_DATA --> CRGet[GetComponent ChunkRenderer]
  CRGet --> CRInit[ChunkRenderer.InitializeChunk]
  CRGet --> CRUpd[ChunkRenderer.UpdateChunk]
  CRUpd --> RM[ChunkRenderer.RenderMesh]
  RM --> MV[Set mesh vertices triangles uv and recalc normals]
  RM --> MC[Build MeshCollider]

  RM --> DONE([World generated])

  BDAwake([BlockDataManager.Awake populates block texture dictionary and tile sizes]) --> WG
```

Notes:
- Entry: GenerateWorld is not called in Start or Awake in World.cs; it appears to be invoked via UI Button (OnClick) per project docs. The flow assumes World.GenerateWorld as the entry.
- Completion: Generation is synchronous; once the final ChunkRenderer.UpdateChunk finishes, meshes and colliders are set and the world is ready.
- Dependencies: BlockDataManager.Awake should execute earlier in the scene to populate texture data for BlockHelper when building faces.
