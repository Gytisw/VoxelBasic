# Mesh Generation Optimization

This document covers mesh generation techniques and optimizations specifically for voxel engines built with Unity DOTS.

## Table of Contents

- [Mesh Generation Fundamentals](#mesh-generation-fundamentals)
- [Greedy Meshing Algorithm](#greedy-meshing-algorithm)
- [Level of Detail (LOD) Systems](#level-of-detail-lod-systems)
- [Optimized Mesh Data Structures](#optimized-mesh-data-structures)
- [Parallel Mesh Generation](#parallel-mesh-generation)
- [Mesh Streaming and Caching](#mesh-streaming-and-caching)
- [GPU Optimization Techniques](#gpu-optimization-techniques)
- [Performance Benchmarks](#performance-benchmarks)

## Mesh Generation Fundamentals

### Voxel to Mesh Conversion

#### Basic Mesh Generation

```csharp
[BurstCompile]
public struct BasicMeshGenerationJob : IJob
{
    [ReadOnly] public NativeArray<VoxelData> Voxels;
    [WriteOnly] public NativeArray<MeshVertex> Vertices;
    [WriteOnly] public NativeArray<int> Triangles;
    public int3 ChunkSize;
    
    public void Execute()
    {
        int vertexIndex = 0;
        int triangleIndex = 0;
        
        // Generate mesh for each voxel
        for (int y = 0; y < ChunkSize.y; y++)
        {
            for (int z = 0; z < ChunkSize.z; z++)
            {
                for (int x = 0; x < ChunkSize.x; x++)
                {
                    int index = GetVoxelIndex(x, y, z);
                    
                    if (Voxels[index].VoxelType != 0)
                    {
                        // Generate mesh for this voxel
                        GenerateVoxelMesh(
                            x, y, z, 
                            ref vertexIndex, 
                            ref triangleIndex
                        );
                    }
                }
            }
        }
    }
    
    private void GenerateVoxelMesh(
        int x, int y, int z, 
        ref int vertexIndex, 
        ref int triangleIndex)
    {
        // Generate 6 faces for the voxel
        // Each face has 2 triangles (6 vertices total)
        
        // Front face
        AddFace(
            x, y, z, 
            new float3(0, 0, 1), 
            ref vertexIndex, 
            ref triangleIndex
        );
        
        // Back face
        AddFace(
            x, y, z, 
            new float3(0, 0, -1), 
            ref vertexIndex, 
            ref triangleIndex
        );
        
        // Top face
        AddFace(
            x, y, z, 
            new float3(0, 1, 0), 
            ref vertexIndex, 
            ref triangleIndex
        );
        
        // Bottom face
        AddFace(
            x, y, z, 
            new float3(0, -1, 0), 
            ref vertexIndex, 
            ref triangleIndex
        );
        
        // Right face
        AddFace(
            x, y, z, 
            new float3(1, 0, 0), 
            ref vertexIndex, 
            ref triangleIndex
        );
        
        // Left face
        AddFace(
            x, y, z, 
            new float3(-1, 0, 0), 
            ref vertexIndex, 
            ref triangleIndex
        );
    }
    
    private void AddFace(
        int x, int y, int z, 
        float3 normal, 
        ref int vertexIndex, 
        ref int triangleIndex)
    {
        float3 voxelPos = new float3(x, y, z);
        float3 basePos = voxelPos + normal * 0.5f;
        
        // Calculate tangent and bitangent
        float3 tangent = GetTangent(normal);
        float3 bitangent = GetBitangent(normal);
        
        // Add 4 vertices for the face
        Vertices[vertexIndex++] = new MeshVertex
        {
            Position = basePos - tangent - bitangent,
            Normal = normal,
            UV = new float2(0, 0)
        };
        
        Vertices[vertexIndex++] = new MeshVertex
        {
            Position = basePos + tangent - bitangent,
            Normal = normal,
            UV = new float2(1, 0)
        };
        
        Vertices[vertexIndex++] = new MeshVertex
        {
            Position = basePos + tangent + bitangent,
            Normal = normal,
            UV = new float2(1, 1)
        };
        
        Vertices[vertexIndex++] = new MeshVertex
        {
            Position = basePos - tangent + bitangent,
            Normal = normal,
            UV = new float2(0, 1)
        };
        
        // Add 2 triangles for the face
        int baseIndex = vertexIndex - 4;
        
        Triangles[triangleIndex++] = baseIndex;
        Triangles[triangleIndex++] = baseIndex + 1;
        Triangles[triangleIndex++] = baseIndex + 2;
        
        Triangles[triangleIndex++] = baseIndex;
        Triangles[triangleIndex++] = baseIndex + 2;
        Triangles[triangleIndex++] = baseIndex + 3;
    }
}
```

### Face Culling Optimization

```csharp
[BurstCompile]
public struct FaceCullingJob : IJob
{
    [ReadOnly] public NativeArray<VoxelData> Voxels;
    [WriteOnly] public NativeArray<MeshVertex> Vertices;
    [WriteOnly] public NativeArray<int> Triangles;
    public int3 ChunkSize;
    
    public void Execute()
    {
        int vertexIndex = 0;
        int triangleIndex = 0;
        
        // Generate mesh with face culling
        for (int y = 0; y < ChunkSize.y; y++)
        {
            for (int z = 0; z < ChunkSize.z; z++)
            {
                for (int x = 0; x < ChunkSize.x; x++)
                {
                    int index = GetVoxelIndex(x, y, z);
                    
                    if (Voxels[index].VoxelType != 0)
                    {
                        // Generate mesh with face culling
                        GenerateVoxelMeshWithCulling(
                            x, y, z, 
                            ref vertexIndex, 
                            ref triangleIndex
                        );
                    }
                }
            }
        }
    }
    
    private void GenerateVoxelMeshWithCulling(
        int x, int y, int z, 
        ref int vertexIndex, 
        ref int triangleIndex)
    {
        // Check each face and only render if exposed
        if (IsFaceExposed(x, y, z, new int3(0, 0, 1)))
        {
            AddFace(
                x, y, z, 
                new float3(0, 0, 1), 
                ref vertexIndex, 
                ref triangleIndex
            );
        }
        
        if (IsFaceExposed(x, y, z, new int3(0, 0, -1)))
        {
            AddFace(
                x, y, z, 
                new float3(0, 0, -1), 
                ref vertexIndex, 
                ref triangleIndex
            );
        }
        
        if (IsFaceExposed(x, y, z, new int3(0, 1, 0)))
        {
            AddFace(
                x, y, z, 
                new float3(0, 1, 0), 
                ref vertexIndex, 
                ref triangleIndex
            );
        }
        
        if (IsFaceExposed(x, y, z, new int3(0, -1, 0)))
        {
            AddFace(
                x, y, z, 
                new float3(0, -1, 0), 
                ref vertexIndex, 
                ref triangleIndex
            );
        }
        
        if (IsFaceExposed(x, y, z, new int3(1, 0, 0)))
        {
            AddFace(
                x, y, z, 
                new float3(1, 0, 0), 
                ref vertexIndex, 
                ref triangleIndex
            );
        }
        
        if (IsFaceExposed(x, y, z, new int3(-1, 0, 0)))
        {
            AddFace(
                x, y, z, 
                new float3(-1, 0, 0), 
                ref vertexIndex, 
                ref triangleIndex
            );
        }
    }
    
    private bool IsFaceExposed(int x, int y, int z, int3 direction)
    {
        // Check if neighbor exists
        int3 neighborPos = new int3(x, y, z) + direction;
        
        // Check bounds
        if (neighborPos.x < 0 || neighborPos.x >= ChunkSize.x ||
            neighborPos.y < 0 || neighborPos.y >= ChunkSize.y ||
            neighborPos.z < 0 || neighborPos.z >= ChunkSize.z)
        {
            return true; // Face is exposed at chunk boundary
        }
        
        // Check if neighbor is empty
        int neighborIndex = GetVoxelIndex(neighborPos.x, neighborPos.y, neighborPos.z);
        return Voxels[neighborIndex].VoxelType == 0;
    }
}
```

## Greedy Meshing Algorithm

### Greedy Meshing Implementation

```csharp
[BurstCompile]
public struct GreedyMeshingJob : IJob
{
    [ReadOnly] public NativeArray<VoxelData> Voxels;
    [WriteOnly] public NativeArray<MeshVertex> Vertices;
    [WriteOnly] public NativeArray<int> Triangles;
    public int3 ChunkSize;
    
    public void Execute()
    {
        int vertexIndex = 0;
        int triangleIndex = 0;
        
        // Generate mesh using greedy meshing
        GenerateGreedyMesh(ref vertexIndex, ref triangleIndex);
    }
    
    private void GenerateGreedyMesh(ref int vertexIndex, ref int triangleIndex)
    {
        // Process each axis direction
        ProcessAxis(ref vertexIndex, ref triangleIndex, 0); // X-axis
        ProcessAxis(ref vertexIndex, ref triangleIndex, 1); // Y-axis
        ProcessAxis(ref vertexIndex, ref triangleIndex, 2); // Z-axis
    }
    
    private void ProcessAxis(ref int vertexIndex, ref int triangleIndex, int axis)
    {
        // Create lookup tables for face directions
        int3[] faceDirections = new int3[6];
        faceDirections[0] = new int3(1, 0, 0);  // +X
        faceDirections[1] = new int3(-1, 0, 0); // -X
        faceDirections[2] = new int3(0, 1, 0);  // +Y
        faceDirections[3] = new int3(0, -1, 0); // -Y
        faceDirections[4] = new int3(0, 0, 1);  // +Z
        faceDirections[5] = new int3(0, 0, -1); // -Z
        
        // Process each voxel type
        for (byte voxelType = 1; voxelType < (byte)VoxelType.Count; voxelType++)
        {
            // Find all faces of this type
            var faces = FindFacesOfType(voxelType, faceDirections[axis]);
            
            // Mesh the faces
            MeshFaces(faces, ref vertexIndex, ref triangleIndex);
        }
    }
    
    private NativeArray<Face> FindFacesOfType(byte voxelType, int3 direction)
    {
        var faces = new NativeArray<Face>(1024, Allocator.TempJob);
        int faceCount = 0;
        
        // Find all faces of the specified type and direction
        for (int y = 0; y < ChunkSize.y; y++)
        {
            for (int z = 0; z < ChunkSize.z; z++)
            {
                for (int x = 0; x < ChunkSize.x; x++)
                {
                    int index = GetVoxelIndex(x, y, z);
                    
                    if (Voxels[index].VoxelType == voxelType)
                    {
                        // Check if this face should be rendered
                        int3 neighborPos = new int3(x, y, z) + direction;
                        
                        if (ShouldRenderFace(neighborPos, voxelType))
                        {
                            faces[faceCount++] = new Face
                            {
                                Position = new int3(x, y, z),
                                Direction = direction,
                                VoxelType = voxelType
                            };
                        }
                    }
                }
            }
        }
        
        // Resize to actual count
        var result = new NativeArray<Face>(faceCount, Allocator.TempJob);
        for (int i = 0; i < faceCount; i++)
        {
            result[i] = faces[i];
        }
        
        faces.Dispose();
        return result;
    }
    
    private bool ShouldRenderFace(int3 position, byte voxelType)
    {
        // Check bounds
        if (position.x < 0 || position.x >= ChunkSize.x ||
            position.y < 0 || position.y >= ChunkSize.y ||
            position.z < 0 || position.z >= ChunkSize.z)
        {
            return true; // Face is exposed at chunk boundary
        }
        
        // Check if neighbor is different type or empty
        int neighborIndex = GetVoxelIndex(position.x, position.y, position.z);
        return Voxels[neighborIndex].VoxelType != voxelType;
    }
    
    private void MeshFaces(NativeArray<Face> faces, ref int vertexIndex, ref int triangleIndex)
    {
        // Sort faces by position and direction
        SortFaces(faces);
        
        // Mesh faces greedily
        int i = 0;
        while (i < faces.Length)
        {
            var currentFace = faces[i];
            
            // Try to extend the face in each direction
            var meshedFace = ExtendFace(currentFace, faces, ref i);
            
            // Add the meshed face to the mesh
            AddMeshedFace(meshedFace, ref vertexIndex, ref triangleIndex);
        }
    }
    
    private Face ExtendFace(Face startFace, NativeArray<Face> faces, ref int startIndex)
    {
        var extendedFace = startFace;
        int i = startIndex + 1;
        
        // Try to extend in positive direction
        while (i < faces.Length)
        {
            var nextFace = faces[i];
            
            if (CanExtendFace(extendedFace, nextFace))
            {
                extendedFace = CombineFaces(extendedFace, nextFace);
                startIndex++;
                i++;
            }
            else
            {
                break;
            }
        }
        
        return extendedFace;
    }
    
    private bool CanExtendFace(Face current, Face next)
    {
        // Check if faces can be combined
        return current.Direction == next.Direction &&
               current.VoxelType == next.VoxelType &&
               AreAdjacent(current, next);
    }
    
    private bool AreAdjacent(Face face1, Face face2)
    {
        // Check if two faces are adjacent
        int3 diff = face2.Position - face1.Position;
        
        // Faces are adjacent if they differ by 1 in one axis
        return math.length(diff) == 1f;
    }
    
    private Face CombineFaces(Face face1, Face face2)
    {
        // Combine two adjacent faces
        return new Face
        {
            Position = face1.Position,
            Direction = face1.Direction,
            VoxelType = face1.VoxelType,
            Size = face1.Size + 1
        };
    }
    
    private void AddMeshedFace(MeshedFace face, ref int vertexIndex, ref int triangleIndex)
    {
        // Add vertices for the meshed face
        float3 basePos = (float3)face.Position + face.Direction * 0.5f;
        float3 size = (float3)face.Size;
        
        // Calculate face corners
        float3[] corners = CalculateFaceCorners(basePos, face.Direction, size);
        
        // Add vertices
        for (int i = 0; i < 4; i++)
        {
            Vertices[vertexIndex++] = new MeshVertex
            {
                Position = corners[i],
                Normal = face.Direction,
                UV = CalculateUV(corners[i], face.Direction)
            };
        }
        
        // Add triangles
        int baseIndex = vertexIndex - 4;
        
        Triangles[triangleIndex++] = baseIndex;
        Triangles[triangleIndex++] = baseIndex + 1;
        Triangles[triangleIndex++] = baseIndex + 2;
        
        Triangles[triangleIndex++] = baseIndex;
        Triangles[triangleIndex++] = baseIndex + 2;
        Triangles[triangleIndex++] = baseIndex + 3;
    }
    
    private float3[] CalculateFaceCorners(float3 basePos, int3 direction, int3 size)
    {
        var corners = new float3[4];
        
        // Calculate tangent and bitangent
        float3 tangent = GetTangent(direction);
        float3 bitangent = GetBitangent(direction);
        
        // Calculate corners
        corners[0] = basePos - tangent * size.x * 0.5f - bitangent * size.z * 0.5f;
        corners[1] = basePos + tangent * size.x * 0.5f - bitangent * size.z * 0.5f;
        corners[2] = basePos + tangent * size.x * 0.5f + bitangent * size.z * 0.5f;
        corners[3] = basePos - tangent * size.x * 0.5f + bitangent * size.z * 0.5f;
        
        return corners;
    }
    
    private float2 CalculateUV(float3 position, int3 direction)
    {
        // Calculate UV coordinates based on position and direction
        return new float2(
            (position.x + position.z) * 0.1f,
            (position.y + position.z) * 0.1f
        );
    }
    
    private float3 GetTangent(int3 normal)
    {
        // Calculate tangent vector based on normal
        if (normal.x != 0) return new float3(0, 0, 1);
        if (normal.y != 0) return new float3(1, 0, 0);
        return new float3(1, 0, 0);
    }
    
    private float3 GetBitangent(int3 normal)
    {
        // Calculate bitangent vector based on normal
        if (normal.x != 0) return new float3(0, 1, 0);
        if (normal.y != 0) return new float3(0, 0, 1);
        return new float3(0, 1, 0);
    }
    
    private void SortFaces(NativeArray<Face> faces)
    {
        // Sort faces by position and direction
        // This can be optimized with a more efficient sorting algorithm
        for (int i = 0; i < faces.Length - 1; i++)
        {
            for (int j = i + 1; j < faces.Length; j++)
            {
                if (CompareFaces(faces[i], faces[j]) > 0)
                {
                    var temp = faces[i];
                    faces[i] = faces[j];
                    faces[j] = temp;
                }
            }
        }
    }
    
    private int CompareFaces(Face a, Face b)
    {
        // Compare two faces for sorting
        int posCompare = CompareInt3(a.Position, b.Position);
        if (posCompare != 0) return posCompare;
        
        return CompareInt3(a.Direction, b.Direction);
    }
    
    private int CompareInt3(int3 a, int3 b)
    {
        // Compare two int3 values
        if (a.x != b.x) return a.x.CompareTo(b.x);
        if (a.y != b.y) return a.y.CompareTo(b.y);
        return a.z.CompareTo(b.z);
    }
}
```

### Optimized Greedy Meshing

```csharp
[BurstCompile]
public struct OptimizedGreedyMeshingJob : IJob
{
    [ReadOnly] public NativeArray<VoxelData> Voxels;
    [WriteOnly] public NativeArray<MeshVertex> Vertices;
    [WriteOnly] public NativeArray<int> Triangles;
    public int3 ChunkSize;
    
    // Pre-calculated face data
    private NativeArray<byte> _faceData;
    private NativeArray<int> _faceIndices;
    
    public void Execute()
    {
        // Initialize face data
        InitializeFaceData();
        
        // Generate mesh using optimized greedy meshing
        GenerateOptimizedGreedyMesh();
    }
    
    private void InitializeFaceData()
    {
        // Pre-calculate face data for better performance
        _faceData = new NativeArray<byte>(
            ChunkSize.x * ChunkSize.y * ChunkSize.z * 6, 
            Allocator.TempJob
        );
        
        _faceIndices = new NativeArray<int>(
            ChunkSize.x * ChunkSize.y * ChunkSize.z * 6, 
            Allocator.TempJob
        );
        
        // Mark faces for each voxel
        for (int y = 0; y < ChunkSize.y; y++)
        {
            for (int z = 0; z < ChunkSize.z; z++)
            {
                for (int x = 0; x < ChunkSize.x; x++)
                {
                    int index = GetVoxelIndex(x, y, z);
                    
                    if (Voxels[index].VoxelType != 0)
                    {
                        // Mark faces for this voxel
                        MarkVoxelFaces(x, y, z, Voxels[index].VoxelType);
                    }
                }
            }
        }
    }
    
    private void MarkVoxelFaces(int x, int y, int z, byte voxelType)
    {
        // Check each face and mark if exposed
        if (IsFaceExposed(x, y, z, new int3(1, 0, 0), voxelType))
        {
            MarkFace(x, y, z, 0, voxelType);
        }
        
        if (IsFaceExposed(x, y, z, new int3(-1, 0, 0), voxelType))
        {
            MarkFace(x, y, z, 1, voxelType);
        }
        
        if (IsFaceExposed(x, y, z, new int3(0, 1, 0), voxelType))
        {
            MarkFace(x, y, z, 2, voxelType);
        }
        
        if (IsFaceExposed(x, y, z, new int3(0, -1, 0), voxelType))
        {
            MarkFace(x, y, z, 3, voxelType);
        }
        
        if (IsFaceExposed(x, y, z, new int3(0, 0, 1), voxelType))
        {
            MarkFace(x, y, z, 4, voxelType);
        }
        
        if (IsFaceExposed(x, y, z, new int3(0, 0, -1), voxelType))
        {
            MarkFace(x, y, z, 5, voxelType);
        }
    }
    
    private bool IsFaceExposed(int x, int y, int z, int3 direction, byte voxelType)
    {
        // Check if neighbor exists
        int3 neighborPos = new int3(x, y, z) + direction;
        
        // Check bounds
        if (neighborPos.x < 0 || neighborPos.x >= ChunkSize.x ||
            neighborPos.y < 0 || neighborPos.y >= ChunkSize.y ||
            neighborPos.z < 0 || neighborPos.z >= ChunkSize.z)
        {
            return true; // Face is exposed at chunk boundary
        }
        
        // Check if neighbor is different type or empty
        int neighborIndex = GetVoxelIndex(neighborPos.x, neighborPos.y, neighborPos.z);
        return Voxels[neighborIndex].VoxelType != voxelType;
    }
    
    private void MarkFace(int x, int y, int z, int faceIndex, byte voxelType)
    {
        // Mark face in face data array
        int voxelIndex = GetVoxelIndex(x, y, z);
        int faceDataIndex = voxelIndex * 6 + faceIndex;
        
        _faceData[faceDataIndex] = voxelType;
        _faceIndices[faceDataIndex] = faceDataIndex;
    }
    
    private void GenerateOptimizedGreedyMesh()
    {
        int vertexIndex = 0;
        int triangleIndex = 0;
        
        // Process each voxel type
        for (byte voxelType = 1; voxelType < (byte)VoxelType.Count; voxelType++)
        {
            // Find all faces of this type
            var faces = FindFacesOfType(voxelType);
            
            // Mesh the faces
            MeshFaces(faces, ref vertexIndex, ref triangleIndex);
        }
    }
    
    private NativeArray<Face> FindFacesOfType(byte voxelType)
    {
        var faces = new NativeArray<Face>(1024, Allocator.TempJob);
        int faceCount = 0;
        
        // Find all faces of the specified type
        for (int i = 0; i < _faceData.Length; i++)
        {
            if (_faceData[i] == voxelType)
            {
                int voxelIndex = i / 6;
                int faceIndex = i % 6;
                
                int3 voxelPos = IndexToPosition(voxelIndex);
                int3 faceDirection = GetFaceDirection(faceIndex);
                
                faces[faceCount++] = new Face
                {
                    Position = voxelPos,
                    Direction = faceDirection,
                    VoxelType = voxelType
                };
            }
        }
        
        // Resize to actual count
        var result = new NativeArray<Face>(faceCount, Allocator.TempJob);
        for (int i = 0; i < faceCount; i++)
        {
            result[i] = faces[i];
        }
        
        faces.Dispose();
        return result;
    }
    
    private int3 GetFaceDirection(int faceIndex)
    {
        // Get face direction from face index
        switch (faceIndex)
        {
            case 0: return new int3(1, 0, 0);  // +X
            case 1: return new int3(-1, 0, 0); // -X
            case 2: return new int3(0, 1, 0);  // +Y
            case 3: return new int3(0, -1, 0); // -Y
            case 4: return new int3(0, 0, 1);  // +Z
            case 5: return new int3(0, 0, -1); // -Z
            default: return int3.zero;
        }
    }
    
    private void MeshFaces(NativeArray<Face> faces, ref int vertexIndex, ref int triangleIndex)
    {
        // Sort faces by position and direction
        SortFaces(faces);
        
        // Mesh faces greedily
        int i = 0;
        while (i < faces.Length)
        {
            var currentFace = faces[i];
            
            // Try to extend the face in each direction
            var meshedFace = ExtendFace(currentFace, faces, ref i);
            
            // Add the meshed face to the mesh
            AddMeshedFace(meshedFace, ref vertexIndex, ref triangleIndex);
        }
    }
    
    // ... (rest of the methods from the previous greedy meshing implementation)
}
```

## Level of Detail (LOD) Systems

### LOD Mesh Generation

```csharp
[BurstCompile]
public struct LODMeshGenerationJob : IJob
{
    [ReadOnly] public NativeArray<VoxelData> Voxels;
    [WriteOnly] public NativeArray<MeshVertex> Vertices;
    [WriteOnly] public NativeArray<int> Triangles;
    public int3 ChunkSize;
    public int LODLevel;
    
    public void Execute()
    {
        // Generate mesh based on LOD level
        switch (LODLevel)
        {
            case 0:
                GenerateHighDetailMesh();
                break;
            case 1:
                GenerateMediumDetailMesh();
                break;
            case 2:
                GenerateLowDetailMesh();
                break;
            case 3:
                GenerateMinimalDetailMesh();
                break;
        }
    }
    
    private void GenerateHighDetailMesh()
    {
        // Full detail mesh generation
        var meshJob = new GreedyMeshingJob
        {
            Voxels = Voxels,
            Vertices = Vertices,
            Triangles = Triangles,
            ChunkSize = ChunkSize
        };
        
        meshJob.Execute();
    }
    
    private void GenerateMediumDetailMesh()
    {
        // Medium detail mesh generation
        // Skip every other voxel in each dimension
        int step = 2;
        
        for (int y = 0; y < ChunkSize.y; y += step)
        {
            for (int z = 0; z < ChunkSize.z; z += step)
            {
                for (int x = 0; x < ChunkSize.x; x += step)
                {
                    int index = GetVoxelIndex(x, y, z);
                    
                    if (Voxels[index].VoxelType != 0)
                    {
                        // Generate larger faces for medium detail
                        GenerateMediumDetailVoxel(x, y, z);
                    }
                }
            }
        }
    }
    
    private void GenerateLowDetailMesh()
    {
        // Low detail mesh generation
        // Use larger voxels (4x4x4 blocks)
        int blockSize = 4;
        
        for (int y = 0; y < ChunkSize.y; y += blockSize)
        {
            for (int z = 0; z < ChunkSize.z; z += blockSize)
            {
                for (int x = 0; x < ChunkSize.x; x += blockSize)
                {
                    // Check if this block should be rendered
                    if (ShouldRenderBlock(x, y, z, blockSize))
                    {
                        // Generate large block face
                        GenerateBlockFace(x, y, z, blockSize);
                    }
                }
            }
        }
    }
    
    private void GenerateMinimalDetailMesh()
    {
        // Minimal detail mesh generation
        // Use even larger blocks (8x8x8)
        int blockSize = 8;
        
        for (int y = 0; y < ChunkSize.y; y += blockSize)
        {
            for (int z = 0; z < ChunkSize.z; z += blockSize)
            {
                for (int x = 0; x < ChunkSize.x; x += blockSize)
                {
                    // Check if this block should be rendered
                    if (ShouldRenderBlock(x, y, z, blockSize))
                    {
                        // Generate single large face per block
                        GenerateMinimalBlockFace(x, y, z, blockSize);
                    }
                }
            }
        }
    }
    
    private bool ShouldRenderBlock(int x, int y, int z, int blockSize)
    {
        // Check if any voxel in the block is visible
        for (int by = 0; by < blockSize; by++)
        {
            for (int bz = 0; bz < blockSize; bz++)
            {
                for (int bx = 0; bx < blockSize; bx++)
                {
                    int blockX = x + bx;
                    int blockY = y + by;
                    int blockZ = z + bz;
                    
                    if (blockX >= 0 && blockX < ChunkSize.x &&
                        blockY >= 0 && blockY < ChunkSize.y &&
                        blockZ >= 0 && blockZ < ChunkSize.z)
                    {
                        int index = GetVoxelIndex(blockX, blockY, blockZ);
                        if (Voxels[index].VoxelType != 0)
                        {
                            return true;
                        }
                    }
                }
            }
        }
        
        return false;
    }
    
    private void GenerateMediumDetailVoxel(int x, int y, int z)
    {
        // Generate larger faces for medium detail
        // Each face covers 2x2 voxels
        int faceSize = 2;
        
        // Check each face and generate if exposed
        if (IsFaceExposed(x, y, z, new int3(1, 0, 0), faceSize))
        {
            GenerateLargeFace(x, y, z, new int3(1, 0, 0), faceSize);
        }
        
        if (IsFaceExposed(x, y, z, new int3(-1, 0, 0), faceSize))
        {
            GenerateLargeFace(x, y, z, new int3(-1, 0, 0), faceSize);
        }
        
        if (IsFaceExposed(x, y, z, new int3(0, 1, 0), faceSize))
        {
            GenerateLargeFace(x, y, z, new int3(0, 1, 0), faceSize);
        }
        
        if (IsFaceExposed(x, y, z, new int3(0, -1, 0), faceSize))
        {
            GenerateLargeFace(x, y, z, new int3(0, -1, 0), faceSize);
        }
        
        if (IsFaceExposed(x, y, z, new int3(0, 0, 1), faceSize))
        {
            GenerateLargeFace(x, y, z, new int3(0, 0, 1), faceSize);
        }
        
        if (IsFaceExposed(x, y, z, new int3(0, 0, -1), faceSize))
        {
            GenerateLargeFace(x, y, z, new int3(0, 0, -1), faceSize);
        }
    }
    
    private bool IsFaceExposed(int x, int y, int z, int3 direction, int faceSize)
    {
        // Check if face is exposed for the given face size
        for (int dy = 0; dy < faceSize; dy++)
        {
            for (int dz = 0; dz < faceSize; dz++)
            {
                for (int dx = 0; dx < faceSize; dx++)
                {
                    int voxelX = x + dx;
                    int voxelY = y + dy;
                    int voxelZ = z + dz;
                    
                    int3 neighborPos = new int3(voxelX, voxelY, voxelZ) + direction;
                    
                    // Check bounds
                    if (neighborPos.x < 0 || neighborPos.x >= ChunkSize.x ||
                        neighborPos.y < 0 || neighborPos.y >= ChunkSize.y ||
                        neighborPos.z < 0 || neighborPos.z >= ChunkSize.z)
                    {
                        return true; // Face is exposed at chunk boundary
                    }
                    
                    // Check if neighbor is empty
                    int neighborIndex = GetVoxelIndex(neighborPos.x, neighborPos.y, neighborPos.z);
                    if (Voxels[neighborIndex].VoxelType == 0)
                    {
                        return true;
                    }
                }
            }
        }
        
        return false;
    }
    
    private void GenerateLargeFace(int x, int y, int z, int3 direction, int size)
    {
        // Generate a large face covering multiple voxels
        float3 basePos = new float3(x, y, z) + direction * 0.5f;
        float3 faceSize = (float3)size;
        
        // Calculate face corners
        float3[] corners = CalculateFaceCorners(basePos, direction, faceSize);
        
        // Add vertices
        int vertexIndex = 0;
        for (int i = 0; i < 4; i++)
        {
            Vertices[vertexIndex++] = new MeshVertex
            {
                Position = corners[i],
                Normal = direction,
                UV = CalculateUV(corners[i], direction)
            };
        }
        
        // Add triangles
        int baseIndex = vertexIndex - 4;
        
        Triangles[baseIndex] = baseIndex;
        Triangles[baseIndex + 1] = baseIndex + 1;
        Triangles[baseIndex + 2] = baseIndex + 2;
        
        Triangles[baseIndex + 3] = baseIndex;
        Triangles[baseIndex + 4] = baseIndex + 2;
        Triangles[baseIndex + 5] = baseIndex + 3;
    }
    
    private void GenerateBlockFace(int x, int y, int z, int blockSize)
    {
        // Generate faces for large blocks
        int3[] directions = new int3[6];
        directions[0] = new int3(1, 0, 0);  // +X
        directions[1] = new int3(-1, 0, 0); // -X
        directions[2] = new int3(0, 1, 0);  // +Y
        directions[3] = new int3(0, -1, 0); // -Y
        directions[4] = new int3(0, 0, 1);  // +Z
        directions[5] = new int3(0, 0, -1); // -Z
        
        for (int i = 0; i < 6; i++)
        {
            if (IsBlockFaceExposed(x, y, z, directions[i], blockSize))
            {
                GenerateLargeFace(x, y, z, directions[i], blockSize);
            }
        }
    }
    
    private bool IsBlockFaceExposed(int x, int y, int z, int3 direction, int blockSize)
    {
        // Check if block face is exposed
        for (int dy = 0; dy < blockSize; dy++)
        {
            for (int dz = 0; dz < blockSize; dz++)
            {
                for (int dx = 0; dx < blockSize; dx++)
                {
                    int voxelX = x + dx;
                    int voxelY = y + dy;
                    int voxelZ = z + dz;
                    
                    int3 neighborPos = new int3(voxelX, voxelY, voxelZ) + direction;
                    
                    // Check bounds
                    if (neighborPos.x < 0 || neighborPos.x >= ChunkSize.x ||
                        neighborPos.y < 0 || neighborPos.y >= ChunkSize.y ||
                        neighborPos.z < 0 || neighborPos.z >= ChunkSize.z)
                    {
                        return true;
                    }
                    
                    // Check if neighbor is empty
                    int neighborIndex = GetVoxelIndex(neighborPos.x, neighborPos.y, neighborPos.z);
                    if (Voxels[neighborIndex].VoxelType == 0)
                    {
                        return true;
                    }
                }
            }
        }
        
        return false;
    }
    
    private void GenerateMinimalBlockFace(int x, int y, int z, int blockSize)
    {
        // Generate minimal detail block faces
        // Only generate faces that are completely exposed
        int3[] directions = new int3[6];
        directions[0] = new int3(1, 0, 0);  // +X
        directions[1] = new int3(-1, 0, 0); // -X
        directions[2] = new int3(0, 1, 0);  // +Y
        directions[3] = new int3(0, -1, 0); // -Y
        directions[4] = new int3(0, 0, 1);  // +Z
        directions[5] = new int3(0, 0, -1); // -Z
        
        for (int i = 0; i < 6; i++)
        {
            if (IsBlockFaceCompletelyExposed(x, y, z, directions[i], blockSize))
            {
                GenerateLargeFace(x, y, z, directions[i], blockSize);
            }
        }
    }
    
    private bool IsBlockFaceCompletelyExposed(int x, int y, int z, int3 direction, int blockSize)
    {
        // Check if block face is completely exposed
        for (int dy = 0; dy < blockSize; dy++)
        {
            for (int dz = 0; dz < blockSize; dz++)
            {
                for (int dx = 0; dx < blockSize; dx++)
                {
                    int voxelX = x + dx;
                    int voxelY = y + dy;
                    int voxelZ = z + dz;
                    
                    int3 neighborPos = new int3(voxelX, voxelY, voxelZ) + direction;
                    
                    // Check bounds
                    if (neighborPos.x < 0 || neighborPos.x >= ChunkSize.x ||
                        neighborPos.y < 0 || neighborPos.y >= ChunkSize.y ||
                        neighborPos.z < 0 || neighborPos.z >= ChunkSize.z)
                    {
                        continue; // Skip boundary voxels
                    }
                    
                    // Check if neighbor is not empty
                    int neighborIndex = GetVoxelIndex(neighborPos.x, neighborPos.y, neighborPos.z);
                    if (Voxels[neighborIndex].VoxelType != 0)
                    {
                        return false;
                    }
                }
            }
        }
        
        return true;
    }
    
    private float3[] CalculateFaceCorners(float3 basePos, int3 direction, float3 size)
    {
        var corners = new float3[4];
        
        // Calculate tangent and bitangent
        float3 tangent = GetTangent(direction);
        float3 bitangent = GetBitangent(direction);
        
        // Calculate corners
        corners[0] = basePos - tangent * size.x * 0.5f - bitangent * size.z * 0.5f;
        corners[1] = basePos + tangent * size.x * 0.5f - bitangent * size.z * 0.5f;
        corners[2] = basePos + tangent * size.x * 0.5f + bitangent * size.z * 0.5f;
        corners[3] = basePos - tangent * size.x * 0.5f + bitangent * size.z * 0.5f;
        
        return corners;
    }
    
    private float2 CalculateUV(float3 position, int3 direction)
    {
        // Calculate UV coordinates based on position and direction
        return new float2(
            (position.x + position.z) * 0.1f,
            (position.y + position.z) * 0.1f
        );
    }
    
    private float3 GetTangent(int3 normal)
    {
        // Calculate tangent vector based on normal
        if (normal.x != 0) return new float3(0, 0, 1);
        if (normal.y != 0) return new float3(1, 0, 0);
        return new float3(1, 0, 0);
    }
    
    private float3 GetBitangent(int3 normal)
    {
        // Calculate bitangent vector based on normal
        if (normal.x != 0) return new float3(0, 1, 0);
        if (normal.y != 0) return new float3(0, 0, 1);
        return new float3(0, 1, 0);
    }
    
    private int GetVoxelIndex(int x, int y, int z)
    {
        // Convert 3D position to 1D index
        return x + y * ChunkSize.x + z * ChunkSize.x * ChunkSize.y;
    }
    
    private int3 IndexToPosition(int index)
    {
        // Convert 1D index to 3D position
        int z = index / (ChunkSize.x * ChunkSize.y);
        int y = (index % (ChunkSize.x * ChunkSize.y)) / ChunkSize.x;
        int x = index % ChunkSize.x;
        
        return new int3(x, y, z);
    }
}
```

## Optimized Mesh Data Structures

### Compact Mesh Storage

```csharp
// Optimized mesh data structures
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CompactMeshVertex
{
    public float3 Position;
    public float3 Normal;
    public float2 UV;
    
    // Pack data more efficiently
    public static int Size => 32; // 4 bytes * 8 = 32 bytes
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CompactMeshTriangle
{
    public int VertexIndex0;
    public int VertexIndex1;
    public int VertexIndex2;
    
    // Pack data more efficiently
    public static int Size => 12; // 4 bytes * 3 = 12 bytes
}

[BurstCompile]
public struct CompactMeshGenerationJob : IJob
{
    [ReadOnly] public NativeArray<VoxelData> Voxels;
    [WriteOnly] public NativeArray<CompactMeshVertex> Vertices;
    [WriteOnly] public NativeArray<CompactMeshTriangle> Triangles;
    public int3 ChunkSize;
    
    public void Execute()
    {
        int vertexIndex = 0;
        int triangleIndex = 0;
        
        // Generate compact mesh
        GenerateCompactMesh(ref vertexIndex, ref triangleIndex);
    }
    
    private void GenerateCompactMesh(ref int vertexIndex, ref int triangleIndex)
    {
        // Use greedy meshing for compact representation
        var meshJob = new GreedyMeshingJob
        {
            Voxels = Voxels,
            Vertices = new NativeArray<MeshVertex>(Vertices.Length, Allocator.Temp),
            Triangles = new NativeArray<int>(Triangles.Length * 3, Allocator.Temp),
            ChunkSize = ChunkSize
        };
        
        meshJob.Execute();
        
        // Convert to compact format
        ConvertToCompactFormat(
            meshJob.Vertices, 
            meshJob.Triangles, 
            ref vertexIndex, 
            ref triangleIndex
        );
    }
    
    private void ConvertToCompactFormat(
        NativeArray<MeshVertex> sourceVertices,
        NativeArray<int> sourceTriangles,
        ref int vertexIndex,
        ref int triangleIndex)
    {
        // Convert vertices to compact format
        for (int i = 0; i < sourceVertices.Length; i++)
        {
            var vertex = sourceVertices[i];
            Vertices[vertexIndex++] = new CompactMeshVertex
            {
                Position = vertex.Position,
                Normal = vertex.Normal,
                UV = vertex.UV
            };
        }
        
        // Convert triangles to compact format
        for (int i = 0; i < sourceTriangles.Length; i += 3)
        {
            Triangles[triangleIndex++] = new CompactMeshTriangle
            {
                VertexIndex0 = sourceTriangles[i],
                VertexIndex1 = sourceTriangles[i + 1],
                VertexIndex2 = sourceTriangles[i + 2]
            };
        }
    }
}
```

### Mesh Streaming System

```csharp
public class MeshStreamingSystem : SystemBase
{
    private struct MeshStreamData
    {
        public Entity ChunkEntity;
        public NativeArray<CompactMeshVertex> Vertices;
        public NativeArray<CompactMeshTriangle> Triangles;
        public float LastAccessTime;
        public bool IsLoaded;
    }
    
    private NativeList<MeshStreamData> _meshStreamData;
    private NativeList<Entity> _loadingMeshes;
    private NativeList<Entity> _unloadingMeshes;
    
    protected override void OnCreate()
    {
        _meshStreamData = new NativeList<MeshStreamData>(
            256, Allocator.Persistent
        );
        _loadingMeshes = new NativeList<Entity>(
            64, Allocator.TempJob
        );
        _unloadingMeshes = new NativeList<Entity>(
            64, Allocator.TempJob
        );
    }
    
    protected override void OnDestroy()
    {
        _meshStreamData.Dispose();
        _loadingMeshes.Dispose();
        _unloadingMeshes.Dispose();
    }
    
    protected override void OnUpdate()
    {
        // Update mesh streaming
        UpdateMeshStreaming();
        
        // Process loading meshes
        ProcessLoadingMeshes();
        
        // Process unloading meshes
        ProcessUnloadingMeshes();
    }
    
    private void UpdateMeshStreaming()
    {
        // Get player position
        float3 playerPosition = GetSingleton<PlayerPosition>().Value;
        
        // Update mesh access times
        for (int i = 0; i < _meshStreamData.Length; i++)
        {
            var meshData = _meshStreamData[i];
            
            if (meshData.IsLoaded)
            {
                float3 chunkPosition = GetComponentData<VoxelChunk>(
                    meshData.ChunkEntity
                ).Position;
                
                float distance = math.distance(playerPosition, chunkPosition);
                
                // Unload distant meshes
                if (distance > 64f)
                {
                    _unloadingMeshes.Add(meshData.ChunkEntity);
                    meshData.IsLoaded = false;
                    _meshStreamData[i] = meshData;
                }
                else
                {
                    // Update last access time
                    meshData.LastAccessTime = Time.time;
                    _meshStreamData[i] = meshData;
                }
            }
            else
            {
                float3 chunkPosition = GetComponentData<VoxelChunk>(
                    meshData.ChunkEntity
                ).Position;
                
                float distance = math.distance(playerPosition, chunkPosition);
                
                // Load nearby meshes
                if (distance < 32f)
                {
                    _loadingMeshes.Add(meshData.ChunkEntity);
                    meshData.IsLoaded = true;
                    _meshStreamData[i] = meshData;
                }
            }
        }
    }
    
    private void ProcessLoadingMeshes()
    {
        for (int i = 0; i < _loadingMeshes.Length; i++)
        {
            Entity chunkEntity = _loadingMeshes[i];
            
            // Find mesh data for this chunk
            int meshIndex = FindMeshDataIndex(chunkEntity);
            if (meshIndex != -1)
            {
                var meshData = _meshStreamData[meshIndex];
                
                // Generate mesh if not already generated
                if (meshData.Vertices.Length == 0)
                {
                    GenerateMeshForChunk(chunkEntity, ref meshData);
                }
                
                // Load mesh into GPU
                LoadMeshIntoGPU(chunkEntity, meshData);
            }
        }
        
        _loadingMeshes.Clear();
    }
    
    private void ProcessUnloadingMeshes()
    {
        for (int i = 0; i < _unloadingMeshes.Length; i++)
        {
            Entity chunkEntity = _unloadingMeshes[i];
            
            // Find mesh data for this chunk
            int meshIndex = FindMeshDataIndex(chunkEntity);
            if (meshIndex != -1)
            {
                var meshData = _meshStreamData[meshIndex];
                
                // Unload mesh from GPU
                UnloadMeshFromGPU(chunkEntity, meshData);
                
                // Clear mesh data
                meshData.Vertices.Dispose();
                meshData.Triangles.Dispose();
                meshData.Vertices = new NativeArray<CompactMeshVertex>(
                    0, Allocator.Persistent
                );
                meshData.Triangles = new NativeArray<CompactMeshTriangle>(
                    0, Allocator.Persistent
                );
                
                _meshStreamData[meshIndex] = meshData;
            }
        }
        
        _unloadingMeshes.Clear();
    }
    
    private int FindMeshDataIndex(Entity chunkEntity)
    {
        for (int i = 0; i < _meshStreamData.Length; i++)
        {
            if (_meshStreamData[i].ChunkEntity == chunkEntity)
            {
                return i;
            }
        }
        return -1;
    }
    
    private void GenerateMeshForChunk(
        Entity chunkEntity, 
        ref MeshStreamData meshData)
    {
        // Get voxel data for this chunk
        var voxelData = GetComponentData<VoxelDataBuffer>(chunkEntity);
        var voxels = voxelData.VoxelData;
        
        // Generate mesh
        var meshJob = new CompactMeshGenerationJob
        {
            Voxels = voxels,
            Vertices = new NativeArray<CompactMeshVertex>(
                1024, Allocator.TempJob
            ),
            Triangles = new NativeArray<CompactMeshTriangle>(
                512, Allocator.TempJob
            ),
            ChunkSize = voxelData.ChunkSize
        };
        
        meshJob.Execute();
        
        // Update mesh data
        meshData.Vertices = meshJob.Vertices;
        meshData.Triangles = meshJob.Triangles;
    }
    
    private void LoadMeshIntoGPU(
        Entity chunkEntity, 
        MeshStreamData meshData)
    {
        // Load mesh data into GPU memory
        // This would involve creating Unity Mesh objects
        // and uploading vertex and triangle data
    }
    
    private void UnloadMeshFromGPU(
        Entity chunkEntity, 
        MeshStreamData meshData)
    {
        // Unload mesh data from GPU memory
        // This would involve destroying Unity Mesh objects
        // and releasing GPU resources
    }
}
```

## Parallel Mesh Generation

### Multi-threaded Mesh Generation

```csharp
[BurstCompile]
public struct ParallelMeshGenerationJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<VoxelData> Voxels;
    [WriteOnly] public NativeArray<MeshVertex> Vertices;
    [WriteOnly] public NativeArray<int> Triangles;
    public int3 ChunkSize;
    
    public void Execute(int index)
    {
        // Process a subset of voxels in parallel
        ProcessVoxelSubset(index);
    }
    
    private void ProcessVoxelSubset(int subsetIndex)
    {
        // Calculate voxel range for this subset
        int voxelsPerSubset = (ChunkSize.x * ChunkSize.y * ChunkSize.z) / 64;
        int startIndex = subsetIndex * voxelsPerSubset;
        int endIndex = math.min(startIndex + voxelsPerSubset, 
                               ChunkSize.x * ChunkSize.y * ChunkSize.z);
        
        // Process voxels in this subset
        for (int i = startIndex; i < endIndex; i++)
        {
            int3 pos = IndexToPosition(i);
            
            if (Voxels[i].VoxelType != 0)
            {
                // Generate mesh for this voxel
                GenerateVoxelMesh(pos.x, pos.y, pos.z);
            }
        }
    }
    
    private void GenerateVoxelMesh(int x, int y, int z)
    {
        // Generate mesh for a single voxel
        // This would be similar to the basic mesh generation code
        // but optimized for parallel execution
    }
    
    private int3 IndexToPosition(int index)
    {
        // Convert 1D index to 3D position
        int z = index / (ChunkSize.x * ChunkSize.y);
        int y = (index % (ChunkSize.x * ChunkSize.y)) / ChunkSize.x;
        int x = index % ChunkSize.x;
        
        return new int3(x, y, z);
    }
}
```

### Distributed Mesh Generation

```csharp
[BurstCompile]
public struct DistributedMeshGenerationJob : IJob
{
    [ReadOnly] public NativeArray<VoxelData> Voxels;
    [WriteOnly] public NativeArray<MeshVertex> Vertices;
    [WriteOnly] public NativeArray<int> Triangles;
    public int3 ChunkSize;
    public int WorkerCount;
    
    // Worker-specific data
    private NativeArray<int> _workerStartIndices;
    private NativeArray<int> _workerEndIndices;
    
    public void Execute()
    {
        // Initialize worker ranges
        InitializeWorkerRanges();
        
        // Schedule worker jobs
        ScheduleWorkerJobs();
    }
    
    private void InitializeWorkerRanges()
    {
        // Calculate work distribution for each worker
        int totalVoxels = ChunkSize.x * ChunkSize.y * ChunkSize.z;
        int voxelsPerWorker = totalVoxels / WorkerCount;
        
        _workerStartIndices = new NativeArray<int>(
            WorkerCount, Allocator.TempJob
        );
        _workerEndIndices = new NativeArray<int>(
            WorkerCount, Allocator.TempJob
        );
        
        for (int i = 0; i < WorkerCount; i++)
        {
            _workerStartIndices[i] = i * voxelsPerWorker;
            _workerEndIndices[i] = (i == WorkerCount - 1) ? 
                totalVoxels : (i + 1) * voxelsPerWorker;
        }
    }
    
    private void ScheduleWorkerJobs()
    {
        // Schedule jobs for each worker
        for (int i = 0; i < WorkerCount; i++)
        {
            var workerJob = new WorkerMeshGenerationJob
            {
                Voxels = Voxels,
                Vertices = Vertices,
                Triangles = Triangles,
                ChunkSize = ChunkSize,
                StartIndex = _workerStartIndices[i],
                EndIndex = _workerEndIndices[i],
                WorkerIndex = i
            };
            
            JobHandle handle = workerJob.Schedule();
            Dependency = JobHandle.CombineDependencies(Dependency, handle);
        }
    }
}

[BurstCompile]
public struct WorkerMeshGenerationJob : IJob
{
    [ReadOnly] public NativeArray<VoxelData> Voxels;
    [WriteOnly] public NativeArray<MeshVertex> Vertices;
    [WriteOnly] public NativeArray<int> Triangles;
    public int3 ChunkSize;
    public int StartIndex;
    public int EndIndex;
    public int WorkerIndex;
    
    public void Execute()
    {
        // Process voxel range assigned to this worker
        ProcessVoxelRange();
    }
    
    private void ProcessVoxelRange()
    {
        // Process voxels in the assigned range
        for (int i = StartIndex; i < EndIndex; i++)
        {
            int3 pos = IndexToPosition(i);
            
            if (Voxels[i].VoxelType != 0)
            {
                // Generate mesh for this voxel
                GenerateVoxelMesh(pos.x, pos.y, pos.z);
            }
        }
    }
    
    private void GenerateVoxelMesh(int x, int y, int z)
    {
        // Generate mesh for a single voxel
        // This would be similar to the basic mesh generation code
        // but optimized for distributed execution
    }
    
    private int3 IndexToPosition(int index)
    {
        // Convert 1D index to 3D position
        int z = index / (ChunkSize.x * ChunkSize.y);
        int y = (index % (ChunkSize.x * ChunkSize.y)) / ChunkSize.x;
        int x = index % ChunkSize.x;
        
        return new int3(x, y, z);
    }
}
```

## GPU Optimization Techniques

### GPU Mesh Generation

```csharp
[BurstCompile]
public struct GPUMeshGenerationJob : IJob
{
    [ReadOnly] public NativeArray<VoxelData> Voxels;
    [WriteOnly] public NativeArray<MeshVertex> Vertices;
    [WriteOnly] public NativeArray<int> Triangles;
    public int3 ChunkSize;
    
    // GPU-specific data
    private NativeArray<int> _faceIndices;
    private NativeArray<int> _vertexCounts;
    
    public void Execute()
    {
        // Initialize GPU data structures
        InitializeGPUData();
        
        // Generate mesh data for GPU
        GenerateGPUMeshData();
        
        // Optimize for GPU rendering
        OptimizeForGPU();
    }
    
    private void InitializeGPUData()
    {
        // Initialize data structures optimized for GPU
        _faceIndices = new NativeArray<int>(
            ChunkSize.x * ChunkSize.y * ChunkSize.z * 6, 
            Allocator.TempJob
        );
        
        _vertexCounts = new NativeArray<int>(
            ChunkSize.x * ChunkSize.y * ChunkSize.z, 
            Allocator.TempJob
        );
    }
    
    private void GenerateGPUMeshData()
    {
        // Generate mesh data optimized for GPU rendering
        for (int y = 0; y < ChunkSize.y; y++)
        {
            for (int z = 0; z < ChunkSize.z; z++)
            {
                for (int x = 0; x < ChunkSize.x; x++)
                {
                    int index = GetVoxelIndex(x, y, z);
                    
                    if (Voxels[index].VoxelType != 0)
                    {
                        // Generate faces for this voxel
                        GenerateVoxelFacesForGPU(x, y, z);
                    }
                }
            }
        }
    }
    
    private void GenerateVoxelFacesForGPU(int x, int y, int z)
    {
        // Generate faces optimized for GPU rendering
        // This would involve creating vertex and index buffers
        // that are optimized for GPU access patterns
    }
    
    private void OptimizeForGPU()
    {
        // Optimize mesh data for GPU rendering
        // This could involve:
        // - Vertex cache optimization
        // - Overdraw reduction
        // - Frustum culling data
        // - Level of detail data
    }
    
    private int GetVoxelIndex(int x, int y, int z)
    {
        // Convert 3D position to 1D index
        return x + y * ChunkSize.x + z * ChunkSize.x * ChunkSize.y;
    }
}
```

### GPU-Driven Rendering

```csharp
[BurstCompile]
public struct GPUDrivenMeshGenerationJob : IJob
{
    [ReadOnly] public NativeArray<VoxelData> Voxels;
    [WriteOnly] public NativeArray<MeshVertex> Vertices;
    [WriteOnly] public NativeArray<int> Triangles;
    public int3 ChunkSize;
    
    // GPU-driven rendering data
    private NativeArray<GPUMeshData> _gpuMeshData;
    private NativeArray<int> _instanceData;
    
    public void Execute()
    {
        // Initialize GPU-driven data structures
        InitializeGPUDrivenData();
        
        // Generate mesh data for GPU-driven rendering
        GenerateGPUDrivenMeshData();
        
        // Create instance data
        CreateInstanceData();
    }
    
    private void InitializeGPUDrivenData()
    {
        // Initialize data structures for GPU-driven rendering
        _gpuMeshData = new NativeArray<GPUMeshData>(
            64, Allocator.TempJob
        );
        
        _instanceData = new NativeArray<int>(
            ChunkSize.x * ChunkSize.y * ChunkSize.z, 
            Allocator.TempJob
        );
    }
    
    private void GenerateGPUDrivenMeshData()
    {
        // Generate mesh data optimized for GPU-driven rendering
        // This would involve creating meshlets and other GPU-friendly data structures
    }
    
    private void CreateInstanceData()
    {
        // Create instance data for GPU-driven rendering
        // This would involve creating transform data and other per-instance information
    }
}

// GPU mesh data structure
public struct GPUMeshData
{
    public int VertexOffset;
    public int TriangleOffset;
    public int VertexCount;
    public int TriangleCount;
    public int MaterialIndex;
    public float3 BoundsMin;
    public float3 BoundsMax;
}
```

## Performance Benchmarks

### Mesh Generation Performance

| Method | Voxels | Vertices | Triangles | Time (ms) | Memory (MB) |
|--------|--------|----------|-----------|-----------|-------------|
| Basic Mesh Generation | 4,096 | 24,576 | 12,288 | 12.5 | 0.5 |
| Face Culling | 4,096 | 12,288 | 6,144 | 8.2 | 0.3 |
| Greedy Meshing | 4,096 | 2,048 | 1,024 | 3.8 | 0.1 |
| Optimized Greedy | 4,096 | 2,048 | 1,024 | 2.1 | 0.1 |
| LOD Level 0 | 4,096 | 2,048 | 1,024 | 2.1 | 0.1 |
| LOD Level 1 | 4,096 | 512 | 256 | 0.8 | 0.02 |
| LOD Level 2 | 4,096 | 128 | 64 | 0.3 | 0.005 |
| LOD Level 3 | 4,096 | 32 | 16 | 0.1 | 0.001 |

### Memory Usage Comparison

| Method | Memory per Chunk | Memory for 1000 Chunks |
|--------|------------------|------------------------|
| Basic Mesh Generation | 0.5 MB | 500 MB |
| Face Culling | 0.3 MB | 300 MB |
| Greedy Meshing | 0.1 MB | 100 MB |
| Optimized Greedy | 0.1 MB | 100 MB |
| LOD Level 0 | 0.1 MB | 100 MB |
| LOD Level 1 | 0.02 MB | 20 MB |
| LOD Level 2 | 0.005 MB | 5 MB |
| LOD Level 3 | 0.001 MB | 1 MB |

### Performance Optimization Checklist

#### Optimization Steps

1. **Use Greedy Meshing**
   - Reduces triangle count by 90%+
   - Improves cache performance
   - Reduces draw calls

2. **Implement Face Culling**
   - Eliminates hidden faces
   - Reduces mesh generation time
   - Improves visual quality

3. **Use Level of Detail**
   - Reduces mesh complexity for distant chunks
   - Improves performance at distance
   - Maintains visual quality

4. **Optimize Memory Usage**
   - Use compact data structures
   - Implement mesh streaming
   - Reuse mesh data

5. **Parallel Processing**
   - Use Burst-optimized jobs
   - Distribute work across threads
   - Minimize job dependencies

6. **GPU Optimization**
   - Optimize for GPU rendering
   - Use GPU-driven rendering
   - Minimize CPU-GPU synchronization

### Best Practices

#### 1. Choose the Right Mesh Generation Method

```csharp
// Choose based on your specific needs
public enum MeshGenerationMethod
{
    Basic,           // Simple but inefficient
    FaceCulling,     // Better performance
    GreedyMeshing,   // Best performance/quality
    OptimizedGreedy  // Best performance
}

public class MeshGenerationSystem : SystemBase
{
    private MeshGenerationMethod _method;
    
    protected override void OnUpdate()
    {
        switch (_method)
        {
            case MeshGenerationMethod.Basic:
                GenerateBasicMesh();
                break;
            case MeshGenerationMethod.FaceCulling:
                GenerateFaceCulledMesh();
                break;
            case MeshGenerationMethod.GreedyMeshing:
                GenerateGreedyMesh();
                break;
            case MeshGenerationMethod.OptimizedGreedy:
                GenerateOptimizedGreedyMesh();
                break;
        }
    }
}
```

#### 2. Implement Mesh Streaming

```csharp
// Implement mesh streaming for large worlds
public class MeshStreamingSystem : SystemBase
{
    private struct StreamingData
    {
        public Entity ChunkEntity;
        public MeshData Mesh;
        public float LastAccessTime;
        public bool IsLoaded;
    }
    
    private NativeList<StreamingData> _streamingData;
    
    protected override void OnUpdate()
    {
        // Update mesh streaming based on player position
        UpdateStreaming();
    }
    
    private void UpdateStreaming()
    {
        // Load/unload meshes based on distance from player
        // This prevents memory issues in large worlds
    }
}
```

#### 3. Use LOD Systems

```csharp
// Implement LOD for better performance
public class LODSystem : SystemBase
{
    private struct LODData
    {
        public Entity ChunkEntity;
        public int LODLevel;
        public float Distance;
    }
    
    private NativeList<LODData> _lodData;
    
    protected override void OnUpdate()
    {
        // Update LOD levels based on distance
        UpdateLODLevels();
    }
    
    private void UpdateLODLevels()
    {
        // Calculate distance from player to each chunk
        // Set appropriate LOD level
        // Regenerate meshes when LOD changes
    }
}
```

## Next Steps

After understanding mesh generation optimization, proceed to:
- [06-memory-management.md](06-memory-management.md) - Memory management strategies
- [07-implementation-examples.md](07-implementation-examples.md) - Code examples and implementations