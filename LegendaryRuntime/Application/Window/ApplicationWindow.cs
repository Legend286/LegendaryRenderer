using ImGuiNET;
using LegendaryRenderer.Application;
using LegendaryRenderer.LegendaryRuntime.Application.Profiling;
using LegendaryRenderer.LegendaryRuntime.Engine.Editor.Dockspace;
using LegendaryRenderer.LegendaryRuntime.Engine.Editor.Gizmos;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MaterialSystem;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.ModelLoader;
using LegendaryRenderer.LegendaryRuntime.Engine.Renderer.MaterialSystem;
using LegendaryRenderer.Shaders;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using static LegendaryRenderer.LegendaryRuntime.Engine.Utilities.Maths;
using LegendaryRenderer.LegendaryRuntime.Engine.AssetManagement;
using Assimp;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MeshInstancing;
using Camera = LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects.Camera;
using Light = LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects.Light;
using Quaternion = OpenTK.Mathematics.Quaternion;
using LegendaryRenderer.LegendaryRuntime.Engine.Editor;
using LegendaryRenderer.LegendaryRuntime.Engine.Editor.Dockspace; // Assuming DockLayoutManager might be relevant for menu

// Imgui controller (see ImGuiController.cs for notice //
using Vector2 = System.Numerics.Vector2;

namespace LegendaryRenderer.LegendaryRuntime.Application;



public class ApplicationWindow : GameWindow
{
    public static KeyboardState keyboardState;
    public static MouseState mouseState;
    private ImGuiController imguiController;
   
    Vector2 DPIScale = new Vector2(1, 1);

    public ApplicationWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings)
    {
        imguiController = new ImGuiController(Application.Width, Application.Height);

        keyboardState = KeyboardState;
        mouseState = MouseState;
        GL.Enable(EnableCap.DebugOutput);
        GL.Enable(EnableCap.DebugOutputSynchronous);
        
        var windowSizeInPoints      = new Vector2(Size.X, Size.Y);           // ImGui points
        var framebufferSizeInPixels = new Vector2(Application.Width, Application.Height);

        var dpiScale =  framebufferSizeInPixels / windowSizeInPoints;
        Console.WriteLine($"DPI Scale = {dpiScale}");

        DPI.SetDPIScale(FromNumericsVector2(dpiScale));


        var io = ImGui.GetIO();
        io.DisplaySize             = windowSizeInPoints;
        io.DisplayFramebufferScale = dpiScale;
        // 🛠 NEW: scale fonts down if needed
        io.FontGlobalScale = 1.0f / dpiScale.Y;

    }

    public bool IsMouseVisible
    {
        get { return CursorState == CursorState.Hidden; }
        set { CursorState = value ? CursorState.Normal : CursorState.Grabbed; }
    }

    RenderableMesh _renderableMesh;

    RenderableMesh[] instances = new RenderableMesh[100];

    private Camera camera;
    private GameObject model;
    private GameObject model2;
    private GameObject model3;
    public static List<Light> lights = new List<Light>();
    private int numLights = 0;

    string[] modelExtensions = new string[] { ".fbx", ".gltf", ".glb", ".obj", ".objc", ".objd" };
    string[] textureExtensions = new string[] { ".png", ".jpg", ".jpeg", ".tif" };

    // Returns 0 if diffuse, 1 if normal, 2 if metallic / rough / mask
    public int GetTargetFromFile(string fileName)
    {
        string name = fileName.ToLower();
        if (name.Contains("diffuse") || name.Contains("base") || name.Contains("albedo") || name.Contains("color") || name.Contains("colour") || name.Contains("_d"))
        {
            return 0;
        }
        if (name.Contains("normal") || name.Contains("_n"))
        {
            return 1;
        }
        if (name.Contains("roughness") || name.Contains("mask") || name.Contains("metallic"))
        {
            return 2;
        }

        return -1;
    }
    
    bool ContainsModel(string fileName)
    {
        return modelExtensions.Contains(Path.GetExtension(fileName).ToLower());
    }
    
    bool ContainsTexture(string fileName)
    {
        return textureExtensions.Contains(Path.GetExtension(fileName).ToLower());
    }

    private RenderableMesh? droppedMesh;
    private string droppedFile;
    private int droppedTextureID = -1;
    
    protected override void OnFileDrop(FileDropEventArgs e)
    {
        var fileName = e.FileNames[0];
        // Ensure AssetCacheManager is initialized (might be better in OnLoad, but here for safety)
        AssetCacheManager.EnsureInitialized(); 

        var mouseState = ApplicationWindow.mouseState;

        var pos = new Vector2(mouseState.Position.X, Application.Height - mouseState.Position.Y);
        
        if (e.FileNames.Length > 1)
        {
            Console.WriteLine("Please for now only try to import one asset at once.");
        }
        else if (ImGui.IsAnyItemHovered())
        {
            DraggedOnImGui = true;

            if (ContainsTexture(fileName))
            {
                droppedTextureID = TextureLoader.LoadTexture(fileName, false).Reference().GetGLTexture();
            }
        }
        else
        {
            if (ContainsModel(fileName))
            {
                // Modified model loading path
                LoadModelFromDragWithCaching(fileName);
            }
            else if (ImGui.IsAnyItemHovered())
            {
                DraggedOnImGui = true;
            }
            else if (ContainsTexture(fileName))
            {
                LegendaryRuntime.Engine.Engine.Engine.RenderSelectionBufferOnce();

                LegendaryRuntime.Engine.Engine.Engine.ReadMouseSelection((int)pos.X, (int)pos.Y, out GameObject target);

                if (target is RenderableMesh mesh)
                {
                    int targetIndex = GetTargetFromFile(fileName);

                    if (targetIndex != -1)
                    {
                        if (targetIndex == 0)
                        {
                            mesh.Material.DiffuseTexture = TextureLoader.LoadTexture(fileName, false).Reference().GetGLTexture();
                        }
                        if (targetIndex == 1)
                        {
                            mesh.Material.NormalTexture = TextureLoader.LoadTexture(fileName, false).Reference().GetGLTexture();
                        }
                        if (targetIndex == 2)
                        {
                            mesh.Material.RoughnessTexture = TextureLoader.LoadTexture(fileName, false).Reference().GetGLTexture();
                        }
                    }
                    else
                    {
                        droppedFile = fileName;
                        droppedMesh = mesh;
                        shouldShowPopup = true;
                        initial = true;
                    }
                }
                
            }
        }
    }
    public bool DraggedOnImGui { get; set; }
    private Light l1;
    
    protected override void OnLoad()
    {
        AssetCacheManager.EnsureInitialized(); // Initialize AssetCacheManager on load
        IconGenerator.Initialize(); // Initialize IconGenerator
      
        var loading = new ScopedProfiler("Loading Phase");

        /*Light l3 = new Light(Vector3.Zero, "Light Test");
        l3.Type = Light.LightType.Directional;
        l3.EnableShadows = true;
        l3.Colour = Color4.Yellow;
        l3.Intensity = 3.14f;
        l3.Range = 100.0f;
        l3.InnerCone = 50.0f;
        l3.OuterCone = 65.0f;
        l3.CascadeCount = 4;
        lights.Add(l3);*/
        loading.StartTimingCPU();

        base.OnLoad();
        
       // DockLayoutManager.LoadLayoutFromDisk();
        
        GL.ClearColor(Color4.Black);
        PrintDebugLogInfo();

        GL.DepthRange(1, 0);
        GL.Disable(EnableCap.FramebufferSrgb);
        GL.DepthFunc(DepthFunction.Lequal);
        
        
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(CullFaceMode.Back);

        camera = new Camera(Vector3.One, Vector3.Zero, 45.0f);
        camera.Transform.LocalRotation = Rotation(-45, 45, 0);
        
        Color4[] colours = new[]
        {
            Color4.Red,
            Color4.Green,
            Color4.Blue,
            Color4.Cyan,
            Color4.Yellow,
            Color4.Purple,
        };

        var profile = IESProfile.Load("fancyprofile.ies");

        for (int i = 0; i < numLights; i++)
        {
            var light = new Light(new Vector3(MathF.Cos((float)i / numLights) * 4, 1, MathF.Sin((float)i / numLights) * 4), "Light DEF");
            light.Transform.LocalRotation *= Quaternion.FromEulerAngles(MathHelper.DegreesToRadians(0), MathHelper.DegreesToRadians((360 / numLights) * i), 0);
            light.Transform.Position = light.Transform.Forward * 2;
            light.Colour = colours[i % 6];
            light.Range = 10.0f;
            light.Type = Light.LightType.Spot;
            light.OuterCone = 90.0f;
            light.InnerCone = 20.0f;
            light.Intensity = 800.0f;
            light.LightIESProfile = profile;
            light.EnableShadows = true;
            lights.Add(light);
        }

        l1 = new Light(new Vector3(0, 0, -0.25f), "Camera Light");

        l1.Colour = Color4.White;
        l1.Intensity = 50.0f;
        l1.Range = 1000.0f;
        l1.OuterCone = 80;
        l1.InnerCone = 60;
        l1.EnableShadows = true;
        l1.Type = Light.LightType.Spot;
        l1.LightIESProfile = profile;
      
        // light2.Colour = Color.Green;
        // light3.Colour = Color.Blue;
        //   light3.Transform.Rotation = Quaternion.FromEulerAngles(MathHelper.DegreesToRadians(-35), 0, 0);
        //  var light4 = new Light(new Vector3(0, 5, 5), "Light QRT");
        // light4.Transform.Rotation = Quaternion.FromEulerAngles(MathHelper.DegreesToRadians(-90), 0, 0);

        //camera.AddChild(l1);

        // Set the main camera as the active camera for the engine
        LegendaryRuntime.Engine.Engine.Engine.ActiveCamera = camera;

        // HACK
        var time = loading.StopTimingCPU() / 1000;

        Console.WriteLine($"Loaded Application Successfully. It took {time.ToString("0.00")} seconds to load everything.");
    }

    protected override void OnUnload()
    {
        ShaderManager.Dispose();

        base.OnUnload();
    }

    protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
    {
        base.OnFramebufferResize(e);
        Application.Width = e.Width;
        Application.Height = e.Height;
        GL.Viewport(0, 0, e.Width, e.Height);
        imguiController.WindowResized(e.Width, e.Height);
        
        var windowSizeInPoints      = new Vector2(Size.X, Size.Y);           // ImGui points
        var framebufferSizeInPixels = new Vector2(Application.Width, Application.Height);

        var dpiScale =  framebufferSizeInPixels / windowSizeInPoints;
        Console.WriteLine($"DPI Scale = {dpiScale}");

        DPI.SetDPIScale(FromNumericsVector2(dpiScale));


        var io = ImGui.GetIO();
        io.DisplaySize             = windowSizeInPoints;
        io.DisplayFramebufferScale = dpiScale;
        // 🛠 NEW: scale fonts down if needed
        io.FontGlobalScale = 1.0f / dpiScale.Y;
    }

    void ResetCounters()
    {
        LegendaryRuntime.Engine.Engine.Engine.TriangleCountTotal = 0;
        LegendaryRuntime.Engine.Engine.Engine.TriangleCountCulled = 0;
        LegendaryRuntime.Engine.Engine.Engine.TriangleCountRendered = 0;
        LegendaryRuntime.Engine.Engine.Engine.NumShadowCasters = 0;
        LegendaryRuntime.Engine.Engine.Engine.DrawCalls = 0;
        LegendaryRuntime.Engine.Engine.Engine.CullPass = 0;
        LegendaryRuntime.Engine.Engine.Engine.NumberOfVisibleLights = 0;
        LegendaryRuntime.Engine.Engine.Engine.ShadowViewCount = 0;
        Frustum.Count = 0;
        RenderableMesh.ReusedCounter = 0;
        RenderableMesh.TotalSceneMeshes = 0;
        RenderableMesh.LoadedMeshCount = 0;
    }

    private Gizmos.GizmoMode mode = Gizmos.GizmoMode.Translate;
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        ScopedProfiler.ResetStats();
     

        ResetCounters();
        
        LegendaryRuntime.Engine.Engine.Engine.EngineRenderLoop();
        
       
        var size = FromNumericsVector2(LegendaryRuntime.Engine.Engine.Engine.EditorViewport.ViewportSize);
        var vpPos = LegendaryRuntime.Engine.Engine.Engine.EditorViewport.ViewportPosition;
        // Mouse inside viewport, relative to ViewportPosition
        var io = ImGui.GetIO();

        var mousePosGlobal = FromNumericsVector2(io.MousePos);

        var viewPos = FromNumericsVector2(new Vector2(vpPos.X, vpPos.Y));

        if (LegendaryRuntime.Engine.Engine.Engine.SelectedRenderableObjects.Count == 1)
        {
            Gizmos.DrawAndHandle(ref LegendaryRuntime.Engine.Engine.Engine.SelectedRenderableObjects[0].GetRoot().Transform, ref LegendaryRuntime.Engine.Engine.Engine.ActiveCamera, ref mousePosGlobal, ref size, ref viewPos, mode);
        }

        foreach (GameObject go in LegendaryRuntime.Engine.Engine.Engine.SelectedRenderableObjects)
        {
            var light = go as Light;
            if (light == null) continue;
            if (light.Type == Light.LightType.Spot)
            {
                Gizmos.DrawSpotLightCone(LegendaryRuntime.Engine.Engine.Engine.ActiveCamera, light, size, viewPos);
            }
            if (light.Type == Light.LightType.Point)
            {
                Gizmos.DrawPointLightGizmo(LegendaryRuntime.Engine.Engine.Engine.ActiveCamera, light, size, viewPos);
            }
        }

      //  LegendaryRuntime.Engine.Engine.Engine.DoSelection();
        
        // Render ImGui draw data
        imguiController.Render(); // This should be after all ImGui window definitions
        ImGuiController.CheckGLError("End of Frame");
        RenderBufferHelpers.Instance.ApplyPendingResize();
        
        SwapBuffers();
        
     //   ScopedProfiler.PrintStatistics();
    }
    Vector2 mouseInitialPos = Vector2.Zero;
    private bool shouldShowPopup = false;
    private bool initial = true;
    private void ShowMaterialSelectionPopup()
    {
        string name = droppedFile.Split("/").Last();
        
        if (initial)
        {
            initial = false;
            mouseInitialPos = ImGui.GetMousePos();
        }
        
        ImGui.Begin($"Select Material Channel for '{name}'");
        ImGui.SetWindowSize(new Vector2(200, 200));
        ImGui.SetWindowPos(mouseInitialPos);
        if (ImGui.Button("Diffuse"))
        {
            droppedMesh.Material.DiffuseTexture = TextureLoader.LoadTexture(droppedFile, false).Reference().GetGLTexture();
            shouldShowPopup = false;
        }
        if (ImGui.Button("Normal"))
        {
            droppedMesh.Material.NormalTexture = TextureLoader.LoadTexture(droppedFile, false).Reference().GetGLTexture();
            shouldShowPopup = false;
        }
        if (ImGui.Button("Roughness / Metallic"))
        {
            droppedMesh.Material.RoughnessTexture = TextureLoader.LoadTexture(droppedFile, false).Reference().GetGLTexture();
            shouldShowPopup = false;
        }
        
        ImGui.End();
    }
    private void DrawTexture(string name, int textureID, float Size = 128, Light? light = null)
    {
        ImGui.Text(name);
        if (light != null)
        {
            textureID = light.CookieTextureID;
        }
        
        ImGui.ImageButton(name, textureID, new Vector2(Size, Size), new Vector2(0,1), new Vector2(1,0));
        if (ImGui.IsItemHovered() && DraggedOnImGui && light != null)
        {
            Console.WriteLine($"Light {light.Name} Cookie ID" + droppedTextureID);
            light.CookieTextureID = droppedTextureID;
            droppedTextureID = -1;
            DraggedOnImGui = false;
        }
    }
    private bool first = true;
    System.Numerics.Vector3 rot = new System.Numerics.Vector3(0, 0, 0);
    System.Numerics.Vector3 newRotation = new();
    private float snapSize = 1.5f;

    private void DrawLightHierarchy()
    {
        if (ImGui.TreeNode("Lights"))
        {
            foreach (Light light in lights)
            {
                if (ImGui.Button(light.Name))
                {
                    LegendaryRuntime.Engine.Engine.Engine.SelectedRenderableObjects.Clear();
                    LegendaryRuntime.Engine.Engine.Engine.SelectedRenderableObjects.Add(light);
                }
            }
            ImGui.TreePop();
        }
    }
    private float lambda = 0.98f;

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
            
            
        imguiController.PressChar((char)e.Unicode);
    }
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        Title =
            $"Legendary Renderer - {(1 / args.Time).ToString("0.00")} fps - Game Objects: {LegendaryRuntime.Engine.Engine.Engine.GameObjects.Count} - Number of Lights in scene: {LegendaryRuntime.Engine.Engine.Engine.GameObjects.OfType<Light>().ToArray().Length} - Number of visible Lights in Scene: {LegendaryRuntime.Engine.Engine.Engine.NumberOfVisibleLights} - Shadow Casters: {LegendaryRuntime.Engine.Engine.Engine.NumShadowCasters} - Shadow Views {LegendaryRuntime.Engine.Engine.Engine.ShadowViewCount} - Total Triangles Rendered {LegendaryRuntime.Engine.Engine.Engine.TriangleCountRendered} - Total Triangles Culled {LegendaryRuntime.Engine.Engine.Engine.TriangleCountCulled} ({(LegendaryRuntime.Engine.Engine.Engine.TriangleCountTotal > 0 ? (int)(Math.Clamp((float)LegendaryRuntime.Engine.Engine.Engine.TriangleCountCulled / (float)LegendaryRuntime.Engine.Engine.Engine.TriangleCountTotal, 0.0f, 1.0f) * 100.0f) : 0)}%) - Draw Calls {LegendaryRuntime.Engine.Engine.Engine.DrawCalls}";

        base.OnUpdateFrame(args);
        imguiController.Update(this, (float)args.Time);
        LegendaryRuntime.Engine.Engine.Engine.ActiveCamera.MousePosition = MouseState.Position;


        using (new ScopedProfiler("Engine.Update"))
        {
            LegendaryRuntime.Engine.Engine.Engine.Update((float)args.Time);
        }

        if (KeyboardState.IsKeyDown(Keys.Escape))
        {
            DockLayoutManager.SaveLayoutToDisk();
            
            Close();
        }

        if (KeyboardState.IsKeyDown(Keys.W) && !mouseState.IsButtonDown(MouseButton.Button2))
        {
            mode = Gizmos.GizmoMode.Translate;
        }
        if (KeyboardState.IsKeyDown(Keys.R) && !mouseState.IsButtonDown(MouseButton.Button2))
        {
            mode = Gizmos.GizmoMode.Rotate;
        }

        if (KeyboardState.IsKeyPressed(Keys.F1))
        {
            LegendaryRuntime.Engine.Engine.Engine.ActiveCamera = camera;
        }
        

        if (KeyboardState.IsKeyDown(Keys.LeftShift) || KeyboardState.IsKeyDown(Keys.RightShift))
        {
            LegendaryRuntime.Engine.Engine.Engine.IsMultiSelect = true;
        }
        else
        {
            LegendaryRuntime.Engine.Engine.Engine.IsMultiSelect = false;
        }

        if (MouseState.IsButtonPressed(0))
        {
            LegendaryRuntime.Engine.Engine.Engine.ShouldDoSelectionNextFrame = true;
        }
        else
        {
            LegendaryRuntime.Engine.Engine.Engine.ShouldDoSelectionNextFrame = false;
        }
        //l1.Transform.Position = Engine.ActiveCamera.Transform.Position;
        //model2.Transform.Rotation *= Quaternion.FromEulerAngles(0, MathHelper.DegreesToRadians(900 * (float)args.Time), 0);

        for (int i = 0; i < numLights; i++)
        {
            lights[i].Transform.LocalRotation *= Quaternion.FromEulerAngles(0, MathHelper.DegreesToRadians(90) * (float)args.Time, 0);
            lights[i].Transform.Position = new Vector3(-8,2,3) + lights[i].Transform.Forward * 2;
        }
    }


    private void PrintDebugLogInfo()
    {
        int nrAttributes = 0;
        GL.GetInteger(GetPName.MaxVertexAttribs, out nrAttributes);
        Console.WriteLine("Maximum number of vertex attributes supported: " + nrAttributes);

    }

    // New method to handle model loading with caching logic
    void LoadModelFromDragWithCaching(string originalModelFilePath)
    {
        Console.WriteLine($"Attempting to load model with caching: {originalModelFilePath}");
        AssetCacheManager.EnsureInitialized(); // Ensure it's initialized

        try
        {
            AssimpContext importer = new AssimpContext();
            PostProcessSteps processSteps = PostProcessSteps.CalculateTangentSpace | PostProcessSteps.Triangulate |
                                          PostProcessSteps.GenerateBoundingBoxes | PostProcessSteps.GenerateSmoothNormals |
                                          PostProcessSteps.JoinIdenticalVertices | PostProcessSteps.ImproveCacheLocality;
            
            Assimp.Scene scene = importer.ImportFile(originalModelFilePath, processSteps);

            if (scene == null || scene.RootNode == null)
            {
                Console.WriteLine($"Failed to import scene or scene has no root node: {originalModelFilePath}");
                return;
            }
            if (!scene.HasMeshes)
            {
                 Console.WriteLine($"Scene has no meshes: {originalModelFilePath}. Creating empty hierarchy from nodes if any.");
                 // Still proceed to create hierarchy if nodes exist, even without meshes.
            }

            if (scene.HasMeshes)
            {
                Console.WriteLine($"Scene '{originalModelFilePath}' has {scene.MeshCount} meshes. Processing with cache...");
                for (int i = 0; i < scene.MeshCount; i++)
                {
                    Assimp.Mesh assimpMesh = scene.Meshes[i];
                    int meshContentHash = MeshHasher.HashMesh(assimpMesh);

                    // Attempt to generate mesh icon (will only run if icon doesn't exist)
                    // modelFilePath here is originalModelFilePath, which is the full path to the .fbx, .gltf etc.
                    IconGenerator.GenerateMeshIcon(scene, assimpMesh, originalModelFilePath, meshContentHash);

                    if (AssetCacheManager.TryGetCachedMeshData(meshContentHash, originalModelFilePath, out byte[] cachedData))
                    {
                        Console.WriteLine($"Cache hit for mesh {i} (hash {meshContentHash}). Loading from binary.");
                        MeshHasher.GetOrAddMeshFromBinary(meshContentHash, cachedData); // Ensures it's in MeshHasher's map
                    }
                    else
                    {
                        Console.WriteLine($"Cache miss for mesh {i} (hash {meshContentHash}). Processing and caching.");
                        MeshHasher.AddOrGetMesh(assimpMesh); // Process and add to MeshHasher's internal dictionary
                        
                        byte[] binaryData = MeshHasher.SerializeMeshData(assimpMesh); // Serialize the mesh
                        AssetCacheManager.StoreCompiledMesh(meshContentHash, originalModelFilePath, binaryData); // Store it
                    }
                }
            }
            
            Console.WriteLine($"Ensured all meshes from {originalModelFilePath} are processed/cached. Calling ModelLoader.LoadModel.");

            // Define position, rotation, scale for the new model
            Vector3 position = LegendaryRuntime.Engine.Engine.Engine.ActiveCamera.Transform.Position + Engine.Engine.Engine.ActiveCamera.Transform.Forward * 5; // Example position
            Quaternion rotation = Rotation(0, 0, 0); // Example rotation
            Vector3 scale = Vector3.One; // Example scale

            // Now call the standard ModelLoader method. It will use the meshes already processed into MeshHasher.
            GameObject loadedGameObject = ModelLoader.LoadModel(originalModelFilePath, position, rotation, scale, true);
            
            // Optionally, add the loadedGameObject to the main scene or manage it as needed
            // For example, if you have a list of top-level game objects in your engine:
            // LegendaryRuntime.Engine.Engine.Engine.GameObjects.Add(loadedGameObject);
            // Or if your scene manager handles this:
            // LegendaryRuntime.Engine.Engine.Engine.LoadedScenes[0].AddGameObject(loadedGameObject);
            // This part depends on your engine's scene management.
          //  Engine.Engine.Engine.AddGameObject(loadedGameObject); // Add the loaded model to the engine's scene
            Console.WriteLine($"Model {loadedGameObject.Name} created by LoadModel and added to scene.");

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during cached model loading for {originalModelFilePath}: {ex.Message}\n{ex.StackTrace}");
        }
    }
}