# Memory Management Strategies

This document covers memory management techniques specifically for voxel engines built with Unity DOTS.

## Table of Contents

- [Memory Management Fundamentals](#memory-management-fundamentals)
- [NativeArray Optimization](#nativearray-optimization)
- [Memory Pooling Systems](#memory-pooling-systems)
- [Chunk Management](#chunk-management)
- [Streaming and Loading](#streaming-and-loading)
- [Memory Profiling](#memory-profiling)
- [Garbage Collection Avoidance](#garbage-collection-avoidance)
- [Memory Budget Management](#memory-budget-management)
- [Best Practices](#best-practices)

## Memory Management Fundamentals

### Memory Architecture Overview

```
Unity Memory Hierarchy:
┌─────────────────────────────────────────┐
│           GPU Memory                    │
│  - Vertex Buffers                       │
│  - Index Buffers                        │
│  - Texture Data                         │
└─────────────────────────────────────────┘
┌─────────────────────────────────────────┐
│           Unity Managed Memory          │
│  - GameObjects                          │
│  - Components                           │
│  - Unity Objects                        │
└─────────────────────────────────────────┘
┌─────────────────────────────────────────┐
│           DOTS Memory                   │
│  - NativeArrays                         │
│  - ComponentData                        │
│  - EntityArchetypes                     │
└─────────────────────────────────────────┘
┌─────────────────────────────────────────┐
│           System Memory                 │
│  - Stack Memory                         │
│  - Heap Memory                          │
│  - Memory Mapped Files                  │
└─────────────────────────────────────────┘
```

### Memory Allocation Patterns

```csharp
// Bad: Frequent allocation/deallocation
[BurstCompile]
public struct InefficientMemoryJob : IJobParallelFor
{
    public void Execute(int index)
    {
        // Allocate memory in job execution
        var tempArray = new NativeArray<VoxelData>(16, Allocator.Temp);
        
        // Use temp array
        ProcessVoxels(tempArray);
        
        // Dispose in job execution
        tempArray.Dispose();
    }
}

// Good: Reuse allocated memory
[BurstCompile]
public struct EfficientMemoryJob : IJobParallelFor
{
    private NativeArray<VoxelData> _tempArray;
    
    public void SetData(NativeArray<VoxelData> data)
    {
        _tempArray = data;
    }
    
    public void Execute(int index)
    {
        // Reuse allocated memory
        ProcessVoxels(_tempArray);
    }
}
```

## NativeArray Optimization

### NativeArray Best Practices

```csharp
[BurstCompile]
public struct OptimizedNativeArrayJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<VoxelData> InputVoxels;
    [WriteOnly] public NativeArray<VoxelData> OutputVoxels;
    
    public void Execute(int index)
    {
        // Access NativeArray efficiently
        var inputVoxel = InputVoxels[index];
        
        // Process voxel
        var outputVoxel = ProcessVoxel(inputVoxel);
        
        // Write back efficiently
        OutputVoxels[index] = outputVoxel;
    }
    
    private VoxelData ProcessVoxel(VoxelData voxel)
    {
        // Process voxel data
        voxel.VoxelState = (byte)((voxel.VoxelState + 1) % 256);
        return voxel;
    }
}
```

### NativeArray Sizing

```csharp
public class NativeArraySizingSystem : SystemBase
{
    private struct ArraySizeData
    {
        public int ChunkSize;
        public int EstimatedVoxels;
        public int SafetyMargin;
        public int ActualSize;
    }
    
    private NativeList<ArraySizeData> _sizeData;
    
    protected override void OnCreate()
    {
        _sizeData = new NativeList<ArraySizeData>(
            64, Allocator.Persistent
        );
    }
    
    protected override void OnDestroy()
    {
        _sizeData.Dispose();
    }
    
    protected override void OnUpdate()
    {
        // Calculate optimal NativeArray sizes
        CalculateOptimalSizes();
    }
    
    private void CalculateOptimalSizes()
    {
        // Get chunk configuration
        var chunkConfig = GetSingleton<ChunkConfig>();
        
        // Calculate sizes for different chunk types
        foreach (var chunkType in chunkConfig.ChunkTypes)
        {
            var sizeData = new ArraySizeData
            {
                ChunkSize = chunkType.Size,
                EstimatedVoxels = chunkType.Size * chunkType.Size * chunkType.Size,
                SafetyMargin = (int)(chunkType.Size * chunkType.Size * chunkType.Size * 0.1f),
                ActualSize = chunkType.Size * chunkType.Size * chunkType.Size + 
                           (int)(chunkType.Size * chunkType.Size * chunkType.Size * 0.1f)
            };
            
            _sizeData.Add(sizeData);
        }
    }
}
```

### NativeArray Access Patterns

```csharp
[BurstCompile]
public struct AccessPatternJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<VoxelData> Voxels;
    
    public void Execute(int index)
    {
        // Good: Sequential access pattern
        ProcessSequentialAccess(index);
        
        // Bad: Random access pattern
        // ProcessRandomAccess(index);
    }
    
    private void ProcessSequentialAccess(int index)
    {
        // Access data sequentially for better cache performance
        var voxel = Voxels[index];
        
        // Process voxel
        voxel.VoxelState = (byte)((voxel.VoxelState + 1) % 256);
        
        // Write back
        Voxels[index] = voxel;
    }
    
    private void ProcessRandomAccess(int index)
    {
        // Bad: Random access pattern
        // This causes cache misses and poor performance
        var randomIndex = GetRandomIndex(index);
        var voxel = Voxels[randomIndex];
        
        // Process voxel
        voxel.VoxelState = (byte)((voxel.VoxelState + 1) % 256);
        
        // Write back
        Voxels[randomIndex] = voxel;
    }
    
    private int GetRandomIndex(int index)
    {
        // Generate random index (causes cache misses)
        return (index * 1664525 + 1013904223) % Voxels.Length;
    }
}
```

## Memory Pooling Systems

### NativeArray Pool

```csharp
public class NativeArrayPoolSystem : SystemBase
{
    private struct PoolEntry
    {
        public NativeArray<VoxelData> Array;
        public int Size;
        public bool IsInUse;
        public float LastUsedTime;
    }
    
    private NativeList<PoolEntry> _pool;
    private NativeListNativeArray<VoxelData>> _inUseArrays;
    private int _maxPoolSize;
    private float _arrayLifetime;
    
    protected override void OnCreate()
    {
        _pool = new NativeList<PoolEntry>(64, Allocator.Persistent);
        _inUseArrays = new NativeList<NativeArray<VoxelData>>(64, Allocator.Persistent);
        _maxPoolSize = 256;
        _arrayLifetime = 30.0f; // 30 seconds
    }
    
    protected override void OnDestroy()
    {
        // Clean up all pooled arrays
        foreach (var entry in _pool)
        {
            entry.Array.Dispose();
        }
        
        foreach (var array in _inUseArrays)
        {
            array.Dispose();
        }
        
        _pool.Dispose();
        _inUseArrays.Dispose();
    }
    
    protected override void OnUpdate()
    {
        // Update pool
        UpdatePool();
    }
    
    public NativeArray<VoxelData> RentArray(int size)
    {
        NativeArray<VoxelData> result;
        
        // Try to find a suitable array in the pool
        int poolIndex = FindSuitableArray(size);
        
        if (poolIndex != -1)
        {
            // Reuse existing array
            var entry = _pool[poolIndex];
            result = entry.Array;
            entry.IsInUse = true;
            entry.LastUsedTime = Time.time;
            _pool[poolIndex] = entry;
        }
        else
        {
            // Create new array
            result = new NativeArray<VoxelData>(size, Allocator.Persistent);
            
            // Add to pool if we have space
            if (_pool.Length < _maxPoolSize)
            {
                _pool.Add(new PoolEntry
                {
                    Array = result,
                    Size = size,
                    IsInUse = true,
                    LastUsedTime = Time.time
                });
            }
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
        
        // Find in pool and mark as available
        for (int i = 0; i < _pool.Length; i++)
        {
            if (_pool[i].Array.GetUnsafePtr() == array.GetUnsafePtr())
            {
                var entry = _pool[i];
                entry.IsInUse = false;
                _pool[i] = entry;
                return;
            }
        }
        
        // If not found in pool, dispose it
        array.Dispose();
    }
    
    private int FindSuitableArray(int size)
    {
        // Find an available array that's at least as large as needed
        for (int i = 0; i < _pool.Length; i++)
        {
            var entry = _pool[i];
            if (!entry.IsInUse && entry.Size >= size)
            {
                return i;
            }
        }
        
        return -1;
    }
    
    private void UpdatePool()
    {
        // Clean up old unused arrays
        float currentTime = Time.time;
        
        for (int i = _pool.Length - 1; i >= 0; i--)
        {
            var entry = _pool[i];
            
            if (!entry.IsInUse && 
                currentTime - entry.LastUsedTime > _arrayLifetime)
            {
                // Remove old unused array
                entry.Array.Dispose();
                _pool.RemoveAt(i);
            }
        }
    }
}
```

### Component Data Pool

```csharp
public class ComponentDataPoolSystem : SystemBase
{
    private struct ComponentPool<T> where T : unmanaged, IComponentData
    {
        public NativeList<T> AvailableComponents;
        public NativeList<T> InUseComponents;
        public int MaxPoolSize;
        public float ComponentLifetime;
        
        public ComponentPool(int maxSize, float lifetime)
        {
            AvailableComponents = new NativeList<T>(maxSize, Allocator.Persistent);
            InUseComponents = new NativeList<T>(maxSize, Allocator.Persistent);
            MaxPoolSize = maxSize;
            ComponentLifetime = lifetime;
        }
        
        public void Dispose()
        {
            AvailableComponents.Dispose();
            InUseComponents.Dispose();
        }
        
        public T RentComponent()
        {
            T component;
            
            if (AvailableComponents.Length > 0)
            {
                // Reuse existing component
                component = AvailableComponents[AvailableComponents.Length - 1];
                AvailableComponents.RemoveAt(AvailableComponents.Length - 1);
            }
            else
            {
                // Create new component
                component = default(T);
            }
            
            InUseComponents.Add(component);
            return component;
        }
        
        public void ReturnComponent(T component)
        {
            // Find and remove from in-use list
            for (int i = 0; i < InUseComponents.Length; i++)
            {
                if (EqualityComparer<T>.Default.Equals(InUseComponents[i], component))
                {
                    InUseComponents.RemoveAt(i);
                    break;
                }
            }
            
            // Add to available pool if we have space
            if (AvailableComponents.Length < MaxPoolSize)
            {
                AvailableComponents.Add(component);
            }
        }
        
        public void UpdatePool(float currentTime)
        {
            // Clean up old unused components
            for (int i = AvailableComponents.Length - 1; i >= 0; i--)
            {
                // Remove old unused components
                AvailableComponents.RemoveAt(i);
            }
        }
    }
    
    private ComponentPool<VoxelData> _voxelDataPool;
    private ComponentPool<MeshData> _meshDataPool;
    private ComponentPool<ChunkData> _chunkDataPool;
    
    protected override void OnCreate()
    {
        _voxelDataPool = new ComponentPool<VoxelData>(1024, 30.0f);
        _meshDataPool = new ComponentPool<MeshData>(256, 30.0f);
        _chunkDataPool = new ComponentPool<ChunkData>(64, 30.0f);
    }
    
    protected override void OnDestroy()
    {
        _voxelDataPool.Dispose();
        _meshDataPool.Dispose();
        _chunkDataPool.Dispose();
    }
    
    protected override void OnUpdate()
    {
        // Update all pools
        float currentTime = Time.time;
        _voxelDataPool.UpdatePool(currentTime);
        _meshDataPool.UpdatePool(currentTime);
        _chunkDataPool.UpdatePool(currentTime);
    }
    
    public VoxelData RentVoxelData()
    {
        return _voxelDataPool.RentComponent();
    }
    
    public void ReturnVoxelData(VoxelData data)
    {
        _voxelDataPool.ReturnComponent(data);
    }
    
    public MeshData RentMeshData()
    {
        return _meshDataPool.RentComponent();
    }
    
    public void ReturnMeshData(MeshData data)
    {
        _meshDataPool.ReturnComponent(data);
    }
    
    public ChunkData RentChunkData()
    {
        return _chunkDataPool.RentComponent();
    }
    
    public void ReturnChunkData(ChunkData data)
    {
        _chunkDataPool.ReturnComponent(data);
    }
}
```

### Job Pool System

```csharp
public class JobPoolSystem : SystemBase
{
    private struct JobPoolEntry
    {
        public IJob Job;
        public bool IsInUse;
        public float LastUsedTime;
        public System.Type JobType;
    }
    
    private NativeList<JobPoolEntry> _jobPool;
    private NativeList<IJob> _inUseJobs;
    private int _maxPoolSize;
    private float _jobLifetime;
    
    protected override void OnCreate()
    {
        _jobPool = new NativeList<JobPoolEntry>(64, Allocator.Persistent);
        _inUseJobs = new NativeList<IJob>(64, Allocator.Persistent);
        _maxPoolSize = 128;
        _jobLifetime = 60.0f; // 60 seconds
    }
    
    protected override void OnDestroy()
    {
        // Clean up all pooled jobs
        foreach (var entry in _jobPool)
        {
            entry.Job.Dispose();
        }
        
        foreach (var job in _inUseJobs)
        {
            job.Dispose();
        }
        
        _jobPool.Dispose();
        _inUseJobs.Dispose();
    }
    
    protected override void OnUpdate()
    {
        // Update job pool
        UpdateJobPool();
    }
    
    public T RentJob<T>() where T : struct, IJob
    {
        T job;
        
        // Try to find a suitable job in the pool
        int poolIndex = FindSuitableJob<T>();
        
        if (poolIndex != -1)
        {
            // Reuse existing job
            var entry = _jobPool[poolIndex];
            job = (T)entry.Job;
            entry.IsInUse = true;
            entry.LastUsedTime = Time.time;
            _jobPool[poolIndex] = entry;
        }
        else
        {
            // Create new job
            job = new T();
        }
        
        _inUseJobs.Add(job);
        return job;
    }
    
    public void ReturnJob<T>(T job) where T : struct, IJob
    {
        // Find and remove from in-use list
        for (int i = 0; i < _inUseJobs.Length; i++)
        {
            if (_inUseJobs[i].Equals(job))
            {
                _inUseJobs.RemoveAt(i);
                break;
            }
        }
        
        // Find in pool and mark as available
        for (int i = 0; i < _jobPool.Length; i++)
        {
            if (_jobPool[i].Job.Equals(job))
            {
                var entry = _jobPool[i];
                entry.IsInUse = false;
                _jobPool[i] = entry;
                return;
            }
        }
        
        // If not found in pool, dispose it
        job.Dispose();
    }
    
    private int FindSuitableJob<T>() where T : struct, IJob
    {
        // Find an available job of the correct type
        for (int i = 0; i < _jobPool.Length; i++)
        {
            var entry = _jobPool[i];
            if (!entry.IsInUse && entry.JobType == typeof(T))
            {
                return i;
            }
        }
        
        return -1;
    }
    
    private void UpdateJobPool()
    {
        // Clean up old unused jobs
        float currentTime = Time.time;
        
        for (int i = _jobPool.Length - 1; i >= 0; i--)
        {
            var entry = _jobPool[i];
            
            if (!entry.IsInUse && 
                currentTime - entry.LastUsedTime > _jobLifetime)
            {
                // Remove old unused job
                entry.Job.Dispose();
                _jobPool.RemoveAt(i);
            }
        }
    }
}
```

## Chunk Management

### Chunk Memory Management

```csharp
public class ChunkMemorySystem : SystemBase
{
    private struct ChunkMemoryData
    {
        public Entity ChunkEntity;
        public NativeArray<VoxelData> VoxelData;
        public NativeArray<MeshData> MeshData;
        public NativeArray<PhysicsData> PhysicsData;
        public float LastAccessTime;
        public bool IsLoaded;
        public int MemoryUsage;
    }
    
    private NativeList<ChunkMemoryData> _chunkMemoryData;
    private NativeList<Entity> _unloadedChunks;
    private int _maxMemoryUsage;
    private float _unloadDistance;
    
    protected override void OnCreate()
    {
        _chunkMemoryData = new NativeList<ChunkMemoryData>(
            256, Allocator.Persistent
        );
        _unloadedChunks = new NativeList<Entity>(
            64, Allocator.TempJob
        );
        _maxMemoryUsage = 1024 * 1024 * 1024; // 1GB
        _unloadDistance = 128.0f; // 128 units
    }
    
    protected override void OnDestroy()
    {
        _chunkMemoryData.Dispose();
        _unloadedChunks.Dispose();
    }
    
    protected override void OnUpdate()
    {
        // Update chunk memory management
        UpdateChunkMemory();
    }
    
    private void UpdateChunkMemory()
    {
        // Get player position
        float3 playerPosition = GetSingleton<PlayerPosition>().Value;
        
        // Update chunk memory data
        for (int i = 0; i < _chunkMemoryData.Length; i++)
        {
            var memoryData = _chunkMemoryData[i];
            
            if (memoryData.IsLoaded)
            {
                // Get chunk position
                float3 chunkPosition = GetComponentData<VoxelChunk>(
                    memoryData.ChunkEntity
                ).Position;
                
                float distance = math.distance(playerPosition, chunkPosition);
                
                // Unload distant chunks
                if (distance > _unloadDistance)
                {
                    _unloadedChunks.Add(memoryData.ChunkEntity);
                    memoryData.IsLoaded = false;
                    _chunkMemoryData[i] = memoryData;
                }
                else
                {
                    // Update last access time
                    memoryData.LastAccessTime = Time.time;
                    _chunkMemoryData[i] = memoryData;
                }
            }
            else
            {
                // Get chunk position
                float3 chunkPosition = GetComponentData<VoxelChunk>(
                    memoryData.ChunkEntity
                ).Position;
                
                float distance = math.distance(playerPosition, chunkPosition);
                
                // Load nearby chunks
                if (distance < _unloadDistance * 0.5f)
                {
                    LoadChunk(memoryData.ChunkEntity);
                    memoryData.IsLoaded = true;
                    _chunkMemoryData[i] = memoryData;
                }
            }
        }
        
        // Process unloading
        ProcessUnloading();
        
        // Check memory budget
        CheckMemoryBudget();
    }
    
    private void LoadChunk(Entity chunkEntity)
    {
        // Load chunk data
        var memoryData = new ChunkMemoryData
        {
            ChunkEntity = chunkEntity,
            VoxelData = new NativeArray<VoxelData>(
                16 * 16 * 16, Allocator.Persistent
            ),
            MeshData = new NativeArray<MeshData>(
                1024, Allocator.Persistent
            ),
            PhysicsData = new NativeArray<PhysicsData>(
                256, Allocator.Persistent
            ),
            LastAccessTime = Time.time,
            IsLoaded = true,
            MemoryUsage = CalculateMemoryUsage()
        };
        
        _chunkMemoryData.Add(memoryData);
    }
    
    private void ProcessUnloading()
    {
        for (int i = 0; i < _unloadedChunks.Length; i++)
        {
            Entity chunkEntity = _unloadedChunks[i];
            
            // Find memory data for this chunk
            int memoryIndex = FindMemoryDataIndex(chunkEntity);
            if (memoryIndex != -1)
            {
                var memoryData = _chunkMemoryData[memoryIndex];
                
                // Unload chunk data
                memoryData.VoxelData.Dispose();
                memoryData.MeshData.Dispose();
                memoryData.PhysicsData.Dispose();
                
                _chunkMemoryData.RemoveAt(memoryIndex);
            }
        }
        
        _unloadedChunks.Clear();
    }
    
    private void CheckMemoryBudget()
    {
        // Calculate total memory usage
        int totalMemoryUsage = 0;
        for (int i = 0; i < _chunkMemoryData.Length; i++)
        {
            totalMemoryUsage += _chunkMemoryData[i].MemoryUsage;
        }
        
        // If we're over budget, unload some chunks
        if (totalMemoryUsage > _maxMemoryUsage)
        {
            UnloadOldestChunks();
        }
    }
    
    private void UnloadOldestChunks()
    {
        // Sort chunks by last access time
        for (int i = 0; i < _chunkMemoryData.Length - 1; i++)
        {
            for (int j = i + 1; j < _chunkMemoryData.Length; j++)
            {
                if (_chunkMemoryData[i].LastAccessTime > 
                    _chunkMemoryData[j].LastAccessTime)
                {
                    var temp = _chunkMemoryData[i];
                    _chunkMemoryData[i] = _chunkMemoryData[j];
                    _chunkMemoryData[j] = temp;
                }
            }
        }
        
        // Unload oldest chunks until we're under budget
        int targetMemoryUsage = (int)(_maxMemoryUsage * 0.8f); // 80% of budget
        int currentMemoryUsage = 0;
        
        for (int i = _chunkMemoryData.Length - 1; i >= 0; i--)
        {
            currentMemoryUsage += _chunkMemoryData[i].MemoryUsage;
            
            if (currentMemoryUsage > targetMemoryUsage)
            {
                var memoryData = _chunkMemoryData[i];
                
                // Unload chunk
                memoryData.VoxelData.Dispose();
                memoryData.MeshData.Dispose();
                memoryData.PhysicsData.Dispose();
                
                _chunkMemoryData.RemoveAt(i);
            }
        }
    }
    
    private int FindMemoryDataIndex(Entity chunkEntity)
    {
        for (int i = 0; i < _chunkMemoryData.Length; i++)
        {
            if (_chunkMemoryData[i].ChunkEntity == chunkEntity)
            {
                return i;
            }
        }
        return -1;
    }
    
    private int CalculateMemoryUsage()
    {
        // Calculate memory usage for a chunk
        int voxelMemory = 16 * 16 * 16 * sizeof(VoxelData);
        int meshMemory = 1024 * sizeof(MeshData);
        int physicsMemory = 256 * sizeof(PhysicsData);
        
        return voxelMemory + meshMemory + physicsMemory;
    }
}
```

### Chunk Streaming System

```csharp
public class ChunkStreamingSystem : SystemBase
{
    private struct StreamingRequest
    {
        public Entity ChunkEntity;
        public float3 Position;
        public float Priority;
        public bool IsCompleted;
    }
    
    private NativeList<StreamingRequest> _streamingRequests;
    private NativeList<Entity> _loadingChunks;
    private NativeList<Entity> _unloadingChunks;
    private int _maxConcurrentLoads;
    private float _streamingDistance;
    
    protected override void OnCreate()
    {
        _streamingRequests = new NativeList<StreamingRequest>(
            64, Allocator.Persistent
        );
        _loadingChunks = new NativeList<Entity>(
            16, Allocator.TempJob
        );
        _unloadingChunks = new NativeList<Entity>(
            16, Allocator.TempJob
        );
        _maxConcurrentLoads = 8;
        _streamingDistance = 96.0f; // 96 units
    }
    
    protected override void OnDestroy()
    {
        _streamingRequests.Dispose();
        _loadingChunks.Dispose();
        _unloadingChunks.Dispose();
    }
    
    protected override void OnUpdate()
    {
        // Update chunk streaming
        UpdateChunkStreaming();
    }
    
    private void UpdateChunkStreaming()
    {
        // Get player position
        float3 playerPosition = GetSingleton<PlayerPosition>().Value;
        
        // Generate streaming requests
        GenerateStreamingRequests(playerPosition);
        
        // Process streaming requests
        ProcessStreamingRequests();
        
        // Process loading chunks
        ProcessLoadingChunks();
        
        // Process unloading chunks
        ProcessUnloadingChunks();
    }
    
    private void GenerateStreamingRequests(float3 playerPosition)
    {
        // Find chunks that need to be loaded/unloaded
        var chunkQuery = GetEntityQuery(
            typeof(VoxelChunk),
            typeof(ChunkState)
        );
        
        var chunks = chunkQuery.ToEntityArray(Allocator.TempJob);
        
        for (int i = 0; i < chunks.Length; i++)
        {
            Entity chunkEntity = chunks[i];
            
            // Get chunk position
            float3 chunkPosition = GetComponentData<VoxelChunk>(
                chunkEntity
            ).Position;
            
            float distance = math.distance(playerPosition, chunkPosition);
            
            // Check if chunk needs loading
            if (distance < _streamingDistance)
            {
                var chunkState = GetComponentData<ChunkState>(chunkEntity);
                
                if (chunkState.State == ChunkState.Unloaded)
                {
                    // Add streaming request
                    _streamingRequests.Add(new StreamingRequest
                    {
                        ChunkEntity = chunkEntity,
                        Position = chunkPosition,
                        Priority = CalculatePriority(distance),
                        IsCompleted = false
                    });
                }
            }
            else
            {
                // Check if chunk needs unloading
                var chunkState = GetComponentData<ChunkState>(chunkEntity);
                
                if (chunkState.State == ChunkState.Loaded)
                {
                    _unloadingChunks.Add(chunkEntity);
                }
            }
        }
        
        chunks.Dispose();
    }
    
    private void ProcessStreamingRequests()
    {
        // Sort requests by priority
        SortStreamingRequests();
        
        // Process requests based on available loading slots
        int availableSlots = _maxConcurrentLoads - _loadingChunks.Length;
        
        for (int i = 0; i < _streamingRequests.Length && availableSlots > 0; i++)
        {
            var request = _streamingRequests[i];
            
            if (!request.IsCompleted)
            {
                // Start loading chunk
                StartLoadingChunk(request.ChunkEntity);
                request.IsCompleted = true;
                _streamingRequests[i] = request;
                
                availableSlots--;
            }
        }
    }
    
    private void SortStreamingRequests()
    {
        // Sort requests by priority (lower distance = higher priority)
        for (int i = 0; i < _streamingRequests.Length - 1; i++)
        {
            for (int j = i + 1; j < _streamingRequests.Length; j++)
            {
                if (_streamingRequests[i].Priority > _streamingRequests[j].Priority)
                {
                    var temp = _streamingRequests[i];
                    _streamingRequests[i] = _streamingRequests[j];
                    _streamingRequests[j] = temp;
                }
            }
        }
    }
    
    private float CalculatePriority(float distance)
    {
        // Calculate priority based on distance
        // Closer chunks have higher priority
        return distance;
    }
    
    private void StartLoadingChunk(Entity chunkEntity)
    {
        // Start loading chunk
        _loadingChunks.Add(chunkEntity);
        
        // Update chunk state
        var chunkState = GetComponentData<ChunkState>(chunkEntity);
        chunkState.State = ChunkState.Loading;
        SetComponentData(chunkEntity, chunkState);
        
        // Schedule loading job
        var loadingJob = new ChunkLoadingJob
        {
            ChunkEntity = chunkEntity
        };
        
        JobHandle handle = loadingJob.Schedule();
        Dependency = JobHandle.CombineDependencies(Dependency, handle);
    }
    
    private void ProcessLoadingChunks()
    {
        // Check if any chunks have finished loading
        for (int i = _loadingChunks.Length - 1; i >= 0; i--)
        {
            Entity chunkEntity = _loadingChunks[i];
            
            // Check if chunk has finished loading
            var chunkState = GetComponentData<ChunkState>(chunkEntity);
            
            if (chunkState.State == ChunkState.Loaded)
            {
                // Remove from loading list
                _loadingChunks.RemoveAt(i);
            }
        }
    }
    
    private void ProcessUnloadingChunks()
    {
        // Process unloading chunks
        for (int i = 0; i < _unloadingChunks.Length; i++)
        {
            Entity chunkEntity = _unloadingChunks[i];
            
            // Start unloading chunk
            var chunkState = GetComponentData<ChunkState>(chunkEntity);
            chunkState.State = ChunkState.Unloading;
            SetComponentData(chunkEntity, chunkState);
            
            // Schedule unloading job
            var unloadingJob = new ChunkUnloadingJob
            {
                ChunkEntity = chunkEntity
            };
            
            JobHandle handle = unloadingJob.Schedule();
            Dependency = JobHandle.CombineDependencies(Dependency, handle);
        }
        
        _unloadingChunks.Clear();
    }
}
```

## Streaming and Loading

### Asynchronous Loading System

```csharp
[BurstCompile]
public struct AsyncChunkLoadingJob : IJob
{
    public Entity ChunkEntity;
    public float3 ChunkPosition;
    public float LoadingProgress;
    
    public void Execute()
    {
        // Load chunk data asynchronously
        LoadChunkData();
    }
    
    private void LoadChunkData()
    {
        // Simulate asynchronous loading
        // In a real implementation, this would involve:
        // - Loading from disk
