using System.Collections.Concurrent;
using System.Drawing;
using System.Net.Http.Headers;
using ImGuiNET;
using LegendaryRenderer.Application;
using LegendaryRenderer.LegendaryRuntime.Application;
using LegendaryRenderer.LegendaryRuntime.Application.Profiling;
using LegendaryRenderer.LegendaryRuntime.Application.ProgressReporting;
using LegendaryRenderer.LegendaryRuntime.Engine.Editor;
using LegendaryRenderer.LegendaryRuntime.Engine.Editor.Dockspace;
using LegendaryRenderer.LegendaryRuntime.Engine.Editor.Gizmos;
using LegendaryRenderer.LegendaryRuntime.Engine.Editor.Helpers;
using LegendaryRenderer.LegendaryRuntime.Engine.Editor.Systems;
using LegendaryRenderer.LegendaryRuntime.Engine.Editor.UserInterface;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MaterialSystem;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MeshInstancing;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.Systems.Compute;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.Systems.SceneSystem;
using LegendaryRenderer.LegendaryRuntime.Engine.Renderer.MaterialSystem;
using LegendaryRenderer.Shaders;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using BlendingFactor = OpenTK.Graphics.OpenGL.BlendingFactor;
using Buffer = System.Buffer;
using ClearBufferMask = OpenTK.Graphics.OpenGL.ClearBufferMask;
using EnableCap = OpenTK.Graphics.OpenGL.EnableCap;
using FramebufferAttachment = OpenTK.Graphics.OpenGL.FramebufferAttachment;
using FramebufferTarget = OpenTK.Graphics.OpenGL.FramebufferTarget;
using GL = OpenTK.Graphics.OpenGL.GL;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;
using PixelInternalFormat = OpenTK.Graphics.OpenGL.PixelInternalFormat;
using PixelType = OpenTK.Graphics.OpenGL.PixelType;
using TextureMinFilter = OpenTK.Graphics.ES11.TextureMinFilter;
using TextureParameterName = OpenTK.Graphics.OpenGL.TextureParameterName;
using TextureTarget = OpenTK.Graphics.OpenGL.TextureTarget;
using TextureWrapMode = OpenTK.Graphics.OpenGL.TextureWrapMode;
using TextureHandle = LegendaryRenderer.LegendaryRuntime.Engine.Utilities.GLHelpers.TextureHandle;
using Vector2 = OpenTK.Mathematics.Vector2;
using Vector3 = OpenTK.Mathematics.Vector3;
using Vector4 = OpenTK.Mathematics.Vector4;
using System.Runtime.InteropServices;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Engine;

public struct SSAOSettings
{
    public float Radius;
    public float Bias;
    public int NumberOfSamples;

    public SSAOSettings()
    {
        Radius = 0.3f;
        Bias = 0.01f;
        NumberOfSamples = 32;
    }
}

public struct ShadowInstanceData
{
    public Matrix4 ModelMatrix;
    public Matrix4 LightViewProjection;
    public Vector4 AtlasScaleOffset;  // xy = scale, zw = offset
    public Vector4 TileBounds;       // for pixel shader clip
    public int LightIndex;
    public int FaceIndex;            // For point lights (0-5), -1 for others
    private float padding1;
    private float padding2;
}

public struct LightShadowInfo
{
    public Light Light;
    public int BaseAtlasIndex;       // Starting index in atlas
    public int TileCount;            // Number of tiles (1 for spot/directional, 6 for point)
    public bool IsVisible;
    public List<RenderableMesh> ShadowCasters;
}

public struct AtlasTileInfo
{
    public Vector2 atlasOffset;
    public Vector2 atlasScale;
    public int tileSizeInPixels;
    
    public AtlasTileInfo(Vector2 offset, Vector2 scale, int tileSize)
    {
        atlasOffset = offset;
        atlasScale = scale;
        tileSizeInPixels = tileSize;
    }
}

public static class Engine
{
    public static List<GameObject> GameObjects = new List<GameObject>();
    
    public static List<RenderableMesh> RenderableMeshes = new List<RenderableMesh>();

    public static List<Scene> LoadedScenes = new List<Scene>();
    
    public static Camera ActiveCamera;

    public static ShaderFile currentShader;

    public static RenderBufferHelpers RenderBuffers;

    public static int ShadowResolution = 4096;
    
    public static bool ShouldDoSelectionNextFrame = false;

    public static bool CanSelect = true;

    public static SSAOSettings SSAOSettings = new SSAOSettings();

    public static bool EnableShadows = true;
 
    // counters for statistics :)
    public static int
        TriangleCountRendered = 0,
        TriangleCountCulled = 0,
        TriangleCountTotal = 0,
        NumberOfVisibleLights = 0,
        DrawCalls = 0,
        ShadowViewCount = 0,
        NumShadowCasters = 0,
        CullPass = 0;

    // Shadow atlas management
    private static int shadowAtlasTexture = -1;
    private static int shadowAtlasFBO = -1;
    public static int ShadowAtlasResolution = 4096;
    private static int currentAtlasTileSize = 512;
    
    // Instance data management
    private static Dictionary<RenderableMesh, List<ShadowInstanceData>> shadowInstanceGroups = new();
    private static List<LightShadowInfo> visibleShadowLights = new();
    private static int shadowInstanceBufferUBO = -1;
    private static List<ShadowInstanceData> allShadowInstances = new();
    
    // Matrix storage for synchronization between shadow generation and lighting
    private static Dictionary<int, Matrix4[]> storedShadowMatrices = new();
    
    // Maximum instances per draw call (OpenGL 4.1 compliant)
    public const int MAX_SHADOW_INSTANCES_PER_MESH = 64;

    static Engine()
    {
        Initialize();
    }

    public static ConsoleProgressBar EngineProgress { get; private set; }
    public static void Initialize()
    {
        using (EngineProgress = new ConsoleProgressBar())
        {
            EngineProgress.Report(0, "Initialising Engine...");
            
            // Load editor textures
            EngineProgress.Report(0.1, "Initialising EditorSystem...");
            EditorSystem.Initialise();
            
            EngineProgress.Report(0.2, "Initialising Shaders...");
            ShaderManager.LoadShader("basepass", out ShaderFile loaded);
            currentShader = loaded;
            
            GenerateShadowMap(ShadowResolution, ShadowResolution);
            GeneratePointShadowMaps(ShadowResolution, ShadowResolution);
            
            EngineProgress.Report(0.3, "Initialising RenderBuffers...");
            RenderBuffers = new RenderBufferHelpers(PixelInternalFormat.Rgba8, PixelInternalFormat.DepthComponent32f, Application.Application.Width, Application.Application.Height, "Main Buffer");
            EngineProgress.Report(0.5, "Initialising SceneSystem...");
            LoadedScenes.Add(new Scene(ref GameObjects));
            EngineProgress.Report(0.7, "Initialising DockSpace...");
            DockspaceController = new DockspaceController(Application.Application.windowInstance);
            EditorViewport = new EditorViewport(RenderBufferHelpers.Instance.GetTextureHandle(TextureHandle.COPY));
            EditorSceneHierarchyPanel = new EditorSceneHierarchyPanel(LoadedScenes[0]);
            EditorInspector = new EditorInspector();
            ContentBrowserWindow = new ContentBrowserWindow();
            ContentBrowserWindow.InitializeDefaultIcons();
            EngineProgress.Report(1.0f, "Initialising Engine Complete...");
        }
        EditorSceneHierarchyPanel.OnObjectSelected += Go =>
        {
            if (Go is not null)
            {
                SelectedRenderableObjects.Clear();
                SelectedRenderableObjects.Add(Go);
                Console.WriteLine("Selected from inspector" + Go.Name);
            }
            
        };

        InitializeShadowAtlas();
    }
    
    // Thread-safe queue for actions to run on the main thread.
    private static readonly ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();

    /// <summary>
    /// Enqueues an action to be executed on the main thread.
    /// </summary>
    public static void QueueOnMainThread(Action action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        mainThreadQueue.Enqueue(action);
    }

    /// <summary>
    /// Processes all queued actions. Call this method in your main update/render loop.
    /// </summary>
    public static void ProcessMainThreadQueue()
    {
        while (mainThreadQueue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception while executing queued action: {ex.Message}");
            }
        }
    }
    public static void Update(float deltaTime)
    {
        ProcessMainThreadQueue();
        
        foreach (GameObject go in GameObjects)
        {
            go.Update(deltaTime);
        }
        // do update logic here for entire engine
    }

    static Dictionary<Guid, GameObject> GameObjectToGUIDMap = new Dictionary<Guid, GameObject>();

    public static void AddGameObject(GameObject gameObject)
    {
        if (gameObject is Camera camera)
        {
            ActiveCamera = camera;
        }
        LoadedScenes[0].RootNode.Children.Add(gameObject);
        GameObjects.Add(gameObject);
        GameObjectToGUIDMap.Add(ConvertValueToGuid(GuidToUIntArray(gameObject.GUID)), gameObject);
        
        if (gameObject is RenderableMesh)
        {
            RenderableMeshes.Add(gameObject as RenderableMesh);
        }
    }
    public static List<RenderableMesh> CullRenderables(Matrix4 viewProjectionMatrix, bool shouldRender, List<RenderableMesh>? preCulledRenderables = null)
    {
        using (new Profiler($"Scene Culling {CullPass++}"))
        {
            var PotentialRenderedObjects = RenderableMeshes;

            if (preCulledRenderables != null)
            {
                PotentialRenderedObjects = preCulledRenderables;
            }

            List<RenderableMesh> Renderables = new List<RenderableMesh>();

            var Frst = new Frustum(viewProjectionMatrix);
            Frst.UpdateFrustum(viewProjectionMatrix);

            foreach (var objectToCull in PotentialRenderedObjects)
            {
                if (!objectToCull.IsVisible)
                {
                    continue;
                }
                TriangleCountTotal += objectToCull.TriangleCount / 3;

                if (Frst.ContainsSphere(objectToCull.Bounds.Centre, objectToCull.Bounds.Radius * MathF.Max(objectToCull.Transform.Scale.X, MathF.Max(objectToCull.Transform.Scale.Y, objectToCull.Transform.Scale.Z))) && shouldRender)
                {
                    Renderables.Add(objectToCull);
                    TriangleCountRendered += objectToCull.TriangleCount / 3;
                }
                else
                {
                   TriangleCountCulled += objectToCull.TriangleCount / 3;
                }
            }

            return Renderables;
        }
    }

    public static void RemoveGameObject(GameObject gameObject)
    {
        GameObjects.Remove(gameObject);
        LoadedScenes[0].RootNode.Children.Remove(gameObject);

        if (gameObject is RenderableMesh)
        {
            RenderableMeshes.Remove(gameObject as RenderableMesh);
        }
    }

    public static void RenderGBufferModels()
    {
        ShaderManager.LoadShader("basepass", out ShaderFile shader);
        shader.UseShader();
        foreach (GameObject go in CullRenderables(ActiveCamera.ViewProjectionMatrix, true))
        {
            go.Render(GameObject.RenderMode.GBuffer);
        }
    }
    private static int pattern = -1;
    private static void RenderSelectionOutline()
    {

        var rbInst = RenderBufferHelpers.Instance;

        if (rbInst == null)
        {
            throw new Exception("Null Buffers");
        }


        int a, b, c, d;
        a = rbInst.GetTextureHandle(TextureHandle.SELECTION_BUFFER_MASK);
        b = rbInst.GetTextureHandle(TextureHandle.SELECTION_BUFFER_DEPTH);
        c = rbInst.GetTextureHandle(TextureHandle.LIGHTING_RESULT);
        d = rbInst.GetTextureHandle(TextureHandle.PRIMARY_DEPTH);
        if (pattern == -1)
        {
            pattern = TextureLoader.LoadTexture("selectionpattern.png", false).GetGLTexture();
        }
        RenderBufferHelpers.Instance?.BindMainOutputBuffer();
        FullscreenQuad.RenderQuad("SelectionVisualiser", new[] { a, b, c, d, pattern }, new[] { "selectionMask", "selectionDepth", "sceneColour", "sceneDepth", "selectionTexture" });

    }

    public static List<GameObject> SelectedRenderableObjects { get; private set; } = new List<GameObject>();
    public static bool IsMultiSelect { get; set; } = false;

    public static void DoSelection()
    {
        if(ShouldDoSelectionNextFrame && !Gizmos.isGizmoActive && EditorViewport.IsHovered && CanSelect)
        {
            ShouldDoSelectionNextFrame = false;
            Engine.RenderSelectionBufferOnce();

            var Vec = ImGui.GetMousePos();
            Vec -= EditorViewport.ViewportPosition;
            Vec.X *= DPI.DPIScale.X;
            Vec.Y = (EditorViewport.ViewportSize.Y - Vec.Y) * DPI.DPIScale.Y;
            
            Engine.ReadMouseSelection((int)Vec.X,  (int)Vec.Y, out GameObject? hit);
            // width - pos for y because UV starts at bottom and window coord starts at top (or the other way around idk)

            Console.WriteLine($"Mouse Position: {(Vector2i)ApplicationWindow.mouseState.Position}");

            if (hit != null)
            {
                Console.WriteLine($"Selected Object: {hit.Name} ({hit.GUID})");
                if (hit is RenderableMesh)
                {
                    var rm = hit as RenderableMesh;
                    var mat = rm.Material;

                    Console.WriteLine(
                        $"Selected Object Texture IDs: Diffuse: {mat.DiffuseTexture},Masks: {mat.RoughnessTexture},Normal: {mat.NormalTexture}.");
                }

                if ((hit is RenderableMesh renderable || hit is Camera))
                {
                  /*  if (IsMultiSelect)
                    {
                        SelectedRenderableObjects.Add(hit);
                    }
                    else*/
                    {
                        SelectedRenderableObjects.Clear();
                        SelectedRenderableObjects.Add(hit);
                    }
                }
            }
            else if(hit == null)
            {
                SelectedRenderableObjects.Clear();
            }
            
            RenderBufferHelpers.Instance?.BindMainOutputBuffer();
        }
    }
/*
    uniform sampler2D selectionMask;
    uniform sampler2D selectionDepth;
    uniform sampler2D sceneColour;
    uniform sampler2D sceneDepth;

    reference so I know how to bind the textures in the right order, taken from selectionvisualiser.frag
*/
    public static void RenderSelectedObjects()
    {
        var rbInst = RenderBufferHelpers.Instance;

        if (rbInst == null)
        {
            throw new Exception("Null Buffers");
        }

        rbInst.BindSelectionFramebuffer();
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        
        if (SelectedRenderableObjects.Count > 0)
        {
            GL.Disable(EnableCap.CullFace);
            ShaderManager.LoadShader("debug", out ShaderFile shd);
            shd.UseShader();

            foreach (RenderableMesh mesh in CullRenderables(ActiveCamera.ViewProjectionMatrix, true, SelectedRenderableObjects.OfType<RenderableMesh>().ToList()))
            {
                Engine.currentShader.SetShaderVector3("Colour", new Vector3(1, 1, 1));
                mesh.Render(GameObject.RenderMode.ShadowPass);
            }
            GL.Enable(EnableCap.CullFace);
        }
        else
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }
        
        RenderBufferHelpers.Instance?.BindMainOutputBuffer();
    }
    
    public static void EngineRenderLoop()
    {

        using (new Profiler("Scene Rendering (Total)"))
        {
            EditorViewport.SetFramebufferID(RenderBuffers.GetTextureHandle(TextureHandle.COPY));
            FullscreenQuad.RenderQuad("AtmosphericSky", new[] { 0 }, new[] { "null" });

            RenderBuffers.BindGBuffer();

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit |
                     ClearBufferMask.StencilBufferBit);


            using (new Profiler("Render to GBuffer"))
            {
                Engine.RenderGBufferModels();
            }

            RenderBufferHelpers.Instance?.BindMainOutputBuffer();

            RenderBuffers.GetTextureIDs(out int[] textures);


            int lighting = RenderBuffers.GetLightingBufferID();

            using (new Profiler("Render Lights"))
            {
                RenderBufferHelpers.Instance.BindLightingFramebuffer();
                GL.ClearColor(Color.Black);
                GL.Clear(ClearBufferMask.ColorBufferBit);

                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);

                Engine.RenderLights();

                GL.BlendFunc(BlendingFactor.Zero, BlendingFactor.One);
                GL.Disable(EnableCap.Blend);
            }

      

            RenderBufferHelpers.Instance?.BindMainOutputBuffer();

            RenderAutoExposure();

            using (new Profiler("Motion Blur"))
            {
                FullscreenQuad.RenderQuad("MotionBlur", new[] { lighting, textures[3], textures[1] }, new[] { "sourceTexture", "velocityTexture", "depthTexture" });
            }


            // TODO: ugly shit like this copy code should be refactored into neat postprocess chaining auto copy
            RenderBufferHelpers.Instance?.BindLightingFramebuffer();
            int x = RenderBufferHelpers.Instance.GetTextureHandle(TextureHandle.COPY);
            FullscreenQuad.RenderQuad("Blit", new[] { x }, new[] { "sourceTexture" });


            using (new Profiler("Outline (Editor)"))
            {
                RenderSelectedObjects();
                RenderSelectionOutline();
            }

            // TODO: likewise 
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
           // x = RenderBufferHelpers.Instance.GetTextureHandle(TextureHandle.COPY);
           // FullscreenQuad.RenderQuad("Blit", new[] { x }, new[] { "sourceTexture" });

            if (ActiveCamera.PauseCameraFrustum)
            {
                ActiveCamera.Frustum.DrawFrustum(Frustum.FrustumDrawMode.Debug);
            }
            else
            {
                ActiveCamera.Frustum.firstRun = true;
            }
            
            RenderDebugModels();

           
        }
        RenderImGui();
  
    }

       
    public static DockspaceController DockspaceController;
    public static EditorViewport EditorViewport;
    public static EditorSceneHierarchyPanel EditorSceneHierarchyPanel;
    public static EditorInspector EditorInspector;
    public static ContentBrowserWindow ContentBrowserWindow;
    
    public static void RenderImGui()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        DockspaceController.BeginDockspace();
        ContentBrowserWindow.Draw();
        EditorViewport.Draw();
        EditorViewport.ApplyPendingResize();
        EditorSceneHierarchyPanel.Draw();
   
        EditorInspector.Draw();
        DockspaceController.EndDockspace();
    }

    // below was coded with help from CHATGPT>>
    public static uint[] GuidToUIntArray(Guid guid)
    {
        byte[] guidBytes = guid.ToByteArray();
        uint[] uintData = new uint[4];
        for (int i = 0; i < 4; i++)
        {
            uintData[i] = BitConverter.ToUInt32(guidBytes, i * 4);
        }
        return uintData;
    }

    public static Guid ReadGUIDFromPickingBuffer(int x, int y)
    {
        // Allocate an array to hold 4 floats (RGBA channels).
        float[] pixel = new float[4];
        
        RenderBufferHelpers.Instance.BindPickingBuffer();
        GL.ReadPixels(x, y, 1, 1, PixelFormat.Rgba, PixelType.Float, pixel);

        Console.WriteLine($"Pixel Values ({pixel[0]} {pixel[1]} {pixel[2]} {pixel[3]})");
        
        uint[] guidParts = new uint[4];
        for (int i = 0; i < 4; i++)
        {
            int intBits = BitConverter.SingleToInt32Bits(pixel[i]);
            guidParts[i] = unchecked((uint)intBits);
        }
        guidParts[3] = (uint)1.0f;

        byte[] guidBytes = new byte[16];
        Buffer.BlockCopy(BitConverter.GetBytes(guidParts[0]), 0, guidBytes, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(guidParts[1]), 0, guidBytes, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(guidParts[2]), 0, guidBytes, 8, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(guidParts[3]), 0, guidBytes, 12, 4);


       // Console.WriteLine("Guid: " + new Guid(guidBytes));
        
        return new Guid(guidBytes);
    }


    public static Guid ConvertValueToGuid(uint[] value)
    {
        uint[] guidParts = new uint[4];
        for (int i = 0; i < 4; i++)
        {
            uint intBits = value[i];
            guidParts[i] = intBits;
        }
        guidParts[3] = (uint)1.0f;

        byte[] guidBytes = new byte[16];
        Buffer.BlockCopy(BitConverter.GetBytes(guidParts[0]), 0, guidBytes, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(guidParts[1]), 0, guidBytes, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(guidParts[2]), 0, guidBytes, 8, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(guidParts[3]), 0, guidBytes, 12, 4);


       // Console.WriteLine("Guid: " + new Guid(guidBytes));

        return new Guid(guidBytes);
    }
    //<< Above was coded with help from chatgpt

    
    
    public static void RenderSelectionBufferOnce()
    {
        RenderBufferHelpers.Instance?.BindPickingBuffer();
        
        GL.DrawBuffers(1, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0 });

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        ShaderManager.LoadShader("SelectionBits", out ShaderFile shader);
        shader.UseShader();
        
        foreach (GameObject go in CullRenderables(ActiveCamera.ViewProjectionMatrix, true))
        {
            go.Render(GameObject.RenderMode.SelectionMask);
        }
        
        foreach(Camera cam in Engine.GameObjects.OfType<Camera>())
        {
            GL.BlendFunc(BlendingFactor.Zero, BlendingFactor.One);
            cam.Render(GameObject.RenderMode.SelectionMask);
            
        }
    }

    private static int spotLightShadowmapFBO = -1;
    private static int spotShadowMapTexture = -1;
    private static int[] pointShadowMapTextures = new int [6];
    private static int[] pointShadowFBOs = new int [6];

    private static bool PointShadowsCreated = false;

    public static int SpotShadowMapTexture
    {
        get { return spotShadowMapTexture; }
    }

    public static int[] PointShadowMapTextures
    {
        get { return pointShadowMapTextures; }
    }

    private static int SpotShadowWidth, SpotShadowHeight = 0;
    private static int PointShadowWidth, PointShadowHeight = 0;

    private static void GenerateShadowMap(int width, int height)
    {

        if (SpotShadowWidth != width || SpotShadowHeight != height)
        {
            using (var progress = new ConsoleProgressBar())
            {
                if (spotShadowMapTexture != -1)
                {
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                    GL.DeleteTexture(spotShadowMapTexture);
                    GL.DeleteFramebuffers(1, ref spotLightShadowmapFBO);
                }
                spotLightShadowmapFBO = GL.GenFramebuffer();

                SpotShadowWidth = width;
                SpotShadowHeight = height;

                GL.GenTextures(1, out spotShadowMapTexture);
                GL.BindTexture(TextureTarget.Texture2D, spotShadowMapTexture);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32f, width, height, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, spotLightShadowmapFBO);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, spotShadowMapTexture, 0);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                progress.Report((double)1, $"Generated Shadow Map");
            }
        }
    }

    private static void GeneratePointShadowMaps(int width, int height)
    {
        if (PointShadowWidth != width || PointShadowHeight != height || !PointShadowsCreated)
        {
            using (var progress = new ConsoleProgressBar())
            {
                for (int i = 0; i < 6; i++)
                {
                    if (PointShadowsCreated)
                    {
                        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                        GL.DeleteTexture(pointShadowMapTextures[i]);
                        GL.DeleteFramebuffers(1, ref pointShadowFBOs[i]);
                    }

                    pointShadowFBOs[i] = GL.GenFramebuffer();
                    PointShadowWidth = width;
                    PointShadowHeight = height;

                    GL.GenTextures(1, out pointShadowMapTextures[i]);
                    GL.BindTexture(TextureTarget.Texture2D, pointShadowMapTextures[i]);
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32f, width, height, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMinFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, pointShadowFBOs[i]);
                    GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, pointShadowMapTextures[i], 0);
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                    progress.Report((double)i / 6.0, $"Generated Point Shadow Map {i + 1} of 6");
                }
            }

            PointShadowsCreated = true;
        }
    }




    public static void BindShadowMap()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, spotLightShadowmapFBO);
    }

    // NOTE: THIS CURRENTLY USES THE SAME SHADOWMAPS AS THE POINT LIGHTS :)
    public static void BindCascadedShadowMap(int cascadeIndex)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, pointShadowMapTextures[cascadeIndex]);
    }

    public static void BindPointShadowMap(int face)
    {
        if (face is < 0 or > 5)
        {
            throw new InvalidOperationException("Face must be between 0 and 5");
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, pointShadowFBOs[face]);
    }

    public static void RenderSpotShadowMap(Light light, bool shouldRender)
    {
        using (new Profiler($"{light.Name} Shadowmap Rendering"))
        {
            BindShadowMap();
            GL.Viewport(0, 0, SpotShadowWidth, SpotShadowHeight);
            GL.Clear(ClearBufferMask.DepthBufferBit);
            ShaderManager.LoadShader("shadowgen", out ShaderFile shader);
            shader.UseShader();

            if (shouldRender)
            {
                ShadowViewCount++;
            }
            foreach (GameObject go in CullRenderables(light.ViewProjectionMatrix, shouldRender))
            {
                if (shouldRender)
                {
                    if (go is not Camera && go is not Light)
                    {
                        NumShadowCasters++;
                        shader.SetShaderMatrix4x4("shadowViewProjection", light.ViewProjectionMatrix);
                        shader.SetShaderMatrix4x4("model", go.Transform.GetWorldMatrix());
                        go.Render(GameObject.RenderMode.ShadowPass);
                    }
                }

            }
         
            RenderBufferHelpers.Instance.BindLightingFramebuffer();
            
        }
    }

    public static Vector4[] BuildCascadeAtlas(int numCascades)
    {
        // 1) compute grid dimensions
        int cols = (int)MathF.Ceiling(MathF.Sqrt(numCascades));
        int rows = (int)MathF.Ceiling((float)numCascades / cols);
        
        // 2) one uniform UV scale for all tiles
        Vector2 uvScale = new Vector2(1f / cols, 1f / rows);

        // 3) fill out the per-cascade offset+scale
        var atlas = new Vector4[numCascades];
        for (int i = 0; i < numCascades; i++)
        {
            int col = i % cols;
            int row = i / cols;
            Vector2 offset = new Vector2(col * uvScale.X, row * uvScale.Y);
            atlas[i] = new Vector4(offset.X, offset.Y, uvScale.X, uvScale.Y);
        }

        return atlas;
    }
    
    public static Matrix4[] CSMMatrices = new Matrix4[4];

    public static bool UseInstancedShadows = true;
    public static void RenderCascadedShadowMaps(Light light, bool shouldRender)
    {
        CSMMatrices = Light.GenerateCascadedShadowMatrices(ActiveCamera, light, ShadowResolution);

        int index = 0;
        using (new Profiler($"{light.Name} Cascade {index} Shadowmap Rendering"))
        {
            if (!UseInstancedShadows)
            {

                for (int i = 0; i < light.CascadeCount; i++)
                {
                    index++;

                    BindPointShadowMap(i);
                    GL.Viewport(0, 0, ShadowResolution, ShadowResolution);
                    GL.Clear(ClearBufferMask.DepthBufferBit);
                    ShaderManager.LoadShader("shadowgen", out ShaderFile shader);
                    shader.UseShader();

                    if (shouldRender)
                    {
                        ShadowViewCount++;
                    }
                    foreach (GameObject go in CullRenderables(CSMMatrices[i], shouldRender))
                    {
                        if (shouldRender)
                        {
                            if (go is not Camera && go is not Light)
                            {
                                NumShadowCasters++;
                                shader.SetShaderMatrix4x4("shadowViewProjection", CSMMatrices[i], true);
                                shader.SetShaderMatrix4x4("model", go.Transform.GetWorldMatrix());

                                go.Render(GameObject.RenderMode.ShadowPass);

                            }
                        }
                    }

                    RenderBufferHelpers.Instance?.BindLightingFramebuffer();
                  
                }
            }
            else
            {
                foreach (GameObject go in CullRenderables(CSMMatrices[3], shouldRender))
                {
                    if (shouldRender)
                    {
                        ShadowViewCount += light.CascadeCount;
                        
                        if (UseInstancedShadows == true && go is RenderableMesh mesh)
                        {
                            BindPointShadowMap(0);
                            GL.Viewport(0, 0, ShadowResolution, ShadowResolution);
                            GL.Clear(ClearBufferMask.DepthBufferBit);
                            ShaderManager.LoadShader("shadowgen", out ShaderFile shader);
                            shader.UseShader();
                            shader.SetShaderInt("useInstancing", 1);
                            Vector4[] offsets = BuildCascadeAtlas(light.CascadeCount);
                            shader.SetShaderMatrix4x4("model", go.Transform.GetWorldMatrix());
                            for (int x = 0; x < light.CascadeCount; x++)
                            { 
                                shader.SetShaderVector4($"atlasScaleOffset[{x}]", offsets[x]); 
                                shader.SetShaderMatrix4x4($"shadowInstanceMatrices[{x}]", CSMMatrices[x]);
                            }
                            mesh.RenderInstancedShadows(light);
                           
                        }
                    }
                }
                RenderBufferHelpers.Instance?.BindLightingFramebuffer();
               
            }
        }
    }

    public static List<RenderableMesh> CullSceneByPointLight(Light light)
    {
        List<RenderableMesh> renderables = RenderableMeshes;

        List<RenderableMesh> culledRenderables = new List<RenderableMesh>();

        foreach (RenderableMesh renderable in renderables)
        {
            if (SphereBounds.IntersectsOrTouchesSphere(renderable.Bounds, new SphereBounds(light.Transform.Position, light.Range)))
            {
                culledRenderables.Add(renderable);
            }
        }

        return culledRenderables;
    }
    public static void RenderPointShadowMaps(Light light, bool shouldRender)
    {
        using (new Profiler($"{light.Name} Point Shadowmap Rendering"))
        {
            List<RenderableMesh> preculledRenderables;

            preculledRenderables = CullSceneByPointLight(light);

            ShaderManager.LoadShader("shadowgen", out ShaderFile shader);

            for (int i = 0; i < 6; i++)
            {
                bool shouldRenderView = ActiveCamera.Frustum.ContainsFrustum(new Frustum(light.PointLightViewProjections[i]));
              
                if (shouldRenderView)
                {
                    BindPointShadowMap(i);
                    GL.Viewport(0, 0, PointShadowWidth, PointShadowHeight);
                    GL.Clear(ClearBufferMask.DepthBufferBit);
                    shader.UseShader();

                    ShadowViewCount++;
                    
                    foreach (GameObject go in CullRenderables(light.PointLightViewProjections[i], shouldRenderView, preculledRenderables))
                    {
                        if (shouldRender)
                        {
                            NumShadowCasters++;
                            shader.SetShaderMatrix4x4("shadowViewProjection", light.PointLightViewProjections[i]);
                            shader.SetShaderMatrix4x4("model", go.Transform.GetWorldMatrix());
                            go.Render(GameObject.RenderMode.ShadowPass);

                        }
                    }
                }
            }
            RenderBufferHelpers.Instance?.BindLightingFramebuffer();
         
        }
    }

    public static void RenderDebugModels()
    {
        foreach (Camera cam in GameObjects.OfType<Camera>())
        {
            cam.Render();
        }
    }

    public static float previousExposure = 1.0f;
    public static float currentExposure = 10.0f;
    public static void RenderAutoExposure()
    {
        var gb = RenderBufferHelpers.Instance; // nice singleton pattern, but only because I'm lazy and can't just use static for some reason, not exactly sure why just bad design I guess...
        int width, height, id = -1;
        if (gb == null)
        {
            throw new Exception("GBuffer not initialized");
        }

        width = gb.Width;
        height = gb.Height;
        id = gb.GetLightingBufferID();

        previousExposure = currentExposure;

#if MAC
        currentExposure = 32.0f;
#elif WINDOWS
        currentExposure = AutoExposureCompute.RenderAutoExposureCompute(width, height, id, previousExposure, 0.07f);
        //Console.WriteLine("AutoExposure: " + currentExposure);
#endif

    }

    static bool ShadowCasterIntersectsLight(RenderableMesh renderableMesh, Matrix4 lightViewProjection)
    {
        Frustum frustum = new Frustum(lightViewProjection);

        return frustum.ContainsSphere(renderableMesh.Bounds);
    }
    
    public static AtlasTileInfo CalculateAtlasTile(int atlasIndex, int atlasResolution, int tileSize)
    {
        int tilesPerSide = atlasResolution / tileSize;
        int row = atlasIndex / tilesPerSide;
        int col = atlasIndex % tilesPerSide;
        
        Vector2 atlasOffset = new Vector2(
            (float)col / tilesPerSide,
            (float)row / tilesPerSide
        );
        
        Vector2 atlasScale = new Vector2(
            1.0f / tilesPerSide,
            1.0f / tilesPerSide
        );
        
        return new AtlasTileInfo(atlasOffset, atlasScale, tileSize);
    }
    
    private static int CalculateOptimalTileSize(int atlasResolution, int totalTiles)
    {
        if (totalTiles <= 0) return atlasResolution;
        
        // Calculate square grid to fit all tiles
        int tilesPerSide = (int)Math.Ceiling(Math.Sqrt(totalTiles));
        int tileSize = atlasResolution / Math.Max(1, tilesPerSide);
        
        // Ensure tile size is at least 32x32 and power of 2 for better performance
        tileSize = Math.Max(32, tileSize);
        
        // Round down to nearest power of 2
        int powerOf2 = 1;
        while (powerOf2 * 2 <= tileSize)
            powerOf2 *= 2;
            
        return powerOf2;
    }

    private static List<ShadowCasterInstance> shadowCasterInstances = new List<ShadowCasterInstance>();
    public static void BuildShadowCasterInstances()
    {
        shadowCasterInstances.Clear();
        
        List<Light> lights = GameObjects.OfType<Light>().ToList();

        int numLights = lights.Count;
        int lightIndex = 0;
        
        foreach (Light light in lights)
        {
            bool shouldRender = false;

            if (light.Type == Light.LightType.Point)
            {
                SphereBounds bounds = new SphereBounds(light.Transform.Position, light.Range);
                shouldRender = ActiveCamera.Frustum.ContainsSphere(bounds);
            }
            else if (light.Type == Light.LightType.Spot || light.Type == Light.LightType.Projector)
            {
                shouldRender = ActiveCamera.Frustum.ContainsFrustum(new Frustum(light.ViewProjectionMatrix));
            }

            if (light.EnableShadows && shouldRender)
            {
                var atlasSettings = CalculateAtlasTile(lightIndex, ShadowResolution, currentAtlasTileSize);

                List<RenderableMesh> pruned = CullSceneByPointLight(light);
                
                if (light.Type == Light.LightType.Point)
                {
                    foreach (RenderableMesh caster in pruned)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            if (ShadowCasterIntersectsLight(caster, light.PointLightViewProjections[i]))
                            {
                                ShadowCasterInstance instance = new ShadowCasterInstance
                                {
                                    ModelMatrix = caster.Transform.GetWorldMatrix(),
                                    LightViewProjection = light.PointLightViewProjections[i],
                                    AtlasOffset = atlasSettings.atlasOffset,
                                    AtlasScale = atlasSettings.atlasScale,
                                    TileSize = atlasSettings.tileSizeInPixels,
                                };
                                
                                shadowCasterInstances.Add(instance);
                            }
                        }
                    }
                    lightIndex += 6;
                }
                else if (light.Type == Light.LightType.Spot)
                {
                    foreach (RenderableMesh caster in pruned)
                    {
                        if (ShadowCasterIntersectsLight(caster, light.ViewProjectionMatrix))
                        {
                            ShadowCasterInstance instance = new ShadowCasterInstance
                            {
                                Mesh = caster.mesh,
                                ModelMatrix = caster.Transform.GetWorldMatrix(),
                                LightViewProjection = light.ViewProjectionMatrix,
                                AtlasOffset = atlasSettings.atlasOffset,
                                AtlasScale = atlasSettings.atlasScale,
                                TileSize = atlasSettings.tileSizeInPixels,
                            };
                            
                            shadowCasterInstances.Add(instance);
                        }
                    }
                    lightIndex++;
                }
            }
         
        }
        UpdateInstanceBufferOnGPU();
    }

    public static void UpdateInstanceBufferOnGPU()
    {
        
    }
    
    public static void RenderLights()
    {
        EditorWorldIconManager.ResetCounter();
        
        if (UseInstancedShadows)
        {
            // Build instanced shadow data once per frame
            BuildInstancedShadowData();
            
            // Render all shadows with instancing
            if (EnableShadows)
            {
                RenderInstancedShadows();
            }
            
            // Render individual lights for lighting calculations ONLY (shadows handled by instancing)
            foreach (GameObject go in GameObjects)
            {
                if (go is Light)
                {
                    Light? light = go as Light;
                    SphereBounds bounds = new SphereBounds(light.Transform.Position, light.Range * 2);

                    bool shouldRender = false;

                    if (light.Type == Light.LightType.Point)
                    {
                        shouldRender = ActiveCamera.Frustum.ContainsSphere(bounds.Centre, bounds.Radius);
                    }
                    else if (light.Type == Light.LightType.Spot || light.Type == Light.LightType.Projector)
                    {
                        shouldRender = ActiveCamera.Frustum.ContainsFrustum(new Frustum(light.ViewProjectionMatrix));
                    }
                    else if (light.Type == Light.LightType.Directional)
                    {
                        shouldRender = light.IsVisible;
                    }

                    if (shouldRender)
                    {
                        NumberOfVisibleLights++;
                        light.Render(); // Render ONLY the light, not shadows
                    }
                }
            }
        }
        else
        {
            // Original non-instanced rendering path
            foreach (GameObject go in GameObjects)
            {
                if (go is Light)
                {
                    Light? light = go as Light;
                    SphereBounds bounds = new SphereBounds(light.Transform.Position, light.Range*2);

                    bool shouldRender = false;

                    if (light.Type == Light.LightType.Point)
                    {
                        shouldRender = ActiveCamera.Frustum.ContainsSphere(bounds.Centre, bounds.Radius); // stops total triangle count being messed up...
                    }
                    else if (light.Type == Light.LightType.Spot || light.Type == Light.LightType.Projector)
                    {
                        shouldRender = ActiveCamera.Frustum.ContainsFrustum(new Frustum(light.ViewProjectionMatrix));
                    }
                    else if (light.Type == Light.LightType.Directional)
                    {
                        shouldRender = light.IsVisible;
                    }
                    
                    if (light.EnableShadows && EnableShadows)
                    {
                        // Individual shadow rendering (only when instanced shadows are disabled)
                        if (light.Type == Light.LightType.Point)
                        {
                            RenderPointShadowMaps(light, shouldRender);
                        }
                        else if (light.Type == Light.LightType.Spot || light.Type == Light.LightType.Projector)
                        {
                            RenderSpotShadowMap(light, shouldRender);
                        }
                        else if (light.Type == Light.LightType.Directional)
                        {
                            RenderCascadedShadowMaps(light, shouldRender);
                        }
                    }

                    if (shouldRender)
                    {
                        NumberOfVisibleLights++;
                        light.Render();
                    }
                }
            }
        }
    }
    public static void ReadMouseSelection(int mouseStateX, int mouseStateY, out GameObject? gameObject)
    {
        var guid = ReadGUIDFromPickingBuffer(mouseStateX, mouseStateY);
        
        gameObject = null;

        if (GameObjectToGUIDMap.TryGetValue(guid, out GameObject? value))
        {
            gameObject = value;
            var rend = gameObject as RenderableMesh;
            Material mat = new Material();
            if (rend != null)
            {
                mat = rend.Material;
            }
            Console.WriteLine($"Selected {gameObject.Name}. Materials {mat.DiffuseTexture}, {mat.NormalTexture}, {mat.RoughnessTexture}.");
        } 
        else
        {
            Console.Write("Deselected");
            SelectedRenderableObjects.Clear();
        }
    }

    private static void InitializeShadowAtlas()
    {
        // Create shadow atlas texture
        shadowAtlasTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, shadowAtlasTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32f, 
                     ShadowAtlasResolution, ShadowAtlasResolution, 0, 
                     PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
        
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
        
        // Create shadow atlas framebuffer
        shadowAtlasFBO = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, shadowAtlasFBO);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, 
                               TextureTarget.Texture2D, shadowAtlasTexture, 0);
        GL.DrawBuffer(DrawBufferMode.None);
        GL.ReadBuffer(ReadBufferMode.None);
        
        if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
        {
            Console.WriteLine("ERROR: Shadow atlas framebuffer is not complete!");
        }
        
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        
        // Create UBO for instance data with proper std140 layout
        // std140 layout alignment rules:
        // - mat4: 64 bytes (4 vec4s)
        // - vec4: 16 bytes
        // - int in array: padded to 16 bytes per element
        
        int matrixSize = 64;          // mat4 = 64 bytes in std140
        int vectorSize = 16;          // vec4 = 16 bytes
        int intArrayElementSize = 16; // int in array = 16 bytes (padded)
        
        int totalSize = (MAX_SHADOW_INSTANCES_PER_MESH * matrixSize * 2) +      // model + lightViewProjection matrices
                       (MAX_SHADOW_INSTANCES_PER_MESH * vectorSize * 2) +       // atlasScaleOffset + tileBounds vectors
                       (MAX_SHADOW_INSTANCES_PER_MESH * intArrayElementSize * 2); // lightIndex + faceIndex arrays (padded)
        
        shadowInstanceBufferUBO = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.UniformBuffer, shadowInstanceBufferUBO);
        GL.BufferData(BufferTarget.UniformBuffer, totalSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.BindBufferBase(BufferTarget.UniformBuffer, 0, shadowInstanceBufferUBO);
        
        Console.WriteLine($"Shadow atlas initialized: {ShadowAtlasResolution}x{ShadowAtlasResolution}, UBO size: {totalSize} bytes (std140 compliant)");
    }
    
    public static int GetShadowAtlasTexture()
    {
        return shadowAtlasTexture;
    }
    
    public static int GetCurrentAtlasTileSize()
    {
        return currentAtlasTileSize;
    }
    
    private static void BindShadowAtlas()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, shadowAtlasFBO);
    }

    public static void BuildInstancedShadowData()
    {
        if (!UseInstancedShadows)
            return;
            
        using (new Profiler("Build Instanced Shadow Data"))
        {
            shadowInstanceGroups.Clear();
            visibleShadowLights.Clear();
            allShadowInstances.Clear();
            storedShadowMatrices.Clear(); // Clear stored matrices
            
            if (shadowAtlasTexture == -1)
            {
                InitializeShadowAtlas();
            }
            
            // Calculate current tile size based on number of shadow lights
            var allLights = GameObjects.OfType<Light>().Where(l => l.EnableShadows).ToList();
            if (allLights.Count == 0)
            {
                Console.WriteLine("No shadow-casting lights found - skipping shadow generation");
                return;
            }
            
            // Count total tiles needed (point lights need 6, others need 1)
            int totalTiles = 0;
            foreach (var light in allLights)
            {
                if (light.Type == Light.LightType.Point)
                    totalTiles += 6;
                else
                    totalTiles += 1;
            }
            
            // Calculate tile size to fit all lights in atlas
            currentAtlasTileSize = CalculateOptimalTileSize(ShadowAtlasResolution, totalTiles);
            
            int currentAtlasIndex = 0;
            int lightIndex = 0; // Index in visible lights array
            
            foreach (var light in allLights)
            {
                bool shouldRender = false;
                
                // Check if light is visible
                if (light.Type == Light.LightType.Point)
                {
                    SphereBounds bounds = new SphereBounds(light.Transform.Position, light.Range);
                    shouldRender = ActiveCamera.Frustum.ContainsSphere(bounds);
                }
                else if (light.Type == Light.LightType.Spot || light.Type == Light.LightType.Projector)
                {
                    shouldRender = ActiveCamera.Frustum.ContainsFrustum(new Frustum(light.ViewProjectionMatrix));
                }
                else if (light.Type == Light.LightType.Directional)
                {
                    shouldRender = light.IsVisible;
                }
                
                if (!shouldRender) continue;
                
                var lightInfo = new LightShadowInfo
                {
                    Light = light,
                    BaseAtlasIndex = currentAtlasIndex,
                    TileCount = light.Type == Light.LightType.Point ? 6 : 1,
                    IsVisible = true,
                    ShadowCasters = CullSceneByPointLight(light)
                };
                
                visibleShadowLights.Add(lightInfo);
                
                // Store shadow matrices for this light
                if (light.Type == Light.LightType.Point)
                {
                    storedShadowMatrices[lightIndex] = light.PointLightViewProjections;
                }
                else
                {
                    storedShadowMatrices[lightIndex] = new Matrix4[] { light.ViewProjectionMatrix };
                }
                
                // Build instances for this light
                BuildInstancesForLight(lightInfo, currentAtlasIndex);
                
                currentAtlasIndex += lightInfo.TileCount;
                lightIndex++;
            }
            
            // Group instances by mesh for efficient rendering
            GroupInstancesByMesh();
        }
    }
    
    private static void BuildInstancesForLight(LightShadowInfo lightInfo, int baseAtlasIndex)
    {
        var light = lightInfo.Light;
        
        if (light.Type == Light.LightType.Point)
        {
            // Point lights need 6 instances (one per face)
            for (int face = 0; face < 6; face++)
            {
                // Don't cull entire faces based on camera visibility - 
                // objects might cast shadows even if the face isn't visible to camera
                var atlasInfo = CalculateAtlasTile(baseAtlasIndex + face, ShadowAtlasResolution, currentAtlasTileSize);
                
                foreach (var caster in lightInfo.ShadowCasters)
                {
                    if (ShadowCasterIntersectsLight(caster, light.PointLightViewProjections[face]))
                    {
                        var instance = new ShadowInstanceData
                        {
                            ModelMatrix = caster.Transform.GetWorldMatrix(),
                            LightViewProjection = light.PointLightViewProjections[face],
                            AtlasScaleOffset = new Vector4(atlasInfo.atlasScale.X, atlasInfo.atlasScale.Y, 
                                                         atlasInfo.atlasOffset.X, atlasInfo.atlasOffset.Y),
                            TileBounds = new Vector4(atlasInfo.atlasOffset.X, atlasInfo.atlasOffset.Y,
                                                   atlasInfo.atlasOffset.X + atlasInfo.atlasScale.X,
                                                   atlasInfo.atlasOffset.Y + atlasInfo.atlasScale.Y),
                            LightIndex = visibleShadowLights.Count - 1,
                            FaceIndex = face
                        };
                        
                        allShadowInstances.Add(instance);
                        
                        if (!shadowInstanceGroups.ContainsKey(caster))
                            shadowInstanceGroups[caster] = new List<ShadowInstanceData>();
                        shadowInstanceGroups[caster].Add(instance);
                    }
                }
            }
        }
        else
        {
            // Spot/Directional lights need only 1 instance
            var atlasInfo = CalculateAtlasTile(baseAtlasIndex, ShadowAtlasResolution, currentAtlasTileSize);
            
            foreach (var caster in lightInfo.ShadowCasters)
            {
                Matrix4 lightVP = light.Type == Light.LightType.Directional ? 
                    Light.GenerateCascadedShadowMatrices(ActiveCamera, light, ShadowResolution)[0] : 
                    light.ViewProjectionMatrix;
                    
                if (ShadowCasterIntersectsLight(caster, lightVP))
                {
                    var instance = new ShadowInstanceData
                    {
                        ModelMatrix = caster.Transform.GetWorldMatrix(),
                        LightViewProjection = lightVP,
                        AtlasScaleOffset = new Vector4(atlasInfo.atlasScale.X, atlasInfo.atlasScale.Y, 
                                                     atlasInfo.atlasOffset.X, atlasInfo.atlasOffset.Y),
                        TileBounds = new Vector4(atlasInfo.atlasOffset.X, atlasInfo.atlasOffset.Y,
                                               atlasInfo.atlasOffset.X + atlasInfo.atlasScale.X,
                                               atlasInfo.atlasOffset.Y + atlasInfo.atlasScale.Y),
                        LightIndex = visibleShadowLights.Count - 1,
                        FaceIndex = -1
                    };
                    
                    allShadowInstances.Add(instance);
                    
                    if (!shadowInstanceGroups.ContainsKey(caster))
                        shadowInstanceGroups[caster] = new List<ShadowInstanceData>();
                    shadowInstanceGroups[caster].Add(instance);
                }
            }
        }
    }
    
    private static void GroupInstancesByMesh()
    {
        // Instances are already grouped by mesh in shadowInstanceGroups
        // This function can be used for additional optimizations if needed
    }
    
    public static void RenderInstancedShadows()
    {
        if (!UseInstancedShadows || allShadowInstances.Count == 0)
            return;
            
        using (new Profiler("Render Instanced Shadows"))
        {
            BindShadowAtlas();
            GL.Viewport(0, 0, ShadowAtlasResolution, ShadowAtlasResolution);
            GL.Clear(ClearBufferMask.DepthBufferBit);
            
            ShaderManager.LoadShader("shadowgen_instanced", out ShaderFile shader);
            shader.UseShader();
            
            shader.SetShaderInt("useInstancedRendering", 1);
            shader.SetShaderInt("maxInstances", MAX_SHADOW_INSTANCES_PER_MESH);
            
            int totalInstancesRendered = 0;
            int totalDrawCalls = 0;
            
            // Render each unique mesh with all its instances
            foreach (var kvp in shadowInstanceGroups)
            {
                var mesh = kvp.Key;
                var instances = kvp.Value;
                
                if (instances.Count == 0) continue;
                
                // Process instances in batches to respect OpenGL limits
                for (int batchStart = 0; batchStart < instances.Count; batchStart += MAX_SHADOW_INSTANCES_PER_MESH)
                {
                    int batchSize = Math.Min(MAX_SHADOW_INSTANCES_PER_MESH, instances.Count - batchStart);
                    var batchInstances = instances.Skip(batchStart).Take(batchSize).ToArray();
                    
                    // Create separate arrays for the UBO layout
                    Matrix4[] modelMatrices = new Matrix4[batchSize];
                    Matrix4[] lightViewProjections = new Matrix4[batchSize];
                    Vector4[] atlasScaleOffsets = new Vector4[batchSize];
                    Vector4[] tileBounds = new Vector4[batchSize];
                    int[] lightIndices = new int[batchSize];
                    int[] faceIndices = new int[batchSize];
                    
                    for (int i = 0; i < batchSize; i++)
                    {
                        modelMatrices[i] = batchInstances[i].ModelMatrix;
                        lightViewProjections[i] = batchInstances[i].LightViewProjection;
                        atlasScaleOffsets[i] = batchInstances[i].AtlasScaleOffset;
                        tileBounds[i] = batchInstances[i].TileBounds;
                        lightIndices[i] = batchInstances[i].LightIndex;
                        faceIndices[i] = batchInstances[i].FaceIndex;
                    }
                    
                    // Upload data to UBO in the correct std140 layout
                    GL.BindBuffer(BufferTarget.UniformBuffer, shadowInstanceBufferUBO);
                    
                    int offset = 0;
                    int matrixSize = 64;          // mat4 = 64 bytes in std140
                    int vectorSize = 16;          // vec4 = 16 bytes
                    int intArrayElementSize = 16; // int in array = 16 bytes (padded)
                    
                    // Clear the entire UBO first to prevent stale data
                    byte[] clearData = new byte[MAX_SHADOW_INSTANCES_PER_MESH * (matrixSize * 2 + vectorSize * 2 + intArrayElementSize * 2)];
                    GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, clearData.Length, clearData);
                    
                    // Reset offset after clearing
                    offset = 0;
                    
                    // Upload model matrices
                    for (int i = 0; i < batchSize; i++)
                    {
                        float[] matrixData = new float[16];
                        var matrix = modelMatrices[i];
                        for (int j = 0; j < 16; j++)
                            matrixData[j] = matrix[j / 4, j % 4];
                        GL.BufferSubData(BufferTarget.UniformBuffer, (IntPtr)(offset + i * matrixSize), 64, matrixData);
                    }
                    offset += MAX_SHADOW_INSTANCES_PER_MESH * matrixSize;
                    
                    // Upload light view projection matrices
                    for (int i = 0; i < batchSize; i++)
                    {
                        float[] matrixData = new float[16];
                        var matrix = lightViewProjections[i];
                        for (int j = 0; j < 16; j++)
                            matrixData[j] = matrix[j / 4, j % 4];
                        GL.BufferSubData(BufferTarget.UniformBuffer, (IntPtr)(offset + i * matrixSize), 64, matrixData);
                    }
                    offset += MAX_SHADOW_INSTANCES_PER_MESH * matrixSize;
                    
                    // Upload atlas scale offset vectors
                    for (int i = 0; i < batchSize; i++)
                    {
                        float[] vectorData = { atlasScaleOffsets[i].X, atlasScaleOffsets[i].Y, atlasScaleOffsets[i].Z, atlasScaleOffsets[i].W };
                        GL.BufferSubData(BufferTarget.UniformBuffer, (IntPtr)(offset + i * vectorSize), vectorSize, vectorData);
                    }
                    offset += MAX_SHADOW_INSTANCES_PER_MESH * vectorSize;
                    
                    // Upload tile bounds vectors
                    for (int i = 0; i < batchSize; i++)
                    {
                        float[] vectorData = { tileBounds[i].X, tileBounds[i].Y, tileBounds[i].Z, tileBounds[i].W };
                        GL.BufferSubData(BufferTarget.UniformBuffer, (IntPtr)(offset + i * vectorSize), vectorSize, vectorData);
                    }
                    offset += MAX_SHADOW_INSTANCES_PER_MESH * vectorSize;
                    
                    // Upload light indices (padded to 16 bytes per element for std140)
                    for (int i = 0; i < batchSize; i++)
                    {
                        int[] paddedInt = { lightIndices[i], 0, 0, 0 }; // Pad to 16 bytes (4 ints)
                        GL.BufferSubData(BufferTarget.UniformBuffer, (IntPtr)(offset + i * intArrayElementSize), intArrayElementSize, paddedInt);
                    }
                    offset += MAX_SHADOW_INSTANCES_PER_MESH * intArrayElementSize;
                    
                    // Upload face indices (padded to 16 bytes per element for std140)
                    for (int i = 0; i < batchSize; i++)
                    {
                        int[] paddedInt = { faceIndices[i], 0, 0, 0 }; // Pad to 16 bytes (4 ints)
                        GL.BufferSubData(BufferTarget.UniformBuffer, (IntPtr)(offset + i * intArrayElementSize), intArrayElementSize, paddedInt);
                    }
                    
                    shader.SetShaderInt("instanceCount", batchSize);
                    shader.SetShaderInt("tileSize", currentAtlasTileSize);
                    shader.SetShaderInt("atlasResolution", ShadowAtlasResolution);
                    
                    // Set material properties
                    if (mesh.Material.DiffuseTexture != -1)
                    {
                        GL.ActiveTexture(TextureUnit.Texture0);
                        GL.BindTexture(TextureTarget.Texture2D, mesh.Material.DiffuseTexture);
                        shader.SetShaderInt("diffuseTexture", 0);
                        shader.SetShaderFloat("hasDiffuse", 1.0f);
                    }
                    else
                    {
                        shader.SetShaderFloat("hasDiffuse", 0.0f);
                    }
                    
                    // Render instanced
                    RenderableMesh.BindVAOCached(mesh.mesh.ShadowMesh.Vao);
                    GL.DrawElementsInstanced(PrimitiveType.Triangles, mesh.mesh.ShadowMesh.IndexCount, 
                                           DrawElementsType.UnsignedInt, IntPtr.Zero, batchSize);
                    
                    DrawCalls++;
                    NumShadowCasters += batchSize;
                    totalInstancesRendered += batchSize;
                    totalDrawCalls++;
                }
            }
            
            // Debug output for performance tracking
            if (totalInstancesRendered > 0)
            {
                Console.WriteLine($"Instanced Shadows: {totalInstancesRendered} instances in {totalDrawCalls} draw calls " +
                                $"(vs {totalInstancesRendered} individual calls). Efficiency: {(float)totalInstancesRendered / totalDrawCalls:F1}x");
            }
            
            RenderBufferHelpers.Instance?.BindLightingFramebuffer();
        }
    }

    public static Matrix4 GetStoredShadowMatrix(int lightIndex, int faceIndex)
    {
        if (storedShadowMatrices.ContainsKey(lightIndex) && 
            faceIndex >= 0 && faceIndex < storedShadowMatrices[lightIndex].Length)
        {
            return storedShadowMatrices[lightIndex][faceIndex];
        }
        
        // Fallback: recompute from visible lights if available
        if (lightIndex >= 0 && lightIndex < visibleShadowLights.Count)
        {
            var light = visibleShadowLights[lightIndex].Light;
            if (light.Type == Light.LightType.Point)
            {
                return light.PointLightViewProjections[faceIndex];
            }
            else
            {
                return light.ViewProjectionMatrix;
            }
        }
        
        return Matrix4.Identity;
    }

    public static bool TryGetLightShadowInfo(Light light, out LightShadowInfo shadowInfo, out int listIndex)
    {
        for (int i = 0; i < visibleShadowLights.Count; i++)
        {
            if (ReferenceEquals(visibleShadowLights[i].Light, light))
            {
                shadowInfo = visibleShadowLights[i];
                listIndex = i;
                return true;
            }
        }

        shadowInfo = default;
        listIndex = -1;
        return false;
    }
}
