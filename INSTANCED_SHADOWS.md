# Instanced Shadow Rendering System

## Overview

The instanced shadow rendering system is a high-performance shadow mapping implementation that dramatically reduces draw calls by rendering multiple shadow maps in a single draw call per unique mesh. Instead of rendering each object individually for each light's shadow map, the system groups objects by mesh and renders all instances of that mesh across all lights simultaneously.

## Performance Benefits

**Traditional Shadow Rendering:**
- 5 lights × 10 objects = 50 draw calls
- Each object rendered separately for each light

**Instanced Shadow Rendering:**
- 10 unique meshes = 10 draw calls
- Each mesh rendered once with 5 instances (one per light)
- **5x performance improvement** in this example

## Key Features

### Shadow Atlas
- Single large texture (4096x4096 by default) containing all shadow maps
- Automatic tile allocation based on number of lights
- Point lights allocate 6 tiles (one per cube face)
- Spot/Directional lights allocate 1 tile each

### OpenGL 4.1 Compliance
- Uses Shader Storage Buffer Objects (SSBO) for instance data
- Maximum 64 instances per draw call (configurable)
- Automatic batching for scenes with many lights

### Advanced Culling
- Frustum culling per light view
- Sphere-bounds intersection testing
- Only visible shadow casters are included in instance data

### Tile-Based Rendering
- Vertex shader applies atlas scale/bias transformation
- Fragment shader clips to tile bounds to prevent bleeding
- PCF (Percentage Closer Filtering) scaled by tile size

## Technical Implementation

### Data Structures

```csharp
public struct ShadowInstanceData
{
    public Matrix4 ModelMatrix;           // Object transform
    public Matrix4 LightViewProjection;   // Light's view-projection matrix
    public Vector4 AtlasScaleOffset;      // Atlas tile UV mapping
    public Vector4 TileBounds;            // Tile bounds for clipping
    public int LightIndex;                // Light identifier
    public int FaceIndex;                 // Cube face for point lights (-1 for others)
}
```

### Shader Pipeline

1. **shadowgen_instanced.vert**: Transforms vertices using instanced matrices and applies atlas mapping
2. **shadowgen_instanced.frag**: Performs alpha testing and tile clipping
3. **DeferredLight.frag**: Updated to sample from shadow atlas when instanced shadows are enabled

### Atlas Layout

The system automatically calculates optimal atlas layout:
- Square grid arrangement (e.g., 3×3 for 9 tiles)
- Tile size = AtlasResolution / TilesPerRow
- UV coordinates mapped to [0,1] range per tile

## Usage

### Enabling Instanced Shadows

```csharp
Engine.UseInstancedShadows = true;  // Enable instanced rendering
```

Or toggle via the editor: **Viewport → Settings → Enable Instanced Shadows**

### Configuration

```csharp
Engine.ShadowAtlasResolution = 4096;        // Atlas texture size
Engine.MAX_SHADOW_INSTANCES_PER_MESH = 64;  // Max instances per draw call
```

### Performance Monitoring

The editor viewport shows real-time statistics:
- Shadow Views: Number of shadow map renders
- Shadow Casters: Total objects casting shadows
- Atlas Resolution: Current atlas size
- Tile Size: Size of each shadow map tile

## Compatibility

### Supported Light Types
- ✅ Spot Lights (1 tile each)
- ✅ Point Lights (6 tiles each)
- ✅ Directional Lights (1 tile each)
- ✅ Projector Lights (1 tile each)

### Material Support
- ✅ Alpha testing for transparent materials
- ✅ Diffuse texture sampling
- ✅ Automatic material property binding

### Fallback Support
The system maintains full backward compatibility:
- `UseInstancedShadows = false` reverts to traditional rendering
- All existing shadow features continue to work
- Seamless switching between modes

## Performance Considerations

### Optimal Scenarios
- Many lights casting shadows from the same objects
- Scenes with repeated geometry (instanced meshes)
- High shadow resolution requirements

### Memory Usage
- Shadow atlas: `AtlasResolution² × 4 bytes` (depth buffer)
- Instance buffer: `64 × sizeof(ShadowInstanceData)` per batch
- Minimal additional memory overhead

### Limitations
- Maximum 64 instances per draw call (OpenGL 4.1 limit)
- Atlas resolution shared among all lights
- Point lights require 6× more atlas space

## Future Enhancements

- [ ] Adaptive atlas resolution based on light count
- [ ] Temporal atlas reuse for static lights
- [ ] Cascaded shadow map instancing
- [ ] Variable resolution per light type
- [ ] GPU-driven culling integration

## Debugging

Enable debug output to monitor performance:
```csharp
// Console output shows efficiency gains
"Instanced Shadows: 50 instances in 10 draw calls (vs 50 individual calls). Efficiency: 5.0x"
```

The system provides comprehensive statistics in the editor viewport for real-time performance monitoring and optimization. 