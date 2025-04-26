using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using External.ImguiController;
using Geometry;
using Geometry.MaterialSystem;
using ImGuiNET;
using LegendaryRenderer.Application.SceneManagement;
using LegendaryRenderer.Engine.EngineTypes;
using LegendaryRenderer.GameObjects;
using LegendaryRenderer.LegendaryRuntime.Engine.Editor;
using LegendaryRenderer.LegendaryRuntime.Engine.Renderer.MaterialSystem;
using LegendaryRenderer.Shaders;
using OpenTK.Graphics.ES11;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using TheLabs.LegendaryRuntime.Engine.GameObjects;
using TheLabs.LegendaryRuntime.Engine.Renderer;
using BlendingFactor = OpenTK.Graphics.OpenGL.BlendingFactor;
using Buffer = System.Buffer;
using ClearBufferMask = OpenTK.Graphics.OpenGL.ClearBufferMask;
using CullFaceMode = OpenTK.Graphics.OpenGL.CullFaceMode;
using EnableCap = OpenTK.Graphics.OpenGL.EnableCap;
using FramebufferAttachment = OpenTK.Graphics.OpenGL.FramebufferAttachment;
using FramebufferTarget = OpenTK.Graphics.OpenGL.FramebufferTarget;
using GL = OpenTK.Graphics.OpenGL.GL;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;
using PixelInternalFormat = OpenTK.Graphics.OpenGL.PixelInternalFormat;
using PixelType = OpenTK.Graphics.OpenGL.PixelType;
using ShadingRate = OpenTK.Graphics.OpenGL.ShadingRate;
using TextureMinFilter = OpenTK.Graphics.ES11.TextureMinFilter;
using TextureParameterName = OpenTK.Graphics.OpenGL.TextureParameterName;
using TextureTarget = OpenTK.Graphics.OpenGL.TextureTarget;
using TextureWrapMode = OpenTK.Graphics.OpenGL.TextureWrapMode;
using TextureHandle = TheLabs.LegendaryRuntime.Engine.Utilities.GLHelpers.TextureHandle;
using Vector2 = OpenTK.Mathematics.Vector2;
using Vector3 = OpenTK.Mathematics.Vector3;
using Vector4 = OpenTK.Mathematics.Vector4;

namespace LegendaryRenderer.Application;

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

    public static SSAOSettings SSAOSettings = new SSAOSettings();
    
    public static DockspaceController DockspaceController;
    public static EditorViewport EditorViewport;
    public static EditorSceneHierarchyPanel EditorSceneHierarchyPanel;

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

    static Engine()
    {
        Initialize();
    }

    public static void Initialize()
    {
        ShaderManager.LoadShader("basepass", out ShaderFile loaded);
        currentShader = loaded;
        GenerateShadowMap(ShadowResolution, ShadowResolution);
        GeneratePointShadowMaps(ShadowResolution, ShadowResolution);
        RenderBuffers = new RenderBufferHelpers(PixelInternalFormat.Rgba8, PixelInternalFormat.DepthComponent32f, Application.Width, Application.Height, "Main Buffer");
        LoadedScenes.Add(new Scene());
        DockspaceController = new DockspaceController(Application.windowInstance);
        EditorViewport = new EditorViewport(RenderBufferHelpers.Instance.GetTextureHandle(TextureHandle.COPY));
        EditorSceneHierarchyPanel = new EditorSceneHierarchyPanel(LoadedScenes[0]);
        
        
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

        GameObjects.Add(gameObject);
        GameObjectToGUIDMap.Add(ConvertValueToGuid(GuidToUIntArray(gameObject.GUID)), gameObject);
        
        if (gameObject is RenderableMesh)
        {
            RenderableMeshes.Add(gameObject as RenderableMesh);
        }
    }
    public static List<RenderableMesh> CullRenderables(Matrix4 viewProjectionMatrix, bool shouldRender, List<RenderableMesh>? preCulledRenderables = null)
    {
        using (new ScopedProfiler($"Scene Culling {CullPass++}"))
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

    private static void DoSelection()
    {
        if(ShouldDoSelectionNextFrame)
        {
            ShouldDoSelectionNextFrame = false;
            Engine.RenderSelectionBufferOnce();

            Engine.ReadMouseSelection((int)EditorViewport.MouseFramebufferPosition.X,  (int) EditorViewport.ViewportSize.Y - (int) EditorViewport.MouseFramebufferPosition.Y, out GameObject? hit);
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
                    if (IsMultiSelect)
                    {
                        SelectedRenderableObjects.Add(hit);
                    }
                    else
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

        using (new ScopedProfiler("Scene Rendering (Total)"))
        {
            EditorViewport.SetFramebufferID(RenderBuffers.GetTextureHandle(TextureHandle.COPY));
            FullscreenQuad.RenderQuad("AtmosphericSky", new[] { 0 }, new[] { "null" });

            RenderBuffers.BindGBuffer();

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit |
                     ClearBufferMask.StencilBufferBit);


            using (new ScopedProfiler("Render to GBuffer"))
            {
                Engine.RenderGBufferModels();
            }

            RenderBufferHelpers.Instance?.BindMainOutputBuffer();

            RenderBuffers.GetTextureIDs(out int[] textures);


            int lighting = RenderBuffers.GetLightingBufferID();

            using (new ScopedProfiler("Render Lights"))
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

            DoSelection();

            RenderBufferHelpers.Instance?.BindMainOutputBuffer();

            RenderAutoExposure();

            using (new ScopedProfiler("Motion Blur"))
            {
                FullscreenQuad.RenderQuad("MotionBlur", new[] { lighting, textures[3], textures[1] }, new[] { "sourceTexture", "velocityTexture", "depthTexture" });
            }


            // TODO: ugly shit like this copy code should be refactored into neat postprocess chaining auto copy
            RenderBufferHelpers.Instance?.BindLightingFramebuffer();
            int x = RenderBufferHelpers.Instance.GetTextureHandle(TextureHandle.COPY);
            FullscreenQuad.RenderQuad("Blit", new[] { x }, new[] { "sourceTexture" });


            using (new ScopedProfiler("Outline (Editor)"))
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

            RenderImGui();
        }
    }

    public static void RenderImGui()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        DockspaceController.BeginDockspace();
        EditorViewport.Draw();
        EditorViewport.ApplyPendingResize();
        EditorSceneHierarchyPanel.Draw();
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


        Console.WriteLine("Guid: " + new Guid(guidBytes));
        
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


        Console.WriteLine("Guid: " + new Guid(guidBytes));

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
        }
    }

    private static void GeneratePointShadowMaps(int width, int height)
    {
        if (PointShadowWidth != width || PointShadowHeight != height || !PointShadowsCreated)
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
        using (new ScopedProfiler($"{light.Name} Shadowmap Rendering"))
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

    public static bool UseInstancedShadows = false;
    public static void RenderCascadedShadowMaps(Light light, bool shouldRender)
    {
        CSMMatrices = Light.GenerateCascadedShadowMatrices(ActiveCamera, light, ShadowResolution);

        int index = 0;
        using (new ScopedProfiler($"{light.Name} Cascade {index} Shadowmap Rendering"))
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
        using (new ScopedProfiler($"{light.Name} Point Shadowmap Rendering"))
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
    
    public static (Vector2 atlasOffset, Vector2 atlasScale, int tileSizeInPixels) CalculateAtlasTile(int lightIndex, int numberOfLights, int atlasResolution)
    {
        // Compute the number of tiles along one dimension.
        int tileCount = (int)Math.Ceiling(Math.Sqrt(numberOfLights));
    
        // Compute the x and y grid coordinates for the light.
        int x = lightIndex % tileCount;
        int y = lightIndex / tileCount;
    
        // In normalized atlas coordinates (0 to 1).
        Vector2 atlasOffset = new Vector2((float)x / tileCount, (float)y / tileCount);
        Vector2 atlasScale = new Vector2(1f / tileCount, 1f / tileCount);
    
        // Additionally, compute the size of each tile in pixels.
        int tileSizeInPixels = atlasResolution / tileCount;
    
        return (atlasOffset, atlasScale, tileSizeInPixels);
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
                var atlasSettings = CalculateAtlasTile(lightIndex, numLights, ShadowResolution);

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
        // this is the old light rendering code
        foreach (GameObject go in GameObjects)
        {
            if (go is Light)
            {
                Light? light = go as Light;
                SphereBounds bounds = new SphereBounds(light.Transform.Position, light.Range);

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
                
                if (light.EnableShadows)
                {
                 //   GL.CullFace(OpenTK.Graphics.OpenGL.CullFaceMode.Front);
                    
                    if (light.Type == Light.LightType.Spot || light.Type == Light.LightType.Projector)
                    {
                        RenderSpotShadowMap(light, shouldRender);
                    }
                    else if (light.Type == Light.LightType.Point)
                    {
                        RenderPointShadowMaps(light, shouldRender);
                    }
                    else if (light.Type == Light.LightType.Directional)
                    {
                        RenderCascadedShadowMaps(light, shouldRender);
                    }
                    
                  //  GL.CullFace(OpenTK.Graphics.OpenGL.CullFaceMode.Back);
                }

                using (new ScopedProfiler($"{light.Name} Render"))
                {
                    if (shouldRender)
                    {
                        go.Render();
                        Engine.NumberOfVisibleLights++;
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
}
