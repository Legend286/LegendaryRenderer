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
    public float Range { get; set; } = 10.0f;
    
    public static int GetCount => LightCount;

    public bool EnableShadows { get; set; } = false;

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

    public float OuterCone { get; set; } = 90.0f;

    public float ProjectorSize { get; set; } = 5.0f;

    public float NearPlane { get; set; } = 0.05f;

    public float Bias { get; set; } = 0.0000125f;
    public float NormalBias { get; set; } = 0.0f;
    public float Intensity { get; set; } = 3.0f;

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

    public Matrix4 ViewProjectionMatrix
    {
        get
        {
            return GetLightViewProjection();
        }
    }

    public Matrix4[] PointLightViewProjections
    {
        get
        {
            return GetPointLightViewProjections();
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

    private Matrix4 GetLightViewProjection()
    {
        if (EnableShadows)
        {
            OuterCone = Math.Clamp(OuterCone, 1, 179);
            InnerCone = Math.Clamp(InnerCone, 1, OuterCone);
            Matrix4 view = Matrix4.Identity;

            if (Type == LightType.Spot)
            {
                // Position shadow camera at light position, looking toward the center of the light's range
                // This ensures shadows are centered on what the light actually illuminates
                Vector3 shadowCameraTarget = Transform.Position + Transform.Forward * (Range * 0.5f);
                view = Matrix4.LookAt(Transform.Position, shadowCameraTarget, Transform.Up);
            }
            else if (Type == LightType.Projector)
            {
                view = Matrix4.LookAt(Transform.Position - Transform.Forward * 100, Transform.Position + Transform.Forward * 10, Transform.Up);
            }
            Matrix4 projection = Matrix4.Identity;

            if (Type == LightType.Spot)
            {
                projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(OuterCone), 1, NearPlane, Range);
            }
            else if(Type == LightType.Projector)
            {
                projection = Matrix4.CreateOrthographic(ProjectorSize, ProjectorSize, 0.1f, Range);
            }
            return view * projection;
        }
        else
        {
            return Matrix4.Identity;
        }
    }
    
    private Matrix4[] GetPointLightViewProjections()
    {
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
        
        projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(90), 1, NearPlane, Range);
        for (int i = 0; i < 6; i++)
        {
            view = Matrix4.LookAt(Transform.Position, Transform.Position + Dirs[i], Ups[i]);
            var result = view * projection;
            
            viewProjections.Add(result);
        }

        return viewProjections.ToArray();
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
            FullscreenQuad.RenderQuad("DeferredLight", tex, new[] { "screenTexture", "screenDepth", "screenNormal", "shadowMap", "ssaoNoise", "cubemap", "lightCookieTexture" }, Transform, this);
        }
        else if (Type == LightType.Point || Type == LightType.Directional)
        {
            int[] texs = new int[10];
            texs[0] = textures[0];
            texs[1] = textures[1];
            texs[2] = textures[2];
            texs[3] = PointShadowMapTextures[0];
            texs[4] = PointShadowMapTextures[1];
            texs[5] = PointShadowMapTextures[2];
            texs[6] = PointShadowMapTextures[3];
            texs[7] = PointShadowMapTextures[4];
            texs[8] = PointShadowMapTextures[5];
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