using Geometry;
using Geometry.MaterialSystem.IESProfiles;
using LegendaryRenderer.Engine.EngineTypes;
using LegendaryRenderer.GameObjects;
using LegendaryRenderer.LegendaryRuntime.Engine.Renderer.MaterialSystem;
using OpenTK.Mathematics;
using static LegendaryRenderer.Application.Engine;
using Environment = TheLabs.LegendaryRuntime.Engine.Renderer.Environment;

namespace TheLabs.LegendaryRuntime.Engine.GameObjects;

public class Light : GameObject
{
    private static int LightCount = -1;

    public Color4 Colour { get; set; } = Color4.White;
    public float Range { get; set; } = 10.0f;

    public bool EnableShadows { get; set; } = false;

    public float InnerCone { get; set; } = 75.0f;

    public float OuterCone { get; set; } = 90.0f;

    public float NearPlane { get; set; } = 0.05f;

    public float Bias { get; set; } = 0.000001f;
    public float NormalBias { get; set; } = 1.0f;
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
        get
        {
            return useProfile;
        }
        set
        {
            useProfile = value;
        }
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

    public LightType Type { get; set; } = LightType.Spot;

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
    
    public Light(Vector3 position, string name = "") : base(position, name)
    {
        Name = $"(Light {++LightCount}) {name}";
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
                view = Matrix4.LookAt(Transform.Position, Transform.Position + Transform.Forward * 10, Transform.Up);
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
                projection = Matrix4.CreateOrthographic(5, 5, 0.1f, 1000.0f);
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
    public override void Render(RenderMode mode = RenderMode.Default)
    {
        if (!IsVisible)
        {
            return;
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

        if (Type == LightType.Spot || Type == LightType.Projector)
        {
            FullscreenQuad.RenderQuad("DeferredLight", tex, new[] { "screenTexture", "screenDepth", "screenNormal", "shadowMap", "ssaoNoise", "cubemap" }, Transform, this);
        }
        else if (Type == LightType.Point)
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
}