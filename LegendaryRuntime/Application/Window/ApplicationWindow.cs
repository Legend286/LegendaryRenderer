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
                LoadModelFromDragWithCaching(fileName, Vector3.Zero, Quaternion.Identity, Vector3.One);
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
        
        Engine.Engine.Engine.DoSelection();
        
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
    public static GameObject LoadModelFromDragWithCaching(string originalModelFilePath, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Console.WriteLine($"Attempting to load model: {originalModelFilePath}");
        AssetCacheManager.EnsureInitialized(); // Ensure it's initialized

        string extension = Path.GetExtension(originalModelFilePath).ToLowerInvariant();
        
        try
        {
            if (extension == ".meshasset")
            {
                Console.WriteLine($"Loading pre-compiled .meshasset: {originalModelFilePath}");
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalModelFilePath);

                if (!int.TryParse(fileNameWithoutExtension, out int meshContentHash))
                {
                    // Attempt to get hash from manifest if filename is not the hash
                    // This part requires AssetCacheManager to have a way to lookup hash by compiled asset path
                    // For now, we'll rely on the filename being the hash or assume TryGetManifestEntryByCompiledPath might exist
                    // If not, this is a limitation if .meshasset files are not named by their hash.
                    // A more robust solution might involve hashing the .meshasset file's content if the name isn't the hash.
                    Console.WriteLine($"Could not parse mesh content hash from filename: {fileNameWithoutExtension}. Attempting to find via manifest or re-hash (not implemented).");
                    // Placeholder: if manifest lookup by compiled path existed:
                    // if (!AssetCacheManager.TryGetHashForCompiledAsset(originalModelFilePath, out meshContentHash))
                    // {
                    //    Console.WriteLine($"Failed to determine hash for .meshasset: {originalModelFilePath}");
                    //    return null;
                    // }
                    // For now, if filename is not hash, we cannot proceed with this simplified direct load.
                    // Fallback to attempting ModelLoader.LoadModel which might have its own logic or fail.
                    // This is a complex case if .meshasset isn't named by its hash.
                    // The error indicates Assimp is called by ModelLoader.LoadModel, so that fallback is also problematic.
                    // The most robust thing if filename isn't hash is to compute hash from file bytes.
                    // However, MeshHasher.GetOrAddMeshFromBinary *requires* the hash as input.

                    // If the filename *must* be the hash for direct .meshasset loading:
                    Console.WriteLine($"Error: .meshasset filename '{fileNameWithoutExtension}' is not a valid integer hash.");
                    return null;
                }

                byte[] meshAssetBytes;
                try
                {
                    meshAssetBytes = File.ReadAllBytes(originalModelFilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading .meshasset file '{originalModelFilePath}': {ex.Message}");
                    return null;
                }

                if (meshAssetBytes == null || meshAssetBytes.Length == 0)
                {
                    Console.WriteLine($"Failed to read data from .meshasset: {originalModelFilePath}");
                    return null;
                }

                // This ensures the mesh data is loaded into MeshHasher's internal structures.
                MeshHasher.CombinedMesh? gpuMesh = MeshHasher.GetOrAddMeshFromBinary(meshContentHash, meshAssetBytes);
                if (gpuMesh == null)
                {
                    Console.WriteLine($"Failed to process mesh from binary data for hash {meshContentHash} from file {originalModelFilePath}");
                    return null;
                }

                // Pass the original .meshasset path as the 'file' key for RenderableMesh/MeshFactory
                RenderableMesh msh = new RenderableMesh(originalModelFilePath, 0);
                msh.SetMeshData((MeshHasher.CombinedMesh)gpuMesh);
                if (msh.VertexCount != -1) // Check if RenderableMesh constructor + MeshFactory succeeded
                {
                    Console.WriteLine($"Successfully created GameObject from .meshasset: {originalModelFilePath}");
                    msh.Loaded = true;
                    return msh;
                }
                

                Console.WriteLine($"Failed to initialize RenderableMesh for .meshasset: {originalModelFilePath} after loading to MeshHasher.");
                return null;

            }
            // This is for .fbx, .obj, .gltf etc. (original Assimp processing path)

            Console.WriteLine($"Processing source model file with Assimp: {originalModelFilePath}");
            AssimpContext importer = new AssimpContext();
            PostProcessSteps processSteps = PostProcessSteps.CalculateTangentSpace | PostProcessSteps.Triangulate |
                                            PostProcessSteps.GenerateBoundingBoxes | PostProcessSteps.GenerateSmoothNormals |
                                            PostProcessSteps.JoinIdenticalVertices | PostProcessSteps.ImproveCacheLocality;

            Assimp.Scene scene = importer.ImportFile(originalModelFilePath, processSteps);

            if (scene == null || scene.RootNode == null)
            {
                Console.WriteLine($"Failed to import scene or scene has no root node: {originalModelFilePath}");
                return null;
            }
            // Note: Scene might not have meshes but still have a node structure.
            // ModelLoader.LoadModel should handle creating an empty hierarchy if necessary.

            if (scene.HasMeshes)
            {
                Console.WriteLine($"Scene '{originalModelFilePath}' has {scene.MeshCount} meshes. Processing with cache...");
                for (int i = 0; i < scene.MeshCount; i++)
                {
                    Assimp.Mesh assimpMesh = scene.Meshes[i];
                    int meshContentHash = MeshHasher.HashMesh(assimpMesh);

                    IconGenerator.GenerateMeshIcon(scene, assimpMesh, originalModelFilePath, meshContentHash);

                    if (AssetCacheManager.TryGetCachedMeshData(meshContentHash, originalModelFilePath, out byte[] cachedData))
                    {
                        Console.WriteLine($"Cache hit for mesh {i} (hash {meshContentHash}). Ensuring it's in MeshHasher.");
                        MeshHasher.GetOrAddMeshFromBinary(meshContentHash, cachedData);
                    }
                    else
                    {
                        Console.WriteLine($"Cache miss for mesh {i} (hash {meshContentHash}). Processing, serializing, and caching.");
                        MeshHasher.AddOrGetMesh(assimpMesh);

                        byte[] binaryData = MeshHasher.SerializeMeshData(assimpMesh);
                        AssetCacheManager.StoreCompiledMesh(meshContentHash, originalModelFilePath, binaryData);
                    }
                }
            }
            else
            {
                Console.WriteLine($"Scene '{originalModelFilePath}' has no meshes. ModelLoader.LoadModel will create hierarchy from nodes if any.");
            }

            Console.WriteLine($"Ensured all meshes from {originalModelFilePath} are processed/cached. Calling ModelLoader.LoadModel.");
            GameObject loadedGameObject = ModelLoader.LoadModel(originalModelFilePath, position, rotation, scale, true);

            Console.WriteLine($"Model {loadedGameObject?.Name} (from source) created by ModelLoader.LoadModel.");
            return loadedGameObject;

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during model loading for {originalModelFilePath}: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }
}