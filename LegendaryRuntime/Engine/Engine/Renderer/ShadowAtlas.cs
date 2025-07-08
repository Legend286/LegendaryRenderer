using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
using LegendaryRenderer.Shaders;


namespace LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer
{
    public class ShadowAtlas
    {
        private const int ATLAS_SIZE = 4096; // Large atlas for high quality shadows
        private const int MIN_TILE_SIZE = 64; // Minimum tile size
        private const int MAX_TILE_SIZE = 2048; // Maximum tile size for high priority lights
        
        private int atlasTexture;
        private int atlasFramebuffer;
        private QuadTreeNode rootNode;
        private List<AtlasEntry> allocatedEntries;
        private List<AtlasEntry> previousFrameEntries;
        private bool isDirty = true;
        
        public int AtlasTexture => atlasTexture;
        public int AtlasSize => ATLAS_SIZE;
        public List<AtlasEntry> AllocatedEntries => allocatedEntries;
        
        public ShadowAtlas()
        {
            allocatedEntries = new List<AtlasEntry>();
            previousFrameEntries = new List<AtlasEntry>();
            InitializeAtlas();
        }
        
        private void InitializeAtlas()
        {
            // Create atlas texture
            atlasTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, atlasTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32f, 
                         ATLAS_SIZE, ATLAS_SIZE, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            
            // Create framebuffer
            atlasFramebuffer = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, atlasFramebuffer);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, 
                                   TextureTarget.Texture2D, atlasTexture, 0);
            
            // Check framebuffer completeness
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
            {
                throw new Exception("Shadow atlas framebuffer is not complete!");
            }
            
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            
            // Initialize quad tree
            rootNode = new QuadTreeNode(new Rectangle(0, 0, ATLAS_SIZE, ATLAS_SIZE));
        }
        
        public void UpdateAtlas(Camera camera)
        {
            // Get visible lights that cast shadows
            var visibleLights = GetVisibleShadowCastingLights(camera);
            
                    // Calculate raw priorities for all visible lights
        var rawPriorities = new Dictionary<Light, float>();
        foreach (var light in visibleLights)
        {
            rawPriorities[light] = CalculateLightPriority(light, camera);
        }
        
        // Normalize priorities based on total demand for atlas space
        var lightPriorities = NormalizePriorities(rawPriorities, camera);
        
        // Sort lights by priority (higher priority first)
        var sortedLights = visibleLights.OrderByDescending(l => lightPriorities[l]).ToList();
            
            // Store previous frame's entries
            previousFrameEntries.Clear();
            previousFrameEntries.AddRange(allocatedEntries);
            
            // Check if we need to reallocate
            bool needsReallocation = ShouldReallocateAtlas(sortedLights, lightPriorities, camera);
            
            if (needsReallocation || isDirty)
            {
                PerformIntelligentReallocation(sortedLights, lightPriorities, camera);
            }
            else
            {
                // Update existing allocations with new priorities
                UpdateExistingAllocations(lightPriorities, camera);
            }
            
            isDirty = false;
        }
        
        private List<Light> GetVisibleShadowCastingLights(Camera camera)
        {
            var lights = new List<Light>();
            
            foreach (var light in Engine.GameObjects.OfType<Light>())
            {
                if (!light.EnableShadows || !light.IsVisible) continue;
                
                // Only include point, spot, and projector lights
                if (light.Type != Light.LightType.Point && 
                    light.Type != Light.LightType.Spot && 
                    light.Type != Light.LightType.Projector) continue;
                
                // Check if light is visible to camera
                if (IsLightVisibleToCamera(light, camera))
                {
                    lights.Add(light);
                }
            }
            
            return lights;
        }
        
        private bool IsLightVisibleToCamera(Light light, Camera camera)
        {
            switch (light.Type)
            {
                case Light.LightType.Point:
                    var sphere = new SphereBounds(light.Transform.Position, light.Range);
                    return camera.Frustum.ContainsSphere(sphere);
                
                case Light.LightType.Spot:
                case Light.LightType.Projector:
                    var frustum = new Frustum(light.ViewProjectionMatrix);
                    return camera.Frustum.ContainsFrustum(frustum);
                
                default:
                    return false;
            }
        }
        
        private float CalculateLightPriority(Light light, Camera camera)
        {
            // Calculate priority based on multiple factors
            float distanceToCamera = Vector3.Distance(light.Transform.Position, camera.Transform.Position);
            float intensity = light.Intensity;
            float range = light.Range;
            
            // Normalize intensity and range to reasonable scales
            float normalizedIntensity = intensity / 100.0f; // Typical intensity is ~100
            float normalizedRange = range / 50.0f; // Typical range is ~10-50
            float normalizedDistance = distanceToCamera / 50.0f; // Normalize distance
            
            // Closer, brighter, and larger range lights get higher priority
            // Scale to 0-100 range for more predictable behavior
            float priority = (normalizedIntensity * normalizedRange) / Math.Max(normalizedDistance, 0.1f);
            
            // Clamp to reasonable range (0-100)
            priority = Math.Min(priority * 10.0f, 100.0f);
            
            // Point lights get lower priority since they need 6 tiles
            if (light.Type == Light.LightType.Point)
                priority *= 0.6f;
            // Spot lights get slight priority boost as they typically need more precision
            else if (light.Type == Light.LightType.Spot)
                priority *= 1.2f;
            
            return priority;
        }
        
        private Dictionary<Light, float> NormalizePriorities(Dictionary<Light, float> rawPriorities, Camera camera)
        {
            // Calculate total demand for atlas space
            int totalVisibleFaces = 0;
            foreach (var kvp in rawPriorities)
            {
                var light = kvp.Key;
                if (light.Type == Light.LightType.Point)
                {
                    // Count only visible faces for point lights
                    for (int face = 0; face < 6; face++)
                    {
                        if (camera.Frustum.ContainsFrustum(new Frustum(light.PointLightViewProjections[face])))
                        {
                            totalVisibleFaces++;
                        }
                    }
                }
                else
                {
                    // Spot and projector lights have 1 face each
                    totalVisibleFaces++;
                }
            }
            
            // Calculate atlas capacity (in terms of minimum tile count)
            int atlasCapacity = (ATLAS_SIZE / MIN_TILE_SIZE) * (ATLAS_SIZE / MIN_TILE_SIZE);
            
            // Calculate demand vs capacity ratio
            float demandToCapacityRatio = (float)totalVisibleFaces / atlasCapacity;
            
            // Normalize priorities to better utilize atlas space
            var normalizedPriorities = new Dictionary<Light, float>();
            
            if (rawPriorities.Count == 0)
            {
                return normalizedPriorities;
            }
            
            // Find min and max raw priorities
            float minRawPriority = rawPriorities.Values.Min();
            float maxRawPriority = rawPriorities.Values.Max();
            
            // Calculate normalization parameters based on demand
            float targetMaxPriority;
            float targetMinPriority;
            
            if (demandToCapacityRatio <= 0.1f)
            {
                // Low demand - allow large tiles (high priorities)
                targetMaxPriority = 100.0f;
                targetMinPriority = 60.0f;
            }
            else if (demandToCapacityRatio <= 0.5f)
            {
                // Medium demand - moderate tile sizes
                targetMaxPriority = 80.0f;
                targetMinPriority = 40.0f;
            }
            else
            {
                // High demand - smaller tiles to fit more lights
                targetMaxPriority = 60.0f;
                targetMinPriority = 20.0f;
            }
            
            // Apply normalization
            foreach (var kvp in rawPriorities)
            {
                var light = kvp.Key;
                float rawPriority = kvp.Value;
                
                // Normalize to target range
                float normalizedPriority;
                if (Math.Abs(maxRawPriority - minRawPriority) < 0.001f)
                {
                    // All priorities are the same
                    normalizedPriority = (targetMaxPriority + targetMinPriority) / 2.0f;
                }
                else
                {
                    // Linear interpolation to target range
                    float normalizedRatio = (rawPriority - minRawPriority) / (maxRawPriority - minRawPriority);
                    normalizedPriority = targetMinPriority + normalizedRatio * (targetMaxPriority - targetMinPriority);
                }
                
                normalizedPriorities[light] = normalizedPriority;
            }
            
            return normalizedPriorities;
        }
        
        private void AllocateLightTiles(Light light, Camera camera)
        {
            float priority = CalculateLightPriority(light, camera);
            AllocateLightTiles(light, camera, priority);
        }
        
        private void AllocateLightTiles(Light light, Camera camera, float priority)
        {
            int desiredTileSize = CalculateDesiredTileSize(priority);
            
            switch (light.Type)
            {
                case Light.LightType.Point:
                    AllocatePointLightTiles(light, camera, priority, desiredTileSize);
                    break;
                
                case Light.LightType.Spot:
                case Light.LightType.Projector:
                    AllocateSpotLightTile(light, priority, desiredTileSize);
                    break;
            }
        }
        
        private void AllocatePointLightTiles(Light light, Camera camera, float priority, int desiredTileSize)
        {
            AllocatePointLightTilesInternal(light, camera, priority, desiredTileSize, false);
        }
        
        private void AllocatePointLightTilesInternal(Light light, Camera camera, float priority, int desiredTileSize, bool afterEviction)
        {
            // Find all visible faces for this point light
            var visibleFaces = new List<int>();
            for (int face = 0; face < 6; face++)
            {
                if (camera.Frustum.ContainsFrustum(new Frustum(light.PointLightViewProjections[face])))
                {
                    visibleFaces.Add(face);
                }
            }
            
            if (visibleFaces.Count == 0) return;
            
            // Try to allocate all visible faces, starting with desired tile size and going smaller if needed
            int currentTileSize = desiredTileSize;
            while (currentTileSize >= MIN_TILE_SIZE)
            {
                var tempTiles = new List<(int face, QuadTreeNode tile)>();
                bool allFacesAllocated = true;
                
                // Try to allocate all visible faces at current tile size
                foreach (int face in visibleFaces)
                {
                    var tile = rootNode.Allocate(currentTileSize, currentTileSize);
                    if (tile != null)
                    {
                        tempTiles.Add((face, tile));
                    }
                    else
                    {
                        allFacesAllocated = false;
                        break;
                    }
                }
                
                if (allFacesAllocated)
                {
                    // Successfully allocated all faces - commit the allocations
                    foreach (var (face, tile) in tempTiles)
                    {
                        allocatedEntries.Add(new AtlasEntry
                        {
                            Light = light,
                            Face = face,
                            Tile = tile,
                            Priority = priority
                        });
                    }
                    return;
                }
                else
                {
                    // Failed to allocate all faces - free the ones we did allocate and try smaller tiles
                    foreach (var (face, tile) in tempTiles)
                    {
                        tile.Clear(); // Free the tile
                    }
                    
                    // Try smaller tile size (half the current size)
                    currentTileSize = Math.Max(MIN_TILE_SIZE, currentTileSize / 2);
                }
            }
            
            // If we get here, we couldn't allocate even at minimum tile size
            // Try to evict lower priority lights to make room (but only if we haven't already tried eviction)
            if (!afterEviction && TryEvictLowerPriorityLights(light, priority, visibleFaces.Count))
            {
                // Try allocation again after eviction with minimum tile size
                AllocatePointLightTilesInternal(light, camera, priority, MIN_TILE_SIZE, true);
            }
        }
        
        private void AllocateSpotLightTile(Light light, float priority, int desiredTileSize)
        {
            AllocateSpotLightTileInternal(light, priority, desiredTileSize, false);
        }
        
        private void AllocateSpotLightTileInternal(Light light, float priority, int desiredTileSize, bool afterEviction)
        {
            // Try to allocate at desired size first, then go smaller if needed
            int currentTileSize = desiredTileSize;
            while (currentTileSize >= MIN_TILE_SIZE)
            {
                var tile = rootNode.Allocate(currentTileSize, currentTileSize);
                if (tile != null)
                {
                    allocatedEntries.Add(new AtlasEntry
                    {
                        Light = light,
                        Face = 0,
                        Tile = tile,
                        Priority = priority
                    });
                    return;
                }
                
                // Try smaller tile size
                currentTileSize = Math.Max(MIN_TILE_SIZE, currentTileSize / 2);
            }
            
            // If we get here, we couldn't allocate even at minimum tile size
            // Try to evict lower priority lights to make room (but only if we haven't already tried eviction)
            if (!afterEviction && TryEvictLowerPriorityLights(light, priority, 1))
            {
                // Try allocation again after eviction with minimum tile size
                AllocateSpotLightTileInternal(light, priority, MIN_TILE_SIZE, true);
            }
        }
        
        private bool TryEvictLowerPriorityLights(Light newLight, float newPriority, int tilesNeeded)
        {
            // Find all lights with lower priority than the new light
            var lowerPriorityEntries = allocatedEntries
                .Where(e => e.Priority < newPriority)
                .OrderBy(e => e.Priority) // Evict lowest priority first
                .ToList();
            
            if (lowerPriorityEntries.Count == 0) return false;
            
            // Calculate how many tiles we need to free
            int tilesFreed = 0;
            var entriesToEvict = new List<AtlasEntry>();
            
            foreach (var entry in lowerPriorityEntries)
            {
                // For point lights, we need to evict all faces together
                if (entry.Light.Type == Light.LightType.Point)
                {
                    var pointLightEntries = allocatedEntries
                        .Where(e => e.Light == entry.Light)
                        .ToList();
                    
                    // Add all faces of this point light to eviction list
                    foreach (var pointEntry in pointLightEntries)
                    {
                        if (!entriesToEvict.Contains(pointEntry))
                        {
                            entriesToEvict.Add(pointEntry);
                            tilesFreed++;
                        }
                    }
                }
                else
                {
                    entriesToEvict.Add(entry);
                    tilesFreed++;
                }
                
                // Stop if we've freed enough tiles
                if (tilesFreed >= tilesNeeded) break;
            }
            
            // If we can't free enough tiles, don't evict anything
            if (tilesFreed < tilesNeeded) return false;
            
            // Evict the selected entries
            foreach (var entry in entriesToEvict)
            {
                entry.Tile.Clear();
                allocatedEntries.Remove(entry);
            }
            
            return true;
        }
        
        private int CalculateDesiredTileSize(float priority)
        {
            // Map priority to tile size
            float normalizedPriority = Math.Min(priority / 100.0f, 1.0f); // Normalize to 0-1 range
            int tileSize = (int)(MIN_TILE_SIZE + (MAX_TILE_SIZE - MIN_TILE_SIZE) * normalizedPriority);
            
            // Ensure power of 2 for efficient atlas packing
            return NextPowerOfTwo(Math.Max(MIN_TILE_SIZE, tileSize));
        }
        
        private int NextPowerOfTwo(int value)
        {
            if (value <= 0) return 1;
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }
        
        public void BindAtlasFramebuffer()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, atlasFramebuffer);
            
            // Debug: Check if framebuffer is complete
            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                Console.WriteLine($"Atlas framebuffer error: {status}");
            }
        }
        
        public void RenderShadowsToAtlas()
        {
            BindAtlasFramebuffer();
            
            // Set depth clear value to 1.0 (far plane) for shadow maps
            GL.ClearDepth(1.0);
            GL.DepthMask(true); // Make sure depth writes are enabled
            GL.Clear(ClearBufferMask.DepthBufferBit);
            
            Console.WriteLine($"Atlas has {allocatedEntries.Count} entries to render");
            
            foreach (var entry in allocatedEntries)
            {
                RenderLightShadowToTile(entry);
            }
            
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }
        
        private void RenderLightShadowToTile(AtlasEntry entry)
        {
            var tile = entry.Tile;
            var light = entry.Light;
            
            Console.WriteLine($"Rendering {light.Name} {light.Type} face {entry.Face} to tile ({tile.Bounds.X}, {tile.Bounds.Y}, {tile.Bounds.Width}, {tile.Bounds.Height})");
            
            // Set viewport to tile region
            GL.Viewport(tile.Bounds.X, tile.Bounds.Y, tile.Bounds.Width, tile.Bounds.Height);
            
            // Get appropriate view-projection matrix
            Matrix4 viewProj = light.Type == Light.LightType.Point 
                ? light.PointLightViewProjections[entry.Face] 
                : light.ViewProjectionMatrix;
            
            // Render shadow casters
            RenderShadowCasters(light, viewProj);
        }
        
        private void RenderShadowCasters(Light light, Matrix4 viewProjection)
        {
            ShaderManager.LoadShader("shadowgen", out var shader);
            shader.UseShader();
            
            int objectsRendered = 0;
            
            // Cull objects for this light
            if (light.Type == Light.LightType.Point)
            {
                var renderables = Engine.CullSceneByPointLight(light);
                Console.WriteLine($"  Point light has {renderables.Count} culled renderables");
                
                foreach (var renderable in renderables)
                {
                    shader.SetShaderMatrix4x4("shadowViewProjection", viewProjection);
                    shader.SetShaderMatrix4x4("model", renderable.Transform.GetWorldMatrix());
                    renderable.Render(GameObject.RenderMode.ShadowPass);
                    objectsRendered++;
                }
            }
            else
            {
                var renderables = Engine.CullRenderables(viewProjection, true);
                Console.WriteLine($"  Spot/Projector light has {renderables.Count()} total culled renderables");
                
                foreach (var renderable in renderables)
                {
                    if (renderable is Camera || renderable is Light) continue;
                    
                    shader.SetShaderMatrix4x4("shadowViewProjection", viewProjection);
                    shader.SetShaderMatrix4x4("model", renderable.Transform.GetWorldMatrix());
                    renderable.Render(GameObject.RenderMode.ShadowPass);
                    objectsRendered++;
                }
            }
            
            Console.WriteLine($"  Rendered {objectsRendered} objects to atlas tile");
        }
        
        public AtlasEntry? GetAtlasEntry(Light light, int face = 0)
        {
            return allocatedEntries.FirstOrDefault(e => e.Light == light && e.Face == face);
        }
        
        private bool ShouldReallocateAtlas(List<Light> currentLights, Dictionary<Light, float> lightPriorities, Camera camera)
        {
            // Check if any new high-priority lights appeared
            var currentLightSet = new HashSet<Light>(currentLights);
            var previousLightSet = new HashSet<Light>(previousFrameEntries.Select(e => e.Light));
            
            // New lights appeared
            var newLights = currentLightSet.Except(previousLightSet).ToList();
            if (newLights.Any())
            {
                // Check if any new light has higher priority than existing allocations
                float highestExistingPriority = previousFrameEntries.Any() ? previousFrameEntries.Max(e => e.Priority) : 0;
                if (newLights.Any(light => lightPriorities[light] > highestExistingPriority))
                {
                    return true; // High priority light needs space
                }
            }
            
            // Lights disappeared
            var removedLights = previousLightSet.Except(currentLightSet).ToList();
            if (removedLights.Any())
            {
                return true; // Need to free up space
            }
            
            // Check if any existing light's priority changed significantly
            foreach (var entry in previousFrameEntries)
            {
                if (lightPriorities.ContainsKey(entry.Light))
                {
                    float priorityChange = Math.Abs(lightPriorities[entry.Light] - entry.Priority);
                    if (priorityChange > entry.Priority * 0.2f) // 20% change threshold
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        private void PerformIntelligentReallocation(List<Light> sortedLights, Dictionary<Light, float> lightPriorities, Camera camera)
        {
            // Clear the atlas
            allocatedEntries.Clear();
            rootNode.Clear();
            
            // Try to preserve existing allocations for lights that still exist
            var preservedEntries = new List<AtlasEntry>();
            var lightsToReallocate = new List<Light>();
            
            foreach (var light in sortedLights)
            {
                var existingEntry = previousFrameEntries.FirstOrDefault(e => e.Light == light);
                if (existingEntry != null)
                {
                    float newPriority = lightPriorities[light];
                    int newDesiredTileSize = CalculateDesiredTileSize(newPriority);
                    
                    // If the tile size is still appropriate, try to preserve it
                    if (existingEntry.Tile.Bounds.Width == newDesiredTileSize && 
                        existingEntry.Tile.Bounds.Height == newDesiredTileSize)
                    {
                        // Try to allocate at the same location
                        var preservedTile = rootNode.Allocate(newDesiredTileSize, newDesiredTileSize);
                        if (preservedTile != null && 
                            preservedTile.Bounds.X == existingEntry.Tile.Bounds.X && 
                            preservedTile.Bounds.Y == existingEntry.Tile.Bounds.Y)
                        {
                            // Successfully preserved allocation
                            var newEntry = new AtlasEntry
                            {
                                Light = light,
                                Face = existingEntry.Face,
                                Tile = preservedTile,
                                Priority = newPriority
                            };
                            preservedEntries.Add(newEntry);
                            continue;
                        }
                    }
                }
                
                // Need to reallocate this light
                lightsToReallocate.Add(light);
            }
            
            // Add preserved entries
            allocatedEntries.AddRange(preservedEntries);
            
            // Allocate remaining lights
            foreach (var light in lightsToReallocate)
            {
                AllocateLightTiles(light, camera, lightPriorities[light]);
            }
        }
        
        private void UpdateExistingAllocations(Dictionary<Light, float> lightPriorities, Camera camera)
        {
            // Update priorities of existing allocations
            foreach (var entry in allocatedEntries)
            {
                if (lightPriorities.ContainsKey(entry.Light))
                {
                    entry.Priority = lightPriorities[entry.Light];
                }
            }
            
            // Remove allocations for lights that are no longer visible
            allocatedEntries.RemoveAll(entry => !lightPriorities.ContainsKey(entry.Light));
        }

        public void MarkDirty()
        {
            isDirty = true;
        }
        
        public void Dispose()
        {
            GL.DeleteTexture(atlasTexture);
            GL.DeleteFramebuffer(atlasFramebuffer);
        }
    }
    
    public class AtlasEntry
    {
        public Light Light { get; set; }
        public int Face { get; set; } // 0-5 for point lights, 0 for spot/projector
        public QuadTreeNode Tile { get; set; }
        public float Priority { get; set; }
        
        public Vector4 GetAtlasTransform()
        {
            const float atlasSize = 4096f; // Should match ATLAS_SIZE
            return new Vector4(
                (float)Tile.Bounds.X / atlasSize,      // offset X
                (float)Tile.Bounds.Y / atlasSize,      // offset Y
                (float)Tile.Bounds.Width / atlasSize,  // scale X
                (float)Tile.Bounds.Height / atlasSize  // scale Y
            );
        }
    }
    
    public class QuadTreeNode
    {
        public Rectangle Bounds { get; private set; }
        public bool IsLeaf => Children == null;
        public bool IsOccupied { get; private set; }
        public QuadTreeNode[] Children { get; private set; }
        
        public QuadTreeNode(Rectangle bounds)
        {
            Bounds = bounds;
            IsOccupied = false;
        }
        
        public QuadTreeNode? Allocate(int width, int height)
        {
            if (!IsLeaf)
            {
                // Try to allocate in children
                foreach (var child in Children)
                {
                    var result = child.Allocate(width, height);
                    if (result != null) return result;
                }
                return null;
            }
            
            if (IsOccupied) return null;
            
            // Check if requested size fits
            if (width > Bounds.Width || height > Bounds.Height)
                return null;
            
            // Perfect fit
            if (width == Bounds.Width && height == Bounds.Height)
            {
                IsOccupied = true;
                return this;
            }
            
            // Split node
            Split();
            
            // Try to allocate in children
            foreach (var child in Children)
            {
                var result = child.Allocate(width, height);
                if (result != null) return result;
            }
            
            return null;
        }
        
        private void Split()
        {
            int halfWidth = Bounds.Width / 2;
            int halfHeight = Bounds.Height / 2;
            
            Children = new QuadTreeNode[4];
            Children[0] = new QuadTreeNode(new Rectangle(Bounds.X, Bounds.Y, halfWidth, halfHeight));
            Children[1] = new QuadTreeNode(new Rectangle(Bounds.X + halfWidth, Bounds.Y, halfWidth, halfHeight));
            Children[2] = new QuadTreeNode(new Rectangle(Bounds.X, Bounds.Y + halfHeight, halfWidth, halfHeight));
            Children[3] = new QuadTreeNode(new Rectangle(Bounds.X + halfWidth, Bounds.Y + halfHeight, halfWidth, halfHeight));
        }
        
        public void Clear()
        {
            IsOccupied = false;
            Children = null;
        }
    }
    
    public struct Rectangle
    {
        public int X, Y, Width, Height;
        
        public Rectangle(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
} 