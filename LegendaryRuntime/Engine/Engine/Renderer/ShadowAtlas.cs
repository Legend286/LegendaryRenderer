using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer;
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
        private float lastUpdateTime = 0.0f;
        private const float UPDATE_COOLDOWN = 0.1f; // Minimum time between updates in seconds
        private readonly object updateLock = new object();
        private readonly Dictionary<Light, float> lastLightUpdateTimes = new Dictionary<Light, float>();
        private const float LIGHT_UPDATE_COOLDOWN = 0.05f; // Minimum time between updates for individual lights
        
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
            try
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
                var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
                if (status != FramebufferErrorCode.FramebufferComplete)
                {
                    throw new Exception($"Shadow atlas framebuffer is not complete: {status}");
                }
                
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                
                // Initialize quad tree
                rootNode = new QuadTreeNode(new Rectangle(0, 0, ATLAS_SIZE, ATLAS_SIZE));
                
                Console.WriteLine($"Shadow Atlas initialized: {ATLAS_SIZE}x{ATLAS_SIZE}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing shadow atlas: {ex.Message}");
                
                // Clean up on failure
                if (atlasTexture != 0)
                {
                    GL.DeleteTexture(atlasTexture);
                    atlasTexture = 0;
                }
                if (atlasFramebuffer != 0)
                {
                    GL.DeleteFramebuffer(atlasFramebuffer);
                    atlasFramebuffer = 0;
                }
                throw;
            }
        }
        
        public void UpdateAtlas(Camera camera)
        {
            lock (updateLock)
            {
                try
                {
                // Ensure atlas is initialized
                if (atlasTexture == 0)
                {
                    InitializeAtlas();
                }
                
                if (camera == null)
                {
                    Console.WriteLine("Warning: Camera is null in UpdateAtlas");
                    return;
                }
                
                // Check cooldown to prevent too frequent updates
                float currentTime = (float)DateTime.Now.TimeOfDay.TotalSeconds;
                if (currentTime - lastUpdateTime < UPDATE_COOLDOWN && !isDirty)
                {
                    return; // Skip update if too soon and not dirty
                }
                lastUpdateTime = currentTime;
                
                // Get visible lights that cast shadows
                var visibleLights = GetVisibleShadowCastingLights(camera);
                
                // Calculate raw priorities for all visible lights
                var rawPriorities = new Dictionary<Light, float>();
                foreach (var light in visibleLights)
                {
                    if (light != null)
                    {
                        rawPriorities[light] = CalculateLightPriority(light, camera);
                    }
                }
                
                // Normalize priorities based on total demand for atlas space
                var lightPriorities = NormalizePriorities(rawPriorities, camera);
                
                // Sort lights by priority (higher priority first)
                var sortedLights = visibleLights.Where(l => l != null && lightPriorities.ContainsKey(l)).OrderByDescending(l => lightPriorities[l]).ToList();
                
                // Store previous frame's entries
                previousFrameEntries.Clear();
                previousFrameEntries.AddRange(allocatedEntries);
                
                // Check if we need to reallocate
                bool needsReallocation = ShouldReallocateAtlas(sortedLights, lightPriorities, camera);
                
                // For now, always reallocate to ensure all lights get proper allocation
                // This can be optimized later once the allocation logic is stable
                if (needsReallocation || isDirty || true) // Force reallocation for debugging
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
                            catch (Exception ex)
                {
                    Console.WriteLine($"Error updating shadow atlas: {ex.Message}");
                    // Mark as dirty to try again next frame
                    isDirty = true;
                }
            }
        }
        
        private List<Light> GetVisibleShadowCastingLights(Camera camera)
        {
            var lights = new List<Light>();
            
            if (camera == null) return lights;
            
            try
            {
                foreach (var gameObject in Engine.GameObjects.ToList()) // Create a copy to avoid modification during iteration
                {
                    if (gameObject is Light light)
                    {
                        // Null checks for safety
                        if (light == null || light.Transform == null) continue;
                        
                        if (!light.EnableShadows || !light.IsVisible) continue;
                        
                        // Check if light properties are valid
                        if (light.Range <= 0 || light.Intensity <= 0 || 
                            float.IsNaN(light.Range) || float.IsNaN(light.Intensity) ||
                            float.IsInfinity(light.Range) || float.IsInfinity(light.Intensity))
                        {
                            Console.WriteLine($"Warning: Skipping light {light.Name} with invalid properties (Range: {light.Range}, Intensity: {light.Intensity})");
                            continue;
                        }
                        
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
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting visible shadow casting lights: {ex.Message}");
            }
            
            return lights;
        }
        
        private bool IsLightVisibleToCamera(Light light, Camera camera)
        {
            try
            {
                // Additional safety checks
                if (light.Range <= 0 || float.IsNaN(light.Range) || float.IsInfinity(light.Range))
                {
                    return false;
                }
                
                switch (light.Type)
                {
                    case Light.LightType.Point:
                        if (light.Transform == null) return false;
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking light visibility for {light.Name}: {ex.Message}");
                return false;
            }
        }
        
        private float CalculateLightPriority(Light light, Camera camera)
        {
            if (light?.Transform == null || camera?.Transform == null) return 0.0f;
            
            try
            {
                // Calculate priority based on multiple factors
                float distanceToCamera = Vector3.Distance(light.Transform.Position, camera.Transform.Position);
                float intensity = light.Intensity;
                float range = light.Range;
                
                // Validate light properties to prevent crashes
                if (intensity <= 0 || range <= 0 || float.IsNaN(intensity) || float.IsNaN(range) || float.IsInfinity(intensity) || float.IsInfinity(range))
                {
                    Console.WriteLine($"Warning: Light {light.Name} has invalid intensity ({intensity}) or range ({range})");
                    return 0.0f;
                }
                
                if (float.IsNaN(distanceToCamera) || float.IsInfinity(distanceToCamera))
                {
                    Console.WriteLine($"Warning: Light {light.Name} has invalid distance to camera ({distanceToCamera})");
                    return 0.0f;
                }
                
                // Normalize intensity and range to reasonable scales
                float normalizedIntensity = intensity / 100.0f; // Typical intensity is ~100
                float normalizedRange = range / 50.0f; // Typical range is ~10-50
                float normalizedDistance = distanceToCamera / 50.0f; // Normalize distance
                
                // Closer, brighter, and larger range lights get higher priority
                // Scale to 0-100 range for more predictable behavior
                float priority = (normalizedIntensity * normalizedRange) / Math.Max(normalizedDistance, 0.1f);
                
                // Clamp to reasonable range (0-100)
                priority = Math.Min(priority * 10.0f, 100.0f);
                
                // Validate final priority
                if (float.IsNaN(priority) || float.IsInfinity(priority))
                {
                    Console.WriteLine($"Warning: Calculated invalid priority for light {light.Name}");
                    return 0.0f;
                }
                
                // Point lights get lower priority since they need 6 tiles
                if (light.Type == Light.LightType.Point)
                    priority *= 0.6f;
                // Spot lights get slight priority boost as they typically need more precision
                else if (light.Type == Light.LightType.Spot)
                    priority *= 1.2f;
                
                return priority;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating light priority for {light.Name}: {ex.Message}");
                return 0.0f;
            }
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
                    try
                    {
                        var pointViewProjections = light.PointLightViewProjections;
                        if (pointViewProjections != null && pointViewProjections.Length >= 6)
                        {
                            for (int face = 0; face < 6; face++)
                            {
                                try
                                {
                                    if (camera.Frustum.ContainsFrustum(new Frustum(pointViewProjections[face])))
                                    {
                                        totalVisibleFaces++;
                                    }
                                }
                                catch (Exception innerEx)
                                {
                                    Console.WriteLine($"Error checking frustum for face {face} of light {light.Name}: {innerEx.Message}");
                                    // Count this face as visible to be safe
                                    totalVisibleFaces++;
                                }
                            }
                        }
                        else
                        {
                            // Fallback: assume all 6 faces are visible
                            totalVisibleFaces += 6;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error calculating point light view projections for {light.Name}: {ex.Message}");
                        // Fallback: assume all 6 faces are visible
                        totalVisibleFaces += 6;
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
        
        private void AllocateLightTilesGuaranteed(Light light, Camera camera, float priority)
        {
            // This method guarantees allocation by using minimum tile size and aggressive eviction
            switch (light.Type)
            {
                case Light.LightType.Point:
                    AllocatePointLightTilesGuaranteed(light, camera, priority);
                    break;
                
                case Light.LightType.Spot:
                case Light.LightType.Projector:
                    AllocateSpotLightTileGuaranteed(light, priority);
                    break;
            }
        }
        
        private void AllocatePointLightTiles(Light light, Camera camera, float priority, int desiredTileSize)
        {
            AllocatePointLightTilesInternal(light, camera, priority, desiredTileSize, false);
        }
        
        private void AllocatePointLightTilesGuaranteed(Light light, Camera camera, float priority)
        {
            // For guaranteed allocation, allocate all 6 faces regardless of visibility
            // This ensures point lights always get complete shadow coverage
            int tileSize = MIN_TILE_SIZE;
            
            // First, try to evict lower priority lights to make room
            int tilesNeeded = 6; // Always allocate all 6 faces for complete coverage
            if (TryEvictLowerPriorityLights(light, priority, tilesNeeded))
            {
                // Try allocation after eviction
                AllocateAllPointLightFaces(light, priority, tileSize);
            }
            else
            {
                // Force allocation even if it means fragmenting the atlas
                AllocateAllPointLightFaces(light, priority, tileSize);
            }
        }
        
        private void AllocateAllPointLightFaces(Light light, float priority, int tileSize)
        {
            var allocatedFaces = new List<(int face, QuadTreeNode tile)>();
            
            // Try to allocate all 6 faces
            for (int face = 0; face < 6; face++)
            {
                var tile = rootNode.Allocate(tileSize, tileSize);
                if (tile != null)
                {
                    allocatedFaces.Add((face, tile));
                }
                else
                {
                    Console.WriteLine($"Warning: Could not allocate face {face} for point light {light.Name} even at minimum size");
                }
            }
            
            // Commit all successful allocations
            foreach (var (face, tile) in allocatedFaces)
            {
                allocatedEntries.Add(new AtlasEntry
                {
                    Light = light,
                    Face = face,
                    Tile = tile,
                    Priority = priority
                });
            }
            
            Console.WriteLine($"Point light {light.Name}: Allocated {allocatedFaces.Count}/6 faces");
        }
        
        private void AllocatePointLightTilesInternal(Light light, Camera camera, float priority, int desiredTileSize, bool afterEviction)
        {
            // For point lights, allocate all 6 faces for complete shadow coverage
            // This ensures better shadow quality and consistency
            var visibleFaces = new List<int> { 0, 1, 2, 3, 4, 5 };
            
            // Optional: We could still do frustum culling for optimization, but for now
            // let's prioritize correctness and ensure all lights get proper shadows
            try
            {
                var pointViewProjections = light.PointLightViewProjections;
                if (pointViewProjections == null || pointViewProjections.Length < 6)
                {
                    Console.WriteLine($"Warning: Point light {light.Name} has invalid view projections");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating point light view projections for {light.Name}: {ex.Message}");
                return;
            }
            
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
        
        private void AllocateSpotLightTileGuaranteed(Light light, float priority)
        {
            // For guaranteed allocation, use minimum tile size and aggressive eviction
            int tileSize = MIN_TILE_SIZE;
            
            // First, try to evict lower priority lights to make room
            if (TryEvictLowerPriorityLights(light, priority, 1))
            {
                // Try allocation after eviction
                var tile = rootNode.Allocate(tileSize, tileSize);
                if (tile != null)
                {
                    allocatedEntries.Add(new AtlasEntry
                    {
                        Light = light,
                        Face = 0,
                        Tile = tile,
                        Priority = priority
                    });
                    Console.WriteLine($"Spot light {light.Name}: Allocated after eviction");
                    return;
                }
            }
            
            // Force allocation even without eviction
            var forcedTile = rootNode.Allocate(tileSize, tileSize);
            if (forcedTile != null)
            {
                allocatedEntries.Add(new AtlasEntry
                {
                    Light = light,
                    Face = 0,
                    Tile = forcedTile,
                    Priority = priority
                });
                Console.WriteLine($"Spot light {light.Name}: Force allocated");
            }
            else
            {
                Console.WriteLine($"ERROR: Could not allocate spot light {light.Name} even at minimum size!");
            }
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
            if (newLight == null || allocatedEntries == null) return false;
            
            try
            {
                // Create a snapshot of current entries to avoid modification during iteration
                var currentEntries = allocatedEntries.ToList();
                
                // Find all lights with lower priority than the new light
                // Also exclude lights that are currently being processed (to prevent evicting a light while it's being updated)
                var lowerPriorityEntries = currentEntries
                    .Where(e => e?.Light != null && 
                               e.Priority < newPriority && 
                               e.Light != newLight &&
                               !IsLightCurrentlyBeingProcessed(e.Light))
                    .OrderBy(e => e.Priority) // Evict lowest priority first
                    .ToList();
                
                if (lowerPriorityEntries.Count == 0) 
                {
                    Console.WriteLine($"No lights available for eviction for {newLight.Name} (priority: {newPriority})");
                    return false;
                }
                
                // Calculate how many tiles we need to free
                int tilesFreed = 0;
                var entriesToEvict = new List<AtlasEntry>();
                var lightsAlreadyProcessed = new HashSet<Light>();
                
                foreach (var entry in lowerPriorityEntries)
                {
                    if (entry?.Light == null || lightsAlreadyProcessed.Contains(entry.Light))
                        continue;
                    
                    // Double-check that this light is safe to evict
                    if (IsLightCurrentlyBeingProcessed(entry.Light))
                    {
                        Console.WriteLine($"Skipping eviction of {entry.Light.Name} - currently being processed");
                        continue;
                    }
                    
                    // For point lights, we need to evict all faces together
                    if (entry.Light.Type == Light.LightType.Point)
                    {
                        var pointLightEntries = currentEntries
                            .Where(e => e?.Light == entry.Light && e.Light != null)
                            .ToList();
                        
                        // Add all faces of this point light to eviction list
                        foreach (var pointEntry in pointLightEntries)
                        {
                            if (pointEntry != null && !entriesToEvict.Contains(pointEntry))
                            {
                                entriesToEvict.Add(pointEntry);
                                tilesFreed++;
                            }
                        }
                        
                        lightsAlreadyProcessed.Add(entry.Light);
                    }
                    else
                    {
                        entriesToEvict.Add(entry);
                        tilesFreed++;
                        lightsAlreadyProcessed.Add(entry.Light);
                    }
                    
                    // Stop if we've freed enough tiles
                    if (tilesFreed >= tilesNeeded) break;
                }
                
                // If we can't free enough tiles, don't evict anything
                if (tilesFreed < tilesNeeded) 
                {
                    Console.WriteLine($"Insufficient tiles available for eviction for {newLight.Name} (need {tilesNeeded}, can free {tilesFreed})");
                    return false;
                }
                
                // Evict the selected entries
                foreach (var entry in entriesToEvict)
                {
                    if (entry?.Tile != null && entry.Light != null)
                    {
                        try
                        {
                            entry.Tile.Clear();
                            allocatedEntries.Remove(entry);
                            Console.WriteLine($"Evicted light {entry.Light.Name} (priority: {entry.Priority}) for higher priority light {newLight.Name} (priority: {newPriority})");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error evicting entry for light {entry.Light.Name}: {ex.Message}");
                        }
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during light eviction for {newLight.Name}: {ex.Message}");
                return false;
            }
        }
        
        private bool IsLightCurrentlyBeingProcessed(Light light)
        {
            // Simple check - if a light's properties are being modified rapidly, 
            // it's likely being processed by the user interface
            // For now, we'll use a simple time-based check
            return false; // Placeholder - could be enhanced with actual processing tracking
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
            if (atlasTexture == 0 || allocatedEntries == null)
            {
                Console.WriteLine("Warning: Shadow atlas not properly initialized");
                return;
            }
            
            try
            {
                BindAtlasFramebuffer();
                
                // Set depth clear value to 1.0 (far plane) for shadow maps
                GL.ClearDepth(1.0);
                GL.DepthMask(true); // Make sure depth writes are enabled
                GL.Clear(ClearBufferMask.DepthBufferBit);
                
                Console.WriteLine($"Atlas has {allocatedEntries.Count} entries to render");
                
                // Create a copy of the list to avoid modification during iteration
                var entriesToRender = allocatedEntries.ToList();
                
                foreach (var entry in entriesToRender)
                {
                    if (entry?.Light != null && entry.Tile != null)
                    {
                        try
                        {
                            RenderLightShadowToTile(entry);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error rendering shadow for light {entry.Light.Name}: {ex.Message}");
                        }
                    }
                }
                
                // Restore the lighting framebuffer and viewport (matches non-atlas path)
                RenderBufferHelpers.Instance.BindLightingFramebuffer();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rendering shadows to atlas: {ex.Message}");
                // Try to restore the framebuffer even if there was an error
                try
                {
                    RenderBufferHelpers.Instance.BindLightingFramebuffer();
                }
                catch (Exception restoreEx)
                {
                    Console.WriteLine($"Error restoring framebuffer: {restoreEx.Message}");
                }
            }
        }
        
        private void RenderLightShadowToTile(AtlasEntry entry)
        {
            if (entry?.Light == null || entry.Tile == null)
            {
                Console.WriteLine("Warning: Invalid atlas entry");
                return;
            }
            
            var tile = entry.Tile;
            var light = entry.Light;
            
            // Check if light is still valid
            if (light.Transform == null)
            {
                Console.WriteLine($"Warning: Light {light.Name} has null transform");
                return;
            }
            
            Console.WriteLine($"Rendering {light.Name} {light.Type} face {entry.Face} to tile ({tile.Bounds.X}, {tile.Bounds.Y}, {tile.Bounds.Width}, {tile.Bounds.Height})");
            
            // Set viewport to tile region
            GL.Viewport(tile.Bounds.X, tile.Bounds.Y, tile.Bounds.Width, tile.Bounds.Height);
            
            // Get appropriate view-projection matrix
            Matrix4 viewProj;
            if (light.Type == Light.LightType.Point)
            {
                if (light.PointLightViewProjections == null || entry.Face >= light.PointLightViewProjections.Length)
                {
                    Console.WriteLine($"Warning: Invalid point light view projections for {light.Name}");
                    return;
                }
                viewProj = light.PointLightViewProjections[entry.Face];
            }
            else
            {
                viewProj = light.ViewProjectionMatrix;
            }
            
            // Render shadow casters
            RenderShadowCasters(light, viewProj);
        }
        
        private void RenderShadowCasters(Light light, Matrix4 viewProjection)
        {
            if (light?.Transform == null)
            {
                Console.WriteLine("Warning: Light or transform is null in RenderShadowCasters");
                return;
            }
            
            try
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
                        if (renderable?.Transform == null) continue;
                        
                        try
                        {
                            shader.SetShaderMatrix4x4("shadowViewProjection", viewProjection);
                            shader.SetShaderMatrix4x4("model", renderable.Transform.GetWorldMatrix());
                            renderable.Render(GameObject.RenderMode.ShadowPass);
                            objectsRendered++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error rendering shadow caster {renderable.Name}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    var renderables = Engine.CullRenderables(viewProjection, true);
                    Console.WriteLine($"  Spot/Projector light has {renderables.Count()} total culled renderables");
                    
                    foreach (var renderable in renderables)
                    {
                        if (renderable is Camera || renderable is Light || renderable?.Transform == null) continue;
                        
                        try
                        {
                            shader.SetShaderMatrix4x4("shadowViewProjection", viewProjection);
                            shader.SetShaderMatrix4x4("model", renderable.Transform.GetWorldMatrix());
                            renderable.Render(GameObject.RenderMode.ShadowPass);
                            objectsRendered++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error rendering shadow caster {renderable.Name}: {ex.Message}");
                        }
                    }
                }
                
                Console.WriteLine($"  Rendered {objectsRendered} objects to atlas tile");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in RenderShadowCasters for light {light.Name}: {ex.Message}");
            }
        }
        
        public AtlasEntry? GetAtlasEntry(Light light, int face = 0)
        {
            if (light == null || allocatedEntries == null) return null;
            
            try
            {
                return allocatedEntries.FirstOrDefault(e => e?.Light == light && e.Face == face);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting atlas entry for light {light.Name}: {ex.Message}");
                return null;
            }
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
            if (sortedLights == null || lightPriorities == null || camera == null)
            {
                Console.WriteLine("Warning: Null parameters in PerformIntelligentReallocation");
                return;
            }
            
            try
            {
                // Clear the atlas
                allocatedEntries.Clear();
                rootNode.Clear();
                
                Console.WriteLine($"Shadow Atlas: Starting allocation for {sortedLights.Count} lights");
                
                // Phase 1: Try to allocate all lights at their desired sizes
                var unallocatedLights = new List<Light>();
                foreach (var light in sortedLights.ToList()) // Create a copy to avoid modification during iteration
                {
                    if (light == null) continue;
                    
                    if (lightPriorities.ContainsKey(light))
                    {
                        try
                        {
                            int entriesBeforeAllocation = allocatedEntries.Count;
                            
                            // Check if this light already has entries from a previous frame
                            var existingEntries = allocatedEntries.Where(e => e?.Light == light).ToList();
                            bool hadExistingEntries = existingEntries.Any();
                            
                            AllocateLightTiles(light, camera, lightPriorities[light]);
                            
                            // Check if the light was successfully allocated
                            bool lightWasAllocated = allocatedEntries.Count > entriesBeforeAllocation || 
                                                   allocatedEntries.Any(e => e?.Light == light);
                            
                            if (!lightWasAllocated)
                            {
                                unallocatedLights.Add(light);
                                Console.WriteLine($"Light {light.Name} failed initial allocation, will retry at minimum size");
                            }
                            else
                            {
                                Console.WriteLine($"Light {light.Name} allocated successfully (priority: {lightPriorities[light]:F2})");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error allocating light {light.Name}: {ex.Message}");
                            unallocatedLights.Add(light);
                        }
                    }
                }
                
                // Phase 2: Ensure all unallocated lights get at least minimum-sized tiles
                foreach (var light in unallocatedLights.ToList())
                {
                    if (light == null) continue;
                    
                    try
                    {
                        Console.WriteLine($"Retrying allocation for {light.Name} at minimum tile size");
                        if (lightPriorities.ContainsKey(light))
                        {
                            AllocateLightTilesGuaranteed(light, camera, lightPriorities[light]);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in guaranteed allocation for light {light.Name}: {ex.Message}");
                    }
                }
                
                Console.WriteLine($"Shadow Atlas: Final allocation - {allocatedEntries.Count} entries for {sortedLights.Count} lights");
                
                // Debug: Show which lights got allocated
                var allocatedLights = allocatedEntries.Where(e => e?.Light != null).Select(e => e.Light).Distinct().ToList();
                foreach (var light in sortedLights)
                {
                    if (light == null) continue;
                    
                    bool isAllocated = allocatedLights.Contains(light);
                    int entryCount = allocatedEntries.Count(e => e?.Light == light);
                    Console.WriteLine($"  {light.Name} ({light.Type}): {(isAllocated ? $"Allocated ({entryCount} entries)" : "NOT ALLOCATED")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PerformIntelligentReallocation: {ex.Message}");
            }
        }
        
        private void UpdateExistingAllocations(Dictionary<Light, float> lightPriorities, Camera camera)
        {
            if (allocatedEntries == null) return;
            
            // Update priorities of existing allocations
            var entriesToRemove = new List<AtlasEntry>();
            
            foreach (var entry in allocatedEntries.ToList()) // Create a copy to avoid modification during iteration
            {
                if (entry?.Light == null)
                {
                    entriesToRemove.Add(entry);
                    continue;
                }
                
                if (lightPriorities.ContainsKey(entry.Light))
                {
                    entry.Priority = lightPriorities[entry.Light];
                }
                else
                {
                    // Light is no longer visible or valid, mark for removal
                    entriesToRemove.Add(entry);
                }
            }
            
            // Remove invalid or no longer visible lights
            foreach (var entry in entriesToRemove)
            {
                if (entry?.Tile != null)
                {
                    entry.Tile.Clear(); // Free the tile
                }
                allocatedEntries.Remove(entry);
                Console.WriteLine($"Removed atlas entry for light: {entry?.Light?.Name ?? "null"}");
            }
        }

        public void MarkDirty()
        {
            lock (updateLock)
            {
                if (atlasTexture != 0) // Only mark dirty if atlas is initialized
                {
                    isDirty = true;
                }
            }
        }
        
        public void MarkDirtyForLight(Light light)
        {
            if (light == null) return;
            
            lock (updateLock)
            {
                if (atlasTexture != 0) // Only mark dirty if atlas is initialized
                {
                    float currentTime = (float)DateTime.Now.TimeOfDay.TotalSeconds;
                    
                    // Check if this light was updated recently
                    if (lastLightUpdateTimes.TryGetValue(light, out float lastTime))
                    {
                        if (currentTime - lastTime < LIGHT_UPDATE_COOLDOWN)
                        {
                            // Too soon since last update for this light
                            return;
                        }
                    }
                    
                    lastLightUpdateTimes[light] = currentTime;
                    isDirty = true;
                }
            }
        }
        
        public void RemoveLightEntries(Light light)
        {
            if (light == null || allocatedEntries == null) return;
            
            try
            {
                var entriesToRemove = allocatedEntries.Where(e => e?.Light == light).ToList();
                foreach (var entry in entriesToRemove)
                {
                    if (entry?.Tile != null)
                    {
                        entry.Tile.Clear(); // Free the tile
                    }
                    allocatedEntries.Remove(entry);
                }
                
                if (entriesToRemove.Any())
                {
                    Console.WriteLine($"Removed {entriesToRemove.Count} atlas entries for light {light.Name}");
                    MarkDirty(); // Trigger reallocation
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing light entries for {light.Name}: {ex.Message}");
            }
        }
        
        public void Dispose()
        {
            if (atlasTexture != 0)
            {
                GL.DeleteTexture(atlasTexture);
                atlasTexture = 0;
            }
            
            if (atlasFramebuffer != 0)
            {
                GL.DeleteFramebuffer(atlasFramebuffer);
                atlasFramebuffer = 0;
            }
            
            allocatedEntries?.Clear();
            previousFrameEntries?.Clear();
            rootNode?.Clear();
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