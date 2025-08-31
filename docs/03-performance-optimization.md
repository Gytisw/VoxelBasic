# Performance Optimization Techniques

This document covers performance optimization techniques specifically for voxel engines built with Unity DOTS.

## Table of Contents

- [CPU Optimization Strategies](#cpu-optimization-strategies)
- [Memory Management Optimization](#memory-management-optimization)
- [Job System Best Practices](#job-system-best-practices)
- [Parallel Processing Patterns](#parallel-processing-patterns)
- [Cache Optimization](#cache-optimization)
- [Memory Pooling](#memory-pooling)
- [Profiling and Analysis](#profiling-and-analysis)
- [Performance Metrics](#performance-metrics)

## CPU Optimization Strategies

### Job Scheduling and Parallelization

#### Efficient Job Design

```csharp
[BurstCompile]
public struct VoxelProcessingJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<VoxelData> InputVoxels;
    [WriteOnly] public NativeArray<VoxelData> OutputVoxels;
    public float3 PlayerPosition;
    public float Time;
    
    public void Execute(int index)
    {
        var voxel = InputVoxels[index];
        
        // Process voxel based on distance to player
        float3 voxelPos = IndexToWorldPosition(index);
        float distance = math.distance(voxelPos, PlayerPosition);
        
        if (distance < 32f)
        {
            // High priority processing for nearby voxels
            OutputVoxels[index] = ProcessHighPriority(voxel, Time);
        }
        else
        {
            // Low priority processing for distant voxels
            OutputVoxels[index] = ProcessLowPriority(voxel, Time);
        }
    }
    
    private VoxelData ProcessHighPriority(VoxelData voxel, float time)
    {
        // Intensive processing for nearby voxels
        voxel.VoxelState = (byte)VoxelState.Active;
        voxel.VoxelMetadata = (byte)(time * 100f);
        return voxel;
    }
    
    private VoxelData ProcessLowPriority(VoxelData voxel, float time)
    {
        // Minimal processing for distant voxels
        if (voxel.VoxelType == (byte)VoxelType.Grass)
        {
            voxel.VoxelState = (byte)VoxelState.Dormant;
        }
        return voxel;
    }
}
```

#### Job Batching and Dependencies

```csharp
public class ChunkProcessingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Schedule multiple jobs with proper dependencies
        var generationJob = new GenerateChunkJob
        {
            ChunkData = GetComponentDataFromEntity<VoxelChunk>(),
            VoxelData = GetComponentDataFromEntity<VoxelData>()
        };
        
        var meshJob = new GenerateMeshJob
        {
            ChunkData = GetComponentDataFromEntity<VoxelChunk>(),
            MeshData = GetComponentDataFromEntity<MeshData>()
        };
        
        var physicsJob = new UpdatePhysicsJob
        {
            VoxelData = GetComponentDataFromEntity<VoxelData>(),
            PhysicsData = GetComponentDataFromEntity<PhysicsData>()
        };
        
        // Schedule jobs with dependencies
        JobHandle generationHandle = generationJob.ScheduleParallel(
            _chunkQuery.CalculateEntityCount(), 64
        );
        
        JobHandle meshHandle = meshJob.Schedule(
            generationHandle, 64
        );
        
        JobHandle physicsHandle = physicsJob.Schedule(
            meshHandle, 64
        );
        
        // Combine all dependencies
        Dependency = JobHandle.CombineDependencies(
            Dependency, physicsHandle
        );
    }
}
```

### Burst Compiler Optimization

#### Burst-Optimized Voxel Processing

```csharp
[BurstCompile]
public struct FastVoxelProcessingJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<byte> VoxelTypes;
    [WriteOnly] public NativeArray<byte> VoxelStates;
    
    public void Execute(int index)
    {
        byte voxelType = VoxelTypes[index];
        
        // Use Burst-optimized branching
        if (voxelType == (byte)VoxelType.Grass)
        {
            VoxelStates[index] = (byte)VoxelState.Growing;
        }
        else if (voxelType == (byte)VoxelType.Stone)
        {
            VoxelStates[index] = (byte)VoxelState.Solid;
        }
        else if (voxelType == (byte)VoxelType.Sand)
        {
            VoxelStates[index] = (byte)VoxelState.Falling;
        }
        else
        {
            VoxelStates[index] = (byte)VoxelState.Empty;
        }
    }
}
```

#### Vectorization Techniques

```csharp
[BurstCompile]
public struct VectorizedVoxelJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<VoxelData> Input;
    [WriteOnly] public NativeArray<VoxelData> Output;
    
    public void Execute(int index)
    {
        // Process 4 voxels at once using SIMD
        int4 indices = int4(index, index + 1, index + 2, index + 3);
        
        // Load 4 voxels simultaneously
        var voxels = new VoxelData4
        {
            VoxelType = new uint4(
                Input[indices.x].VoxelType,
                Input[indices.y].VoxelType,
                Input[indices.z].VoxelType,
                Input[indices.w].VoxelType
            ),
            VoxelState = new uint4(
                Input[indices.x].VoxelState,
                Input[indices.y].VoxelState,
                Input[indices.z].VoxelState,
                Input[indices.w].VoxelState
            )
        };
        
        // Process all 4 voxels in parallel
        var processed = ProcessVoxelBatch(voxels);
        
        // Store results
        Output[indices.x] = new VoxelData
        {
            VoxelType = (byte)processed.VoxelType.x,
            VoxelState = (byte)processed.VoxelState.x
        };
        // ... repeat for y, z, w components
    }
    
    private VoxelData4 ProcessVoxelBatch(VoxelData4 voxels)
    {
        // Vectorized processing logic
        var result = voxels;
        
        // Example: Apply the same operation to all voxels
        result.VoxelState = select(
            result.VoxelState,
            (uint4)1, // New state
            result.VoxelType == (uint4)(byte)VoxelType.Grass
        );
        
        return result;
    }
}
```

### Component Access Optimization

#### Component Type Caching

```csharp
public class OptimizedChunkSystem : SystemBase
{
    private EntityQuery _chunkQuery;
    private ComponentTypeHandle<VoxelChunk> _chunkTypeHandle;
    private ComponentTypeHandle<VoxelData> _voxelTypeHandle;
    private ComponentTypeHandle<MeshData> _meshTypeHandle;
    
    protected override void OnCreate()
    {
        _chunkQuery = GetEntityQuery(
            typeof(VoxelChunk),
            typeof(VoxelData),
            typeof(MeshData)
        );
        
        // Cache component type handles
        _chunkTypeHandle = GetComponentTypeHandle<VoxelChunk>();
        _voxelTypeHandle = GetComponentTypeHandle<VoxelData>();
        _meshTypeHandle = GetComponentTypeHandle<MeshData>();
    }
    
    protected override void OnUpdate()
    {
        // Update component type handles
        _chunkTypeHandle.Update(_chunkQuery);
        _voxelTypeHandle.Update(_chunkQuery);
        _meshTypeHandle.Update(_chunkQuery);
        
        // Process chunks with cached handles
        var chunkArray = _chunkQuery.ToComponentDataArray<VoxelChunk>(
            Allocator.TempJob
        );
        
        var voxelArray = _chunkQuery.ToComponentDataArray<VoxelData>(
            Allocator.TempJob
        );
        
        var meshArray = _chunkQuery.ToComponentDataArray<MeshData>(
            Allocator.TempJob
        );
        
        // Process data efficiently
        ProcessChunks(chunkArray, voxelArray, meshArray);
    }
    
    private void ProcessChunks(
        NativeArray<VoxelChunk> chunks,
        NativeArray<VoxelData> voxels,
        NativeArray<MeshData> meshes)
    {
        for (int i = 0; i < chunks.Length; i++)
        {
            // Process chunk data
            var chunk = chunks[i];
            var voxelData = voxels[i];
            var meshData = meshes[i];
            
            // Optimization: Only process chunks that need updating
            if (chunk.State == ChunkState.Ready)
            {
                UpdateChunkData(ref voxelData, ref meshData);
                voxels[i] = voxelData;
                meshes[i] = meshData;
            }
        }
    }
}
```

## Memory Management Optimization

### Memory Allocation Strategies

#### NativeArray Pooling

```csharp
public class MemoryPoolSystem : SystemBase
{
    private NativeListNativeArray<VoxelData>> _availableArrays;
    private NativeList<NativeArray<VoxelData>> _inUseArrays;
    private int _arraySize;
    
    protected override void OnCreate()
    {
        _arraySize = 16 * 16 * 16; // 16x16x16 chunk
        _availableArrays = new NativeList<NativeArray<VoxelData>>(
            64, Allocator.Persistent
        );
        _inUseArrays = new NativeList<NativeArray<VoxelData>>(
            64, Allocator.Persistent
        );
    }
    
    protected override void OnDestroy()
    {
        // Clean up all pooled arrays
        foreach (var array in _availableArrays)
        {
            array.Dispose();
        }
        foreach (var array in _inUseArrays)
        {
            array.Dispose();
        }
        
        _availableArrays.Dispose();
        _inUseArrays.Dispose();
    }
    
    public NativeArray<VoxelData> RentArray()
    {
        NativeArray<VoxelData> result;
        
        if (_availableArrays.Length > 0)
        {
            // Reuse existing array
            result = _availableArrays[_availableArrays.Length - 1];
            _availableArrays.RemoveAt(_availableArrays.Length - 1);
        }
        else
        {
            // Create new array
            result = new NativeArray<VoxelData>(
                _arraySize, Allocator.Persistent
            );
        }
        
        _inUseArrays.Add(result);
        return result;
    }
    
    public void ReturnArray(NativeArray<VoxelData> array)
    {
        // Find and remove from in-use list
        for (int i = 0; i < _inUseArrays.Length; i++)
        {
            if (_inUseArrays[i].GetUnsafePtr() == array.GetUnsafePtr())
            {
                _inUseArrays.RemoveAt(i);
                break;
            }
        }
        
        // Clear and add to available pool
        array.Clear();
        _availableArrays.Add(array);
    }
}
```

#### Component Size Optimization

```csharp
// Optimized voxel component with packed data
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CompactVoxelData : IComponentData
{
    // Pack multiple voxel properties into fewer bytes
    public uint PackedData;
    
    // Bit manipulation for efficient storage
    public byte VoxelType
    {
        get => (byte)((PackedData >> 0) & 0xFF);
        set => PackedData = (PackedData & ~(0xFF << 0)) | ((uint)value << 0);
    }
    
    public byte VoxelState
    {
        get => (byte)((PackedData >> 8) & 0xFF);
        set => PackedData = (PackedData & ~(0xFF << 8)) | ((uint)value << 8);
    }
    
    public byte VoxelMetadata
    {
        get => (byte)((PackedData >> 16) & 0xFF);
        set => PackedData = (PackedData & ~(0xFF << 16)) | ((uint)value << 16);
    }
    
    public byte VoxelFlags
    {
        get => (byte)((PackedData >> 24) & 0xFF);
        set => PackedData = (PackedData & ~(0xFF << 24)) | ((uint)value << 24);
    }
}
```

### Memory Layout Optimization

#### Cache-Friendly Data Structures

```csharp
[BurstCompile]
public struct CacheOptimizedChunkJob : IJobParallelFor
{
    // Structure of Arrays (SoA) for better cache performance
    [ReadOnly] public NativeArray<byte> VoxelTypes;
    [ReadOnly] public NativeArray<byte> VoxelStates;
    [ReadOnly] public NativeArray<byte> VoxelMetadata;
    [WriteOnly] public NativeArray<byte> OutputTypes;
    [WriteOnly] public NativeArray<byte> OutputStates;
    
    public void Execute(int index)
    {
        // Access all data for a single voxel at once
        byte type = VoxelTypes[index];
        byte state = VoxelStates[index];
        byte metadata = VoxelMetadata[index];
        
        // Process voxel
        if (type == (byte)VoxelType.Grass && state == (byte)VoxelState.Growing)
        {
            OutputTypes[index] = type;
            OutputStates[index] = (byte)VoxelState.Mature;
        }
        else
        {
            OutputTypes[index] = type;
            OutputStates[index] = state;
        }
    }
}
```

#### Archetype Optimization

```csharp
public class ArchetypeOptimizationSystem : SystemBase
{
    private EntityQuery _optimizedQuery;
    
    protected override void OnCreate()
    {
        // Create query with specific archetype for optimal performance
        _optimizedQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(VoxelChunk),
                typeof(VoxelData),
                typeof(MeshData)
            },
            None = new ComponentType[]
            {
                typeof(DirtyChunk),
                typeof(LoadingChunk)
            },
            Options = EntityQueryOptions.FilterWriteGroup
        });
    }
    
    protected override void OnUpdate()
    {
        // Process only entities that match the optimized archetype
        var chunks = _optimizedQuery.ToComponentDataArray<VoxelChunk>(
            Allocator.TempJob
        );
        
        var voxelData = _optimizedQuery.ToComponentDataArray<VoxelData>(
            Allocator.TempJob
        );
        
        var meshData = _optimizedQuery.ToComponentDataArray<MeshData>(
            Allocator.TempJob
        );
        
        // Process with guaranteed archetype compatibility
        ProcessOptimizedChunks(chunks, voxelData, meshData);
    }
}
```

## Job System Best Practices

### Job Safety and Dependencies

#### Proper Job Scheduling

```csharp
public class SafeJobSystem : SystemBase
{
    private EntityQuery _chunkQuery;
    private NativeArray<VoxelData> _voxelData;
    private NativeArray<JobHandle> _jobHandles;
    
    protected override void OnCreate()
    {
        _chunkQuery = GetEntityQuery(typeof(VoxelChunk));
        _voxelData = new NativeArray<VoxelData>(
            4096, Allocator.Persistent
        );
        _jobHandles = new NativeArray<JobHandle>(
            64, Allocator.TempJob
        );
    }
    
    protected override void OnUpdate()
    {
        int jobIndex = 0;
        
        // Schedule multiple jobs with proper safety
        var generationJob = new GenerateChunkJob
        {
            VoxelData = _voxelData,
            ChunkCount = _chunkQuery.CalculateEntityCount()
        };
        
        _jobHandles[jobIndex++] = generationJob.ScheduleParallel(
            _chunkQuery.CalculateEntityCount(), 64
        );
        
        // Schedule dependent jobs
        var meshJob = new GenerateMeshJob
        {
            VoxelData = _voxelData,
            MeshData = GetComponentDataFromEntity<MeshData>()
        };
        
        _jobHandles[jobIndex++] = meshJob.Schedule(
            _jobHandles[0], 64
        );
        
        // Wait for all jobs to complete
        JobHandle.CompleteAll(_jobHandles, 0, jobIndex);
        
        // Update dependencies
        Dependency = JobHandle.CombineDependencies(Dependency, _jobHandles[jobIndex - 1]);
    }
    
    protected override void OnDestroy()
    {
        _voxelData.Dispose();
        _jobHandles.Dispose();
    }
}
```

### Job Batching Strategies

#### Dynamic Job Batching

```csharp
public class DynamicJobBatchingSystem : SystemBase
{
    private EntityQuery _chunkQuery;
    private NativeList<JobHandle> _jobHandles;
    
    protected override void OnCreate()
    {
        _chunkQuery = GetEntityQuery(typeof(VoxelChunk));
        _jobHandles = new NativeList<JobHandle>(
            32, Allocator.TempJob
        );
    }
    
    protected override void OnUpdate()
    {
        _jobHandles.Clear();
        
        // Calculate optimal batch size based on available cores
        int batchSize = CalculateOptimalBatchSize();
        
        // Process chunks in batches
        for (int i = 0; i < _chunkQuery.CalculateEntityCount(); i += batchSize)
        {
            int endIndex = math.min(i + batchSize, _chunkQuery.CalculateEntityCount());
            int batchCount = endIndex - i;
            
            // Schedule batch job
            var batchJob = new ProcessChunkBatchJob
            {
                StartIndex = i,
                EndIndex = endIndex,
                ChunkData = GetComponentDataFromEntity<VoxelChunk>()
            };
            
            JobHandle batchHandle = batchJob.Schedule(batchCount, 64);
            _jobHandles.Add(batchHandle);
        }
        
        // Complete all batch jobs
        if (_jobHandles.Length > 0)
        {
            JobHandle.CompleteAll(_jobHandles);
            Dependency = JobHandle.CombineDependencies(
                Dependency, _jobHandles[_jobHandles.Length - 1]
            );
        }
    }
    
    private int CalculateOptimalBatchSize()
    {
        // Dynamic batch size based on system load
        int coreCount = SystemInfo.processorCount;
        return math.max(16, 64 / coreCount);
    }
}
```

## Parallel Processing Patterns

### Spatial Partitioning

#### Octree-Based Processing

```csharp
[BurstCompile]
public struct OctreeProcessingJob : IJob
{
    public NativeArray<VoxelData> Voxels;
    public NativeArray<int3> VoxelPositions;
    public int3 OctreeOrigin;
    public int OctreeSize;
    
    public void Execute()
    {
        // Process voxels in spatial partitions
        ProcessOctreeNodes(OctreeOrigin, OctreeSize, 0);
    }
    
    private void ProcessOctreeNodes(int3 origin, int size, int depth)
    {
        if (depth > 4 || size < 4) // Base case
        {
            ProcessVoxelsInRegion(origin, size);
            return;
        }
        
        // Recursively process octants
        int halfSize = size / 2;
        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    int3 childOrigin = origin + new int3(
                        x * halfSize, y * halfSize, z * halfSize
                    );
                    ProcessOctreeNodes(childOrigin, halfSize, depth + 1);
                }
            }
        }
    }
    
    private void ProcessVoxelsInRegion(int3 origin, int size)
    {
        // Process all voxels in this region
        for (int y = 0; y < size; y++)
        {
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    int3 pos = origin + new int3(x, y, z);
                    int index = GetVoxelIndex(pos);
                    
                    if (index >= 0 && index < Voxels.Length)
                    {
                        ProcessVoxel(index, pos);
                    }
                }
            }
        }
    }
}
```

### Frustum Culling

#### View Frustum Optimization

```csharp
[BurstCompile]
public struct FrustumCullingJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> VoxelPositions;
    [ReadOnly] public Plane[] FrustumPlanes;
    [WriteOnly] public NativeArray<bool> VisibleVoxels;
    
    public void Execute(int index)
    {
        float3 voxelPos = VoxelPositions[index];
        
        // Check if voxel is inside frustum
        bool isVisible = true;
        for (int i = 0; i < FrustumPlanes.Length; i++)
        {
            if (math.dot(FrustumPlanes[i].normal, voxelPos) + 
                FrustumPlanes[i].distance < 0f)
            {
                isVisible = false;
                break;
            }
        }
        
        VisibleVoxels[index] = isVisible;
    }
}
```

## Cache Optimization

### Cache-Aware Data Access

#### Cache-Friendly Voxel Processing

```csharp
[BurstCompile]
public struct CacheAwareJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<VoxelData> Voxels;
    [WriteOnly] public NativeArray<byte> Results;
    
    public void Execute(int index)
    {
        // Process voxels in cache-friendly patterns
        // Access neighboring voxels while they're still in cache
        
        // Get voxel and its neighbors
        var center = Voxels[index];
        var neighbors = GetNeighbors(index);
        
        // Process with cache-friendly access pattern
        Results[index] = ProcessWithCacheOptimization(center, neighbors);
    }
    
    private VoxelData4 GetNeighbors(int centerIndex)
    {
        // Load neighboring voxels efficiently
        // This minimizes cache misses
        return new VoxelData4
        {
            VoxelType = new uint4(
                Voxels[centerIndex - 1].VoxelType,
                Voxels[centerIndex + 1].VoxelType,
                Voxels[centerIndex - CHUNK_SIZE].VoxelType,
                Voxels[centerIndex + CHUNK_SIZE].VoxelType
            )
        };
    }
}
```

### Memory Prefetching

#### Prefetch Optimization

```csharp
[BurstCompile]
public struct PrefetchOptimizedJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<VoxelData> Input;
    [WriteOnly] public NativeArray<VoxelData> Output;
    
    public void Execute(int index)
    {
        // Prefetch data that will be needed soon
        PrefetchData(index);
        
        // Process current voxel
        Output[index] = ProcessVoxel(Input[index]);
        
        // Prefetch next data
        PrefetchData(index + 1);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PrefetchData(int index)
    {
        // Prefetch data to reduce cache misses
        if (index + 3 < Input.Length)
        {
            var prefetch = new VoxelData4
            {
                VoxelType = new uint4(
                    Input[index].VoxelType,
                    Input[index + 1].VoxelType,
                    Input[index + 2].VoxelType,
                    Input[index + 3].VoxelType
                )
            };
            // Prefetch data (compiler will optimize this)
        }
    }
}
```

## Memory Pooling

### Object Pooling for Entities

#### Entity Pool System

```csharp
public class EntityPoolSystem : SystemBase
{
    private NativeList<Entity> _availableEntities;
    private NativeList<Entity> _activeEntities;
    private EntityManager _entityManager;
    
    protected override void OnCreate()
    {
        _availableEntities = new NativeList<Entity>(
            256, Allocator.Persistent
        );
        _activeEntities = new NativeList<Entity>(
            256, Allocator.Persistent
        );
        _entityManager = World.EntityManager;
    }
    
    protected override void OnDestroy()
    {
        _availableEntities.Dispose();
        _activeEntities.Dispose();
    }
    
    public Entity RentEntity()
    {
        Entity entity;
        
        if (_availableEntities.Length > 0)
        {
            // Reuse existing entity
            entity = _availableEntities[_availableEntities.Length - 1];
            _availableEntities.RemoveAt(_availableEntities.Length - 1);
        }
        else
        {
            // Create new entity
            entity = _entityManager.CreateEntity(
                typeof(VoxelData),
                typeof(MeshData)
            );
        }
        
        _activeEntities.Add(entity);
        return entity;
    }
    
    public void ReturnEntity(Entity entity)
    {
        // Find and remove from active list
        for (int i = 0; i < _activeEntities.Length; i++)
        {
            if (_activeEntities[i] == entity)
            {
                _activeEntities.RemoveAt(i);
                break;
            }
        }
        
        // Reset entity and add to available pool
        _entityManager.SetComponentData(entity, new VoxelData());
        _availableEntities.Add(entity);
    }
}
```

### Component Pooling

#### Component Data Pooling

```csharp
public class ComponentPoolSystem : SystemBase
{
    private NativeList<VoxelData> _availableComponents;
    private NativeList<VoxelData> _activeComponents;
    
    protected override void OnCreate()
    {
        _availableComponents = new NativeList<VoxelData>(
            1024, Allocator.Persistent
        );
        _activeComponents = new NativeList<VoxelData>(
            1024, Allocator.Persistent
        );
    }
    
    protected override void OnDestroy()
    {
        _availableComponents.Dispose();
        _activeComponents.Dispose();
    }
    
    public VoxelData RentComponent()
    {
        VoxelData component;
        
        if (_availableComponents.Length > 0)
        {
            // Reuse existing component
            component = _availableComponents[_availableComponents.Length - 1];
            _availableComponents.RemoveAt(_availableComponents.Length - 1);
        }
        else
        {
            // Create new component
            component = new VoxelData();
        }
        
        _activeComponents.Add(component);
        return component;
    }
    
    public void ReturnComponent(VoxelData component)
    {
        // Find and remove from active list
        for (int i = 0; i < _activeComponents.Length; i++)
        {
            if (_activeComponents[i].Equals(component))
            {
                _activeComponents.RemoveAt(i);
                break;
            }
        }
        
        // Reset component and add to available pool
        component.VoxelType = 0;
        component.VoxelState = 0;
        _availableComponents.Add(component);
    }
}
```

## Profiling and Analysis

### Performance Profiling

#### Built-in Profiler Integration

```csharp
public class ProfilingSystem : SystemBase
{
    private FrameData _frameData;
    private NativeArray<SampleData> _samples;
    
    protected override void OnCreate()
    {
        _samples = new NativeArray<SampleData>(
            64, Allocator.Persistent
        );
    }
    
    protected override void OnUpdate()
    {
        using (var profilerSample = 
               new SampleMarker("VoxelProcessing"))
        {
            // Start profiling section
            _frameData.StartFrame();
            
            // Profile chunk generation
            ProfileChunkGeneration();
            
            // Profile mesh generation
            ProfileMeshGeneration();
            
            // Profile physics
            ProfilePhysics();
            
            // End profiling section
            _frameData.EndFrame();
        }
    }
    
    private void ProfileChunkGeneration()
    {
        using (var sample = new SampleMarker("ChunkGeneration"))
        {
            var generationJob = new GenerateChunkJob();
            JobHandle handle = generationJob.ScheduleParallel(
                _chunkQuery.CalculateEntityCount(), 64
            );
            
            // Wait for completion and measure time
            handle.Complete();
            _frameData.RecordSample("ChunkGeneration", handle.JobTime);
        }
    }
    
    private void ProfileMeshGeneration()
    {
        using (var sample = new SampleMarker("MeshGeneration"))
        {
            var meshJob = new GenerateMeshJob();
            JobHandle handle = meshJob.Schedule();
            
            handle.Complete();
            _frameData.RecordSample("MeshGeneration", handle.JobTime);
        }
    }
    
    private void ProfilePhysics()
    {
        using (var sample = new SampleMarker("Physics"))
        {
            var physicsJob = new UpdatePhysicsJob();
            JobHandle handle = physicsJob.Schedule();
            
            handle.Complete();
            _frameData.RecordSample("Physics", handle.JobTime);
        }
    }
}
```

### Custom Performance Metrics

#### Performance Tracking System

```csharp
public class PerformanceTracker : SystemBase
{
    private struct PerformanceMetrics
    {
        public long TotalFrameTime;
        public long ChunkGenerationTime;
        public long MeshGenerationTime;
        public long PhysicsTime;
        public int ActiveChunks;
        public int ActiveVoxels;
        public int JobCount;
        public int MemoryUsage;
    }
    
    private NativeList<PerformanceMetrics> _metricsHistory;
    private PerformanceMetrics _currentMetrics;
    
    protected override void OnCreate()
    {
        _metricsHistory = new NativeList<PerformanceMetrics>(
            1024, Allocator.Persistent
        );
    }
    
    protected override void OnUpdate()
    {
        // Reset current metrics
        _currentMetrics = new PerformanceMetrics();
        
        // Collect metrics
        CollectPerformanceMetrics();
        
        // Store metrics
        _metricsHistory.Add(_currentMetrics);
        
        // Keep only recent history
        if (_metricsHistory.Length > 1000)
        {
            _metricsHistory.RemoveAt(0);
        }
    }
    
    private void CollectPerformanceMetrics()
    {
        // Measure frame time
        _currentMetrics.TotalFrameTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        // Count active chunks
        _currentMetrics.ActiveChunks = _chunkQuery.CalculateEntityCount();
        
        // Count active voxels
        _currentMetrics.ActiveVoxels = CountActiveVoxels();
        
        // Measure job performance
        MeasureJobPerformance();
        
        // Measure memory usage
        _currentMetrics.MemoryUsage = System.GC.GetTotalMemory(false);
    }
    
    private void MeasureJobPerformance()
    {
        // Measure chunk generation
        var chunkGenStart = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // ... run chunk generation job ...
        var chunkGenEnd = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _currentMetrics.ChunkGenerationTime = chunkGenEnd - chunkGenStart;
        
        // Measure mesh generation
        var meshGenStart = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // ... run mesh generation job ...
        var meshGenEnd = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _currentMetrics.MeshGenerationTime = meshGenEnd - meshGenStart;
        
        // Measure physics
        var physicsStart = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // ... run physics job ...
        var physicsEnd = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _currentMetrics.PhysicsTime = physicsEnd - physicsStart;
    }
    
    public PerformanceMetrics GetLatestMetrics()
    {
        if (_metricsHistory.Length > 0)
        {
            return _metricsHistory[_metricsHistory.Length - 1];
        }
        return new PerformanceMetrics();
    }
}
```

## Performance Metrics

### Key Performance Indicators

#### Target Performance Metrics

| Metric | Target Value | Measurement Method |
|--------|--------------|-------------------|
| Frame Time | < 16.67ms (60 FPS) | Unity Profiler |
| Chunk Generation | < 5ms per chunk | Custom timing |
| Mesh Generation | < 8ms per frame | Job timing |
| Physics Update | < 2ms per frame | Job timing |
| Memory Usage | < 2GB for 1000 chunks | System profiler |
| Active Entities | < 10,000 | Entity count query |
| Job Count | < 64 concurrent | Job handle tracking |
| Cache Hit Rate | > 80% | Custom profiling |

### Performance Optimization Checklist

#### Optimization Steps

1. **Profile Before Optimizing**
   - Use Unity Profiler to identify bottlenecks
   - Measure baseline performance
   - Focus on the slowest systems first

2. **Optimize Memory Access**
   - Use Structure of Arrays (SoA) pattern
   - Minimize cache misses with spatial locality
   - Prefetch data for better cache utilization

3. **Optimize Job Scheduling**
   - Use appropriate job types (ParallelFor vs regular)
   - Balance job count vs parallelism
   - Minimize job dependencies

4. **Optimize Component Design**
   - Keep components small and focused
   - Use compact data structures
   - Minimize component count per entity

5. **Optimize Memory Usage**
   - Use memory pooling for frequently allocated data
   - Reuse NativeArrays instead of constant allocation
   - Clean up unused resources promptly

6. **Optimize Rendering**
   - Implement frustum culling
   - Use level of detail (LOD) systems
   - Optimize mesh generation with greedy meshing

7. **Test and Validate**
   - Measure performance after each optimization
   - Ensure optimizations don't break functionality
   - Test on target hardware

### Performance Monitoring Tools

#### Recommended Tools

1. **Unity Profiler**
   - CPU usage analysis
   - Memory allocation tracking
   - Rendering performance metrics

2. **Unity Frame Debugger**
   - Render pipeline analysis
   - Draw call optimization
   - Shader performance analysis

3. **Custom Performance System**
   - Real-time performance metrics
   - Historical performance data
   - Custom performance alerts

4. **System Performance Monitor**
   - Memory usage tracking
   - CPU utilization monitoring
   - Disk I/O analysis

## Next Steps

After understanding these performance optimization techniques, proceed to:
- [04-burst-compiler.md](04-burst-compiler.md) - Burst compiler usage and optimization
- [05-mesh-generation.md](05-mesh-generation.md) - Mesh generation optimization