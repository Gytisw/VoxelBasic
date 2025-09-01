```mermaid
flowchart TD
    World --> TerrainGenerator
    World --> ChunkManager
    TerrainGenerator --> BiomeGenerator
    BiomeGenerator --> BlockDataManager
    ChunkManager --> ChunkRenderer
    ChunkRenderer --> MeshBuilder
    MeshBuilder --> MeshData
    MeshData --> Renderer
```
