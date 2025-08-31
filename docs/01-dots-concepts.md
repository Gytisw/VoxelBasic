# Unity DOTS Core Concepts

This document covers the fundamental concepts of Unity's Data-Oriented Technology Stack (DOTS) that are essential for voxel engine development.

## Table of Contents

- [Entity Component System (ECS)](#entity-component-system-ecs)
- [DOTS Packages Overview](#dots-packages-overview)
- [Core ECS Components](#core-ecs-components)
- [Data-Oriented Design Principles](#data-oriented-design-principles)
- [Memory Architecture](#memory-architecture)
- [Key Differences from Traditional Unity](#key-differences-from-traditional-unity)

## Entity Component System (ECS)

### What is ECS?

ECS is a software architecture pattern that separates data and behavior into three distinct concepts:

1. **Entities**: Generic containers that represent objects in your game
2. **Components**: Pure data containers that define entity properties
3. **Systems**: Logic that operates on entities with specific component combinations

### ECS vs Traditional OOP

| Traditional OOP | ECS |
|----------------|-----|
| Objects contain data + behavior | Entities contain only data |
| Inheritance hierarchies | Component composition |
| Cache-unfriendly memory layout | Cache-friendly data layout |
| Single-threaded processing | Multi-threaded by design |

## DOTS Packages Overview

### Core Packages

#### Entities Package
- **Purpose**: Core ECS implementation
- **Key Features**:
  - Entity management and creation/destruction
  - Component system architecture
  - World management
  - Archetype system for efficient memory layout

#### Collections Package
- **Purpose**: Low-level collection types optimized for performance
- **Key Types**:
  - `NativeArray<T>`: Fixed-size arrays allocated in unmanaged memory
  - `NativeList<T>`: Dynamic arrays with capacity management
  - `NativeHashMap<TKey, TValue>`: Hash maps for fast lookups
  - `BlobAsset`: Immutable data assets for read-only data

#### Mathematics Package
- **Purpose**: Math types optimized for DOTS
- **Key Types**:
  - `float3`, `int3`, `quaternion`: Vector and quaternion types
  - `Matrix4x4`: Matrix operations
  - `Random`: Random number generation
  - `Math`: Mathematical utility functions

### Rendering Packages

#### Entities.Graphics
- **Purpose**: Rendering system for DOTS
- **Features**:
  - Render mesh components
  - Material management
  - Shader integration
  - GPU-driven rendering

#### Hybrid Renderer
- **Purpose**: Bridge between GameObjects and Entities for rendering
- **Use Case**: Gradual migration from GameObjects to Entities

### Physics Packages

#### Unity.Physics
- **Purpose**: Physics simulation for DOTS
- **Features**:
  - Collision detection
  - Rigid body dynamics
  - Ray casting
  - Character controllers

## Core ECS Components

### Entity

```csharp
// Entity is a lightweight identifier
Entity entity = EntityManager.CreateEntity();
```

**Key Properties**:
- 16-bit index + 16-bit version
- Very small memory footprint
- Can be created/destroyed efficiently

### Components

```csharp
// Components are data-only structures
public struct Position : IComponentData
{
    public float3 Value;
}

public struct Rotation : IComponentData
{
    public quaternion Value;
}

public struct Scale : IComponentData
{
    public float3 Value;
}
```

**Component Types**:
- **IComponentData**: Standard component data
- **ISharedComponentData**: Data shared across multiple entities
- **IEnableableComponent**: Components that can be enabled/disabled
- **IBufferElementData**: Elements in dynamic buffers

### Systems

```csharp
// Systems process entities with specific components
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
public class MovementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Get all entities with Position and Velocity components
        Entities.ForEach((ref Position position, in Velocity velocity) =>
        {
            position.Value += velocity.Value * DeltaTime;
        }).ScheduleParallel();
    }
}
```

**System Types**:
- **ComponentSystemBase**: Base class for custom systems
- **ISystem**: Interface for systems
- **ScriptBehaviourManager**: For systems requiring Unity lifecycle

## Data-Oriented Design Principles

### Memory Layout

**Traditional Approach (Cache-Unfriendly)**:
```
Object 1: [Data1][Data2][Data3][Method1][Method2]
Object 2: [Data1][Data2][Data3][Method1][Method2]
Object 3: [Data1][Data2][Data3][Method1][Method2]
```

**ECS Approach (Cache-Friendly)**:
```
Data1: [Object1][Object2][Object3]
Data2: [Object1][Object2][Object3]
Data3: [Object1][Object2][Object3]
Methods: [ProcessAllObjects]
```

### Benefits of Data-Oriented Design

1. **Cache Locality**: Related data stored contiguously in memory
2. **Parallel Processing**: Easy to process data in parallel
3. **Memory Efficiency**: Reduced memory overhead
4. **Predictable Performance**: Consistent frame times

### Archetypes

**What are Archetypes?**
- Archetypes define unique combinations of components
- Each archetype has its own memory layout
- Entities with the same component combination share the same archetype

**Archetype Benefits**:
- Efficient memory allocation
- Fast entity iteration
- Better cache utilization

```csharp
// Archetype example
var archetype = EntityManager.CreateArchetype(
    typeof(Position),
    typeof(Rotation),
    typeof(VoxelData)
);

var entity = EntityManager.CreateEntity(archetype);
```

## Memory Architecture

### ECS Chunks

- **Size**: 16KB per chunk
- **Structure**: Contiguous memory block for entities of the same archetype
- **Capacity**: Typically 64-128 entities per chunk
- **Benefits**: Cache-friendly memory access

### Memory Management

```csharp
// Native arrays for unmanaged memory
NativeArray<VoxelData> voxelData = new NativeArray<VoxelData>(
    chunkSize * chunkSize * chunkSize,
    Allocator.Persistent
);

// Clean up when done
voxelData.Dispose();
```

**Memory Allocators**:
- **Temporary**: Short-lived allocations (frame-based)
- **Persistent**: Long-lived allocations (manual cleanup required)
- **Job**: For job-specific allocations

### Safety Checks

```csharp
// Safety handles for safe access
var safetyHandle = new NativeArray<VoxelData>(..., Allocator.TempJob).GetSafetyHandle();

// Schedule job with safety handle
JobHandle jobHandle = new MyJob
{
    VoxelData = voxelData
}.ScheduleParallel(safetyHandle);
```

## Key Differences from Traditional Unity

### Component Access

**Traditional Unity**:
```csharp
// Direct component access
Transform transform = GetComponent<Transform>();
transform.position = newPosition;
```

**DOTS ECS**:
```csharp
// Component data access
Entities.ForEach((ref Position position) =>
{
    position.Value = newPosition;
}).ScheduleParallel();
```

### Performance Characteristics

| Aspect | Traditional Unity | DOTS ECS |
|--------|------------------|----------|
| CPU Performance | Single-threaded | Multi-threaded |
| Memory Usage | Higher overhead | Lower overhead |
| Cache Efficiency | Poor | Excellent |
| Development Speed | Faster prototyping | Requires more planning |
| Scalability | Limited | Highly scalable |

### Migration Path

1. **Hybrid Approach**: Use both GameObjects and Entities
2. **Gradual Migration**: Convert systems one by one
3. **Full Migration**: Complete transition to ECS

### When to Use DOTS

**Good Candidates**:
- Large numbers of similar entities (1000+)
- CPU-bound performance issues
- Complex simulations
- Multiplayer games

**Not Ideal For**:
- Simple games with few entities
- UI-heavy applications
- Projects with tight deadlines
- Games requiring specific Unity APIs

## Next Steps

After understanding these core concepts, proceed to:
- [02-architecture-patterns.md](02-architecture-patterns.md) - Voxel engine architecture patterns
- [03-performance-optimization.md](03-performance-optimization.md) - Performance optimization techniques