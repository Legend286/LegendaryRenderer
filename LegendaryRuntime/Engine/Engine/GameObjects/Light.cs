using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MaterialSystem;
using LegendaryRenderer.LegendaryRuntime.Engine.Renderer.MaterialSystem;
using OpenTK.Mathematics;
using static LegendaryRenderer.LegendaryRuntime.Engine.Engine.Engine;
using Environment = LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.Environment;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;

public class Light : GameObject
{
    private static int LightCount = -1;

    public Color4 Colour { get; set; } = Color4.White;
    
    private float range = 10.0f;
    public float Range 
    { 
        get => range;
        set 
        {
            if (Math.Abs(range - value) > 0.001f) // Only notify if there's a significant change
            {
                lock (viewProjectionLock)
                {
                    range = value;
                    // Invalidate cached view projections for all light types
                    cachedPointLightViewProjections = null;
                    cachedViewProjectionMatrix = null;
                }
                
                // Notify shadow atlas about the change with a delay to prevent rapid updates
                if (Engine.UseShadowAtlas && Engine.ShadowAtlas != null && EnableShadows)
                {
                    // Queue the update on the main thread to avoid race conditions
                    Engine.QueueOnMainThread(() =>
                    {
                        try
                        {
                            if (Engine.ShadowAtlas != null)
                            {
                                Engine.ShadowAtlas.MarkDirtyForLight(this);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error updating shadow atlas for range change on light {Name}: {ex.Message}");
                        }
                    });
                }
            }
        }
    }
    
    public static int GetCount => LightCount;

    private bool enableShadows = false;
    public bool EnableShadows 
    { 
        get => enableShadows;
        set 
        {
            if (enableShadows != value)
            {
                enableShadows = value;
                // Notify shadow atlas about the change
                if (Engine.UseShadowAtlas && Engine.ShadowAtlas != null)
                {
                    if (!enableShadows)
                    {
                        // Remove this light from shadow atlas when shadows are disabled
                        Engine.ShadowAtlas.RemoveLightEntries(this);
                    }
                    else
                    {
                        // Mark atlas dirty to trigger reallocation when shadows are enabled
                        Engine.ShadowAtlas.MarkDirty();
                    }
                }
            }
        }
    }

    public bool EnableVolumetrics { get; set; } = false;

    public bool EnableCookie = false;

    private int cascadeCount = 4;
    
    public float CascadeSplitFactor = 0.996f;

    private int cookieTexID = Texture.NullTexture().Reference().GetGLTexture();

    public int CookieTextureID
    {
        get { return cookieTexID; }
        set
        {
            cookieTexID = value;
            EnableCookie = true;
        }
    }

    public int CascadeCount
    {
        get { return cascadeCount; }
        set
        {
            if (value is > 0 and <= 4)
            {
                cascadeCount = value;
            }
        }
    }
    public float InnerCone { get; set; } = 75.0f;

    private float outerCone = 90.0f;
    public float OuterCone 
    { 
        get => outerCone;
        set 
        {
            if (Math.Abs(outerCone - value) > 0.001f)
            {
                lock (viewProjectionLock)
                {
                    outerCone = value;
                    // Invalidate cached view projection for spot lights
                    cachedViewProjectionMatrix = null;
                }
            }
        }
    }

    private float projectorSize = 5.0f;
    public float ProjectorSize 
    { 
        get => projectorSize;
        set 
        {
            if (Math.Abs(projectorSize - value) > 0.001f)
            {
                lock (viewProjectionLock)
                {
                    projectorSize = value;
                    // Invalidate cached view projection for projector lights
                    cachedViewProjectionMatrix = null;
                }
            }
        }
    }

    private float nearPlane = 0.05f;
    public float NearPlane 
    { 
        get => nearPlane;
        set 
        {
            if (Math.Abs(nearPlane - value) > 0.001f)
            {
                lock (viewProjectionLock)
                {
                    nearPlane = value;
                    // Invalidate cached view projections for all light types
                    cachedPointLightViewProjections = null;
                    cachedViewProjectionMatrix = null;
                }
            }
        }
    }

    public float Bias { get; set; } = 0.0000125f;
    public float NormalBias { get; set; } = 0.0f;
    
    private float intensity = 3.0f;
    public float Intensity 
    { 
        get => intensity;
        set 
        {
            if (Math.Abs(intensity - value) > 0.001f) // Only notify if there's a significant change
            {
                intensity = value;
                // Notify shadow atlas about the change with a delay to prevent rapid updates
                if (Engine.UseShadowAtlas && Engine.ShadowAtlas != null && EnableShadows)
                {
                    // Queue the update on the main thread to avoid race conditions
                    Engine.QueueOnMainThread(() =>
                    {
                        try
                        {
                            if (Engine.ShadowAtlas != null)
                            {
                                Engine.ShadowAtlas.MarkDirtyForLight(this);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error updating shadow atlas for intensity change on light {Name}: {ex.Message}");
                        }
                    });
                }
            }
        }
    }

    private IESProfile? lightProfile;
    private bool useProfile;
    public IESProfile? LightIESProfile
    {
        get { return lightProfile; }
        set
        {
            if (value != null)
            {
                lightProfile = value;
                useProfile = true;
            }
            else
            {
                lightProfile = null;
                useProfile = false;
            }
        }
    }

    public bool UseIESProfile
    {
        get { return useProfile; }
        set { useProfile = value; }
    }

    // Volumetric lighting properties
    public float VolumetricIntensity { get; set; } = 1.0f;       // Multiplier for volumetric visibility
    public float VolumetricAbsorption { get; set; } = 0.1f;     // How much light is absorbed (0.0 = no absorption, 1.0 = full absorption)
    public float VolumetricScattering { get; set; } = 0.5f;     // Scattering strength (0.0 = no scattering, 2.0+ = very bright)
    public float VolumetricAnisotropy { get; set; } = 0.3f;     // Scattering direction (-1.0 = back-scatter, 0.0 = isotropic, 1.0 = forward-scatter)

    public enum LightType
    {
        Spot = 0, // a standard spot light

        Point = 1, // a punctual light with 360 degree shadows

        Directional = 2, // a sun-type of light with cascaded shadowmapping

        Area = 3, // maybe I won't use this but it's here as a placeholder :)

        Projector = 4, // this will be useful for projected texture lights where you don't always want a cone, instead I will use a smooth box...

        Omni = 5, // this will behave like the omni lights found in source 2 where they render partial point light shadows,
        // when the spot outer angle (which controls the fov of spotlight shadow perspective matrix) is <= 90 degrees,
        // this light will render one perspective shadowmap, when the angle is > 90deg && < 270deg it'll render partial point light,
        // when it's above 270deg it'll draw all 6 shadowmaps but this type of light is handy for certain situations :)
    }

    private LightType type = LightType.Spot;
    public LightType Type
    {
        get { return type; }
        set { type = value; Name = $"({type.ToString().Split('.').Last()} light {lightID}) {localName}"; }
    } 
private Matrix4 Projection;

    private Matrix4? cachedViewProjectionMatrix = null;
    private float lastCachedOuterCone = -1f;
    private float lastCachedProjectorSize = -1f;
    private Vector3 lastCachedSpotPosition = Vector3.Zero;
    private Quaternion lastCachedSpotRotation = Quaternion.Identity;
    
    public Matrix4 ViewProjectionMatrix
    {
        get
        {
            lock (viewProjectionLock)
            {
                // Check if we need to recalculate for spot/projector lights
                if (Type == LightType.Spot || Type == LightType.Projector)
                {
                    if (cachedViewProjectionMatrix == null ||
                        Math.Abs(lastCachedRange - Range) > 0.001f ||
                        Math.Abs(lastCachedOuterCone - OuterCone) > 0.001f ||
                        Math.Abs(lastCachedProjectorSize - ProjectorSize) > 0.001f ||
                        Vector3.Distance(lastCachedSpotPosition, Transform?.Position ?? Vector3.Zero) > 0.001f ||
                        !QuaternionsAreEqual(lastCachedSpotRotation, Transform?.Rotation ?? Quaternion.Identity))
                    {
                        cachedViewProjectionMatrix = GetLightViewProjection();
                        lastCachedRange = Range;
                        lastCachedOuterCone = OuterCone;
                        lastCachedProjectorSize = ProjectorSize;
                        lastCachedSpotPosition = Transform?.Position ?? Vector3.Zero;
                        lastCachedSpotRotation = Transform?.Rotation ?? Quaternion.Identity;
                    }
                    
                    return cachedViewProjectionMatrix.Value;
                }
                else
                {
                    // For other light types, calculate directly
                    return GetLightViewProjection();
                }
            }
        }
    }

    private Matrix4[]? cachedPointLightViewProjections = null;
    private float lastCachedRange = -1f;
    private Vector3 lastCachedPosition = Vector3.Zero;
    private readonly object viewProjectionLock = new object();
    
    public Matrix4[] PointLightViewProjections
    {
        get
        {
            lock (viewProjectionLock)
            {
                // Check if we need to recalculate
                if (cachedPointLightViewProjections == null || 
                    Math.Abs(lastCachedRange - Range) > 0.001f ||
                    Vector3.Distance(lastCachedPosition, Transform?.Position ?? Vector3.Zero) > 0.001f)
                {
                    cachedPointLightViewProjections = GetPointLightViewProjections();
                    lastCachedRange = Range;
                    lastCachedPosition = Transform?.Position ?? Vector3.Zero;
                }
                
                return cachedPointLightViewProjections;
            }
        }
    }

    private int lightID = -1;
    private string localName = "";
    
    public Light(Vector3 position, string name = "") : base(position, name)
    {
        lightID = ++LightCount;
        localName = name;
        Name = $"(Light {lightID}) {localName}";
        Random rnd = new Random();
        var col = new Color4(rnd.Next(60, 255), rnd.Next(60, 255), rnd.Next(60, 255), rnd.Next(0, 255));
        Colour = col;
    }
    
    public override void Update(float deltaTime)
    {
        // Check if transform changed and invalidate cache if needed
        if (Transform != null)
        {
            Vector3 currentPosition = Transform.Position;
            Quaternion currentRotation = Transform.Rotation;
            
            if (Type == LightType.Point)
            {
                if (Vector3.Distance(lastCachedPosition, currentPosition) > 0.001f)
                {
                    lock (viewProjectionLock)
                    {
                        cachedPointLightViewProjections = null;
                    }
                }
            }
            else if (Type == LightType.Spot || Type == LightType.Projector)
            {
                if (Vector3.Distance(lastCachedSpotPosition, currentPosition) > 0.001f ||
                    !QuaternionsAreEqual(lastCachedSpotRotation, currentRotation))
                {
                    lock (viewProjectionLock)
                    {
                        cachedViewProjectionMatrix = null;
                    }
                }
            }
        }
        
        base.Update(deltaTime);
    }

    private Matrix4 GetLightViewProjection()
    {
        try
        {
            if (EnableShadows)
            {
                // Validate transform
                if (Transform == null)
                {
                    Console.WriteLine($"Warning: Light {Name} has null transform");
                    return Matrix4.Identity;
                }
                
                // Validate parameters
                float currentRange = Range;
                float currentNearPlane = NearPlane;
                float currentOuterCone = OuterCone;
                float currentProjectorSize = ProjectorSize;
                
                if (currentRange <= 0 || float.IsNaN(currentRange) || float.IsInfinity(currentRange))
                {
                    Console.WriteLine($"Warning: Light {Name} has invalid range: {currentRange}");
                    return Matrix4.Identity;
                }
                
                if (currentNearPlane <= 0 || float.IsNaN(currentNearPlane) || float.IsInfinity(currentNearPlane))
                {
                    Console.WriteLine($"Warning: Light {Name} has invalid near plane: {currentNearPlane}");
                    return Matrix4.Identity;
                }
                
                Vector3 position = Transform.Position;
                if (float.IsNaN(position.X) || float.IsNaN(position.Y) || float.IsNaN(position.Z) ||
                    float.IsInfinity(position.X) || float.IsInfinity(position.Y) || float.IsInfinity(position.Z))
                {
                    Console.WriteLine($"Warning: Light {Name} has invalid position: {position}");
                    return Matrix4.Identity;
                }
                
                currentOuterCone = Math.Clamp(currentOuterCone, 1, 179);
                InnerCone = Math.Clamp(InnerCone, 1, currentOuterCone);
                Matrix4 view = Matrix4.Identity;

                if (Type == LightType.Spot)
                {
                    Vector3 forward = Transform.Forward;
                    Vector3 up = Transform.Up;
                    
                    // Validate transform vectors
                    if (float.IsNaN(forward.Length) || forward.Length < 0.001f)
                    {
                        forward = Vector3.UnitZ;
                    }
                    if (float.IsNaN(up.Length) || up.Length < 0.001f)
                    {
                        up = Vector3.UnitY;
                    }
                    
                    view = Matrix4.LookAt(position, position + forward * 10, up);
                }
                else if (Type == LightType.Projector)
                {
                    Vector3 forward = Transform.Forward;
                    Vector3 up = Transform.Up;
                    
                    // Validate transform vectors
                    if (float.IsNaN(forward.Length) || forward.Length < 0.001f)
                    {
                        forward = Vector3.UnitZ;
                    }
                    if (float.IsNaN(up.Length) || up.Length < 0.001f)
                    {
                        up = Vector3.UnitY;
                    }
                    
                    view = Matrix4.LookAt(position - forward * 100, position + forward * 10, up);
                }
                
                Matrix4 projection = Matrix4.Identity;

                if (Type == LightType.Spot)
                {
                    // Ensure near plane is less than range
                    if (currentNearPlane >= currentRange)
                    {
                        currentNearPlane = currentRange * 0.01f;
                    }
                    
                    projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(currentOuterCone), 1, currentNearPlane, currentRange);
                }
                else if(Type == LightType.Projector)
                {
                    if (currentProjectorSize <= 0 || float.IsNaN(currentProjectorSize) || float.IsInfinity(currentProjectorSize))
                    {
                        currentProjectorSize = 5.0f;
                    }
                    
                    projection = Matrix4.CreateOrthographic(currentProjectorSize, currentProjectorSize, 0.1f, currentRange);
                }
                
                var result = view * projection;
                
                // Validate the resulting matrix
                if (!IsValidMatrix(result))
                {
                    Console.WriteLine($"Warning: Invalid view projection matrix for light {Name}");
                    return Matrix4.Identity;
                }
                
                return result;
            }
            else
            {
                return Matrix4.Identity;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating view projection for light {Name}: {ex.Message}");
            return Matrix4.Identity;
        }
    }
    
    private Matrix4[] GetPointLightViewProjections()
    {
        try
        {
            // Check if transform is valid
            if (Transform == null)
            {
                Console.WriteLine($"Warning: Light {Name} has null transform");
                return CreateIdentityMatrixArray();
            }
            
            // Validate parameters before proceeding
            float currentRange = Range;
            float currentNearPlane = NearPlane;
            
            if (currentRange <= 0 || float.IsNaN(currentRange) || float.IsInfinity(currentRange))
            {
                Console.WriteLine($"Warning: Light {Name} has invalid range: {currentRange}");
                return CreateIdentityMatrixArray();
            }
            
            if (currentNearPlane <= 0 || float.IsNaN(currentNearPlane) || float.IsInfinity(currentNearPlane))
            {
                Console.WriteLine($"Warning: Light {Name} has invalid near plane: {currentNearPlane}");
                return CreateIdentityMatrixArray();
            }
            
            Vector3 position = Transform.Position;
            if (float.IsNaN(position.X) || float.IsNaN(position.Y) || float.IsNaN(position.Z) ||
                float.IsInfinity(position.X) || float.IsInfinity(position.Y) || float.IsInfinity(position.Z))
            {
                Console.WriteLine($"Warning: Light {Name} has invalid position: {position}");
                return CreateIdentityMatrixArray();
            }
            
            Vector3[] Ups = new Vector3[]
            {
                Vector3.UnitY, Vector3.UnitY, Vector3.UnitX, Vector3.UnitX, Vector3.UnitY, Vector3.UnitY
            };
            Vector3[] Dirs = new Vector3[]
            {
                Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ,
            };
            
            Matrix4 view = Matrix4.Identity;
            Matrix4 projection;
            
            List<Matrix4> viewProjections = new List<Matrix4>();
            
            // Validate parameters
            float nearPlane = Math.Max(currentNearPlane, 0.01f); // Ensure near plane is valid
            float range = Math.Max(currentRange, 0.1f); // Ensure range is valid
            
            // Ensure near plane is less than range
            if (nearPlane >= range)
            {
                nearPlane = range * 0.01f; // Set near plane to 1% of range
            }
            
            projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(90), 1, nearPlane, range);
            
            for (int i = 0; i < 6; i++)
            {
                try
                {
                    view = Matrix4.LookAt(position, position + Dirs[i], Ups[i]);
                    var result = view * projection;
                    
                    // Validate the resulting matrix
                    if (IsValidMatrix(result))
                    {
                        viewProjections.Add(result);
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Invalid matrix for face {i} of light {Name}");
                        viewProjections.Add(Matrix4.Identity);
                    }
                }
                catch (Exception innerEx)
                {
                    Console.WriteLine($"Error creating view projection for face {i} of light {Name}: {innerEx.Message}");
                    viewProjections.Add(Matrix4.Identity);
                }
            }

            return viewProjections.ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating point light view projections for {Name}: {ex.Message}");
            // Return array of identity matrices as fallback
            return CreateIdentityMatrixArray();
        }
    }
    
    private Matrix4[] CreateIdentityMatrixArray()
    {
        var identityArray = new Matrix4[6];
        for (int i = 0; i < 6; i++)
        {
            identityArray[i] = Matrix4.Identity;
        }
        return identityArray;
    }
    
    private bool IsValidMatrix(Matrix4 matrix)
    {
        // Check if any component is NaN or Infinity
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                float value = matrix[row, col];
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    return false;
                }
            }
        }
        return true;
    }
    
    private bool QuaternionsAreEqual(Quaternion q1, Quaternion q2, float threshold = 0.001f)
    {
        // Calculate the dot product manually
        float dot = q1.X * q2.X + q1.Y * q2.Y + q1.Z * q2.Z + q1.W * q2.W;
        // Use absolute value since quaternions q and -q represent the same rotation
        return Math.Abs(Math.Abs(dot) - 1.0f) < threshold;
    }

    private int noiseTex = -1;

    private Frustum[]? frustums = new Frustum[6];

    private bool first = true;
    public override void Render(RenderMode mode = RenderMode.Default)
    {
        
        if (!IsVisible)
        {
            return;
        }

        var cam = ActiveCamera;

        if (Type == LightType.Directional)
        {
            for (int i = 0; i < cascadeCount; i++)
            {
                if (first)
                {
                    Frustum fr = new Frustum(CSMMatrices[i]);
                    frustums[i] = fr;
                }
                frustums[i].first = true;
                frustums[i].UpdateFrustum(CSMMatrices[i]);
                frustums[i].DrawFrustum(Frustum.FrustumDrawMode.Debug);
            }
            first = false;
        }
        if (noiseTex == -1)
        {
            noiseTex = TextureLoader.LoadTexture("ssao_noise.png", false).Reference().GetGLTexture();
        }
        
        RenderBufferHelpers.Instance.GetTextureIDs(out int[] textures);
        
        int[] tex = new int[textures.Length+3];
        tex[0] = textures[0];
        tex[1] = textures[1];
        tex[2] = textures[2];
        tex[3] = SpotShadowMapTexture;
        tex[4] = noiseTex;
        tex[5] = Environment.EnvmapID;
        tex[6] = CookieTextureID;

        if (Type == LightType.Spot || Type == LightType.Projector)
        {
            // Use shadow atlas if enabled and available, otherwise fall back to traditional shadow maps
            if (Engine.UseShadowAtlas && Engine.ShadowAtlas != null)
            {
                tex[3] = Engine.ShadowAtlas.AtlasTexture;
            }
            FullscreenQuad.RenderQuad("DeferredLight", tex, new[] { "screenTexture", "screenDepth", "screenNormal", "shadowMap", "ssaoNoise", "cubemap", "lightCookieTexture" }, Transform, this);
        }
        else if (Type == LightType.Point || Type == LightType.Directional)
        {
            int[] texs = new int[10];
            texs[0] = textures[0];
            texs[1] = textures[1];
            texs[2] = textures[2];
            
            // Use shadow atlas for point lights if enabled, otherwise use traditional cube maps
            if (Type == LightType.Point && Engine.UseShadowAtlas && Engine.ShadowAtlas != null)
            {
                // For atlas-based point lights, bind the atlas texture to all 6 slots
                int atlasTexture = Engine.ShadowAtlas.AtlasTexture;
                texs[3] = atlasTexture;
                texs[4] = atlasTexture;
                texs[5] = atlasTexture;
                texs[6] = atlasTexture;
                texs[7] = atlasTexture;
                texs[8] = atlasTexture;
            }
            else
            {
                // Traditional point light shadow maps
                texs[3] = PointShadowMapTextures[0];
                texs[4] = PointShadowMapTextures[1];
                texs[5] = PointShadowMapTextures[2];
                texs[6] = PointShadowMapTextures[3];
                texs[7] = PointShadowMapTextures[4];
                texs[8] = PointShadowMapTextures[5];
            }
            
            texs[9] = Environment.EnvmapID;
            FullscreenQuad.RenderQuad("DeferredLight", texs, new[] { "screenTexture", "screenDepth", "screenNormal", "shadowMap0", "shadowMap1", "shadowMap2", "shadowMap3", "shadowMap4", "shadowMap5", "cubemap" }, Transform, this);

        }
    }

    
    public static float[] GetCascadeSplits(int cascadeCount, float nearPlane, float farPlane, float CascadeSplitFactor)
    {
        float[] splits = new float[cascadeCount + 1];
        splits[0] = nearPlane;
        for (int i = 1; i <= cascadeCount; i++)
        {
            float p = i / (float)cascadeCount;
            float log = nearPlane * MathF.Pow(farPlane / nearPlane, p);
            float uniform = nearPlane + (farPlane - nearPlane) * p;
            splits[i] = MathHelper.Lerp(uniform, log, CascadeSplitFactor);
        }
        return splits;
    }

    /// <summary>
    /// Projects a point along the view-space Z axis and returns its clip-space Z (NDC).
    /// </summary>
    private static float ProjectedZ(Matrix4 proj, float viewDepth)
    {
        var p = new Vector4(0, 0, -viewDepth, 1);
        var clip = p * proj;
        clip.Xyz /= clip.W;
        return clip.Z;
    }

    /// <summary>
    /// Calculates world-space frustum corners for a given cascade slice.
    /// </summary>
    private static Vector3[] GetFrustumCornersWorld(Matrix4 invViewProj, Matrix4 proj, float near, float far)
    {
        float zNearNdc = ProjectedZ(proj, near);
        float zFarNdc  = ProjectedZ(proj, far);

        Vector3[] corners = new Vector3[8];
        int idx = 0;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = 0; z <= 1; z++)
        {
            float ndcZ = (z == 0) ? zNearNdc : zFarNdc;
            var clip = new Vector4(x, y, ndcZ, 1);
            var worldH = clip * invViewProj;
            worldH.Xyz /= worldH.W;
            corners[idx++] = worldH.Xyz;
        }
        return corners;
    }

    /// <summary>
/// Builds a light‑space matrix by enclosing the cascade's frustum corners
/// in a bounding sphere, snapping to the texel grid to stabilize shimmering.
/// </summary>
private static Matrix4 GetLightViewProjection(
    Vector3[] frustumCorners,
    Vector3 lightDir,
    int shadowMapResolution,
    float zPaddingNear = 2000f,
    float zPaddingFar  = 2000f)
{
    // 1) Compute center & radius of bounding sphere in world space
    Vector3 centerWS = Vector3.Zero;
    foreach (var v in frustumCorners) centerWS += v;
    centerWS /= frustumCorners.Length;

    float radius = 0f;
    foreach (var v in frustumCorners)
    {
        float d = (v - centerWS).Length;
        if (d > radius) radius = d;
    }

    // 2) Build light‑view matrix (row‑vector convention)
    Matrix4 lightView = Matrix4.LookAt(
        centerWS - lightDir * 100f,
        centerWS,
        Vector3.UnitY);

    // 3) Transform sphere center into light space
    var centerLS4 = new Vector4(centerWS, 1f) * lightView;
    Vector3 centerLS = centerLS4.Xyz / centerLS4.W;

    // 4) Optionally pad radius in Z to capture objects beyond the sphere's bounds
    float nearZ = centerLS.Z - radius - zPaddingNear;
    float farZ  = centerLS.Z + radius + zPaddingFar;

    // 5) Snap the X/Y center to the texel grid in light‑space
    //    (we use the sphere's diameter as the ortho width/height)
    float worldUnitsPerTexel = (radius * 2.0f) / shadowMapResolution;
    centerLS.X = MathF.Floor(centerLS.X / worldUnitsPerTexel) * worldUnitsPerTexel;
    centerLS.Y = MathF.Floor(centerLS.Y / worldUnitsPerTexel) * worldUnitsPerTexel;

    // 6) Reconstruct a symmetric ortho box around the snapped center
    float left   = centerLS.X - radius;
    float right  = centerLS.X + radius;
    float bottom = centerLS.Y - radius;
    float top    = centerLS.Y + radius;

    // 7) Create the orthographic projection
    Matrix4 lightProj = Matrix4.CreateOrthographicOffCenter(
        left,   right,
        bottom, top,
        nearZ,  farZ
    );

    // 8) Return the combined light‑space matrix
    //    (still row‑vector: v * (lightView * lightProj))
    return lightView * lightProj;
}

    /// <summary>
    /// Generates the cascaded shadow map view-projection matrices.
    /// </summary>
    public static Matrix4[] GenerateCascadedShadowMatrices(Camera camera, Light light, int shadowMapResolution)
    {
        var splits = GetCascadeSplits(light.CascadeCount, camera.ZNear, camera.ZFar, light.CascadeSplitFactor);
        Matrix4 proj = camera.ProjectionMatrix;
        Matrix4 invViewProj = camera.ViewProjectionMatrix.Inverted(); 

        Matrix4[] cascades = new Matrix4[light.CascadeCount];
        Vector3 lightDir = -light.Transform.Forward.Normalized();

        for (int i = 1; i <= light.CascadeCount; i++)
        {
            float splitNear = splits[i-1];
            float splitFar  = splits[i];

            var frustumCorners = GetFrustumCornersWorld(invViewProj, proj, splitNear, splitFar);
            cascades[i-1] = GetLightViewProjection(frustumCorners, lightDir, shadowMapResolution);
        }

        return cascades;
    }
}