using System.Drawing;
using Geometry;
using Geometry.MaterialSystem.IESProfiles;
using LegendaryRenderer.Engine.EngineTypes;
using LegendaryRenderer.GameObjects;
using LegendaryRenderer.Shaders;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using TheLabs.LegendaryRuntime.Engine.GameObjects;
using static LegendaryRenderer.Maths;
using PixelInternalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat;
using TextureHandle = TheLabs.LegendaryRuntime.Engine.Utilities.GLHelpers.TextureHandle;

// Imgui controller (see ImGuiController.cs for notice //
using External.ImguiController;
using ImGuiNET;
using LegendaryRenderer.Geometry;
using LegendaryRenderer.LegendaryRuntime.Engine.Renderer.MaterialSystem;
using Microsoft.VisualBasic;
using Vector2 = System.Numerics.Vector2;

namespace LegendaryRenderer.Application;



public class ApplicationWindow : GameWindow
{
    public static KeyboardState keyboardState;
    public static MouseState mouseState;
    private ImGuiController imguiController;

    public ApplicationWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings)
    {
        keyboardState = KeyboardState;
        mouseState = MouseState;
        GL.Enable(EnableCap.DebugOutput);
        GL.Enable(EnableCap.DebugOutputSynchronous);

    }

    public bool IsMouseVisible
    {
        get { return CursorState == CursorState.Hidden; }
        set { CursorState = value ? CursorState.Normal : CursorState.Grabbed; }
    }

    RenderableMesh _renderableMesh;

    RenderableMesh[] instances = new RenderableMesh[100];

    private Camera camera;
    private Camera camera2;
    private GameObject model;
    private GameObject model2;
    private GameObject model3;
    private List<Light> lights = new List<Light>();
    private int numLights = 0;

    string[] modelExtensions = new string[] { ".fbx", ".gltf", ".glb", ".obj", ".objc", ".objd" };
    string[] textureExtensions = new string[] { ".png", ".jpg", ".jpeg", ".tif" };

    void LoadModelFromDrag(string fileName)
    {
        ModelLoader.LoadModel(fileName, Engine.ActiveCamera.Transform.Position, Rotation(0, 0, 0), Vector3.One, true);
    }

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
        return modelExtensions.Contains(Path.GetExtension(fileName));
    }
    
    bool ContainsTexture(string fileName)
    {
        return textureExtensions.Contains(Path.GetExtension(fileName));
    }

    private RenderableMesh droppedMesh;
    private string droppedFile;
    
    protected override void OnFileDrop(FileDropEventArgs e)
    {
        var fileName = e.FileNames[0];
        var mouseState = ApplicationWindow.mouseState;

        var pos = new Vector2(mouseState.Position.X, Application.Height - mouseState.Position.Y);
        
        if (e.FileNames.Length > 1)
        {
            Console.WriteLine("Please for now only try to import one asset at once.");
        }
        else
        {
            if (ContainsModel(fileName))
            {
                LoadModelFromDrag(fileName);
            }
            
            else if (ContainsTexture(fileName))
            {
                Engine.RenderSelectionBufferOnce();

                Engine.ReadMouseSelection((int)pos.X, (int)pos.Y, out GameObject target);

                if (target != null && target is RenderableMesh)
                {
                    int targetIndex = GetTargetFromFile(fileName);
                    RenderableMesh mesh = target as RenderableMesh;
                    
                    if (targetIndex != -1)
                    {
                        if (targetIndex == 0)
                        {
                            mesh.Material.DiffuseTexture = TextureLoader.LoadTexture(fileName, false);
                        }
                        if (targetIndex == 1)
                        {
                            mesh.Material.NormalTexture = TextureLoader.LoadTexture(fileName, false);
                        }
                        if (targetIndex == 2)
                        {
                            mesh.Material.RoughnessTexture = TextureLoader.LoadTexture(fileName, false);
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
    protected override void OnLoad()
    {
        var loading = new ScopedProfiler("Loading Phase");

        loading.StartTimingCPU();

        base.OnLoad();
        imguiController = new ImGuiController(Application.Width, Application.Height);

        GL.ClearColor(Color4.Black);
        PrintDebugLogInfo();

        GL.DepthRange(1, 0);
        GL.Disable(EnableCap.FramebufferSrgb);
        GL.DepthFunc(DepthFunction.Lequal);

      //  model2 = ModelLoader.LoadModel("Models/dragon.fbx", new Vector3(0, 0.1f, 8), Rotation(0, 0, 0), Vector3.One / 5);
        
      //  RenderableMesh? mdl = model2.Children[0] as RenderableMesh;
      //  mdl.Material.Roughness = 0.9f;
      //  mdl.Material.Colour = Color4.Red;
        //model3 = ModelLoader.LoadModel("Models/diorama.fbx", new Vector3(0, 0, 6), Rotation(90, 0, 0), Vector3.One);

        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(CullFaceMode.Back);

        camera = new Camera(Vector3.Zero, Vector3.Zero, 45.0f);

        camera2 = new Camera(new Vector3(0, 0, 0), Vector3.Zero, 90.0f);

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
            light.Transform.Rotation *= Quaternion.FromEulerAngles(MathHelper.DegreesToRadians(0), MathHelper.DegreesToRadians((360 / numLights) * i), 0);
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

        Light l1 = new Light(new Vector3(0, 0, 0), "Camera Light");

        l1.Colour = Color4.White;
        l1.Intensity = 10.0f;
        l1.Range = 1000.0f;
        l1.OuterCone = 120;
        l1.InnerCone = 90;
        l1.EnableShadows = true;
        l1.Type = Light.LightType.Projector;
        l1.LightIESProfile = profile;
        l1.Transform.Rotation = Rotation(-40, 20, 0);
        //camera2.AddChild(l1);
        // light2.Colour = Color.Green;
        // light3.Colour = Color.Blue;
        //   light3.Transform.Rotation = Quaternion.FromEulerAngles(MathHelper.DegreesToRadians(-35), 0, 0);
        //  var light4 = new Light(new Vector3(0, 5, 5), "Light QRT");
        // light4.Transform.Rotation = Quaternion.FromEulerAngles(MathHelper.DegreesToRadians(-90), 0, 0);

        //camera.AddChild(light);



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

        Engine.RenderBuffers.OnResizeFramebuffer(e.Width, e.Height);
    }

    void ResetCounters()
    {
        Engine.TriangleCountTotal = 0;
        Engine.TriangleCountCulled = 0;
        Engine.TriangleCountRendered = 0;
        Engine.NumShadowCasters = 0;
        Engine.DrawCalls = 0;
        Engine.CullPass = 0;
        Engine.NumberOfVisibleLights = 0;
        Engine.ShadowViewCount = 0;
        Frustum.Count = 0;
        RenderableMesh.ReusedCounter = 0;
        RenderableMesh.TotalSceneMeshes = 0;
        RenderableMesh.LoadedMeshCount = 0;
    }
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        ScopedProfiler.ResetStats();

        ResetCounters();

        Engine.EngineRenderLoop();
        
        
        DoImGui();
        
        imguiController.Render();

        ImGuiController.CheckGLError("End of Frame");
        
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
            droppedMesh.Material.DiffuseTexture = RenderableMesh.LoadTexture(droppedFile);
            shouldShowPopup = false;
        }
        if (ImGui.Button("Normal"))
        {
            droppedMesh.Material.NormalTexture = RenderableMesh.LoadTexture(droppedFile);
            shouldShowPopup = false;
        }
        if (ImGui.Button("Roughness / Metallic"))
        {
            droppedMesh.Material.RoughnessTexture = RenderableMesh.LoadTexture(droppedFile);
            shouldShowPopup = false;
        }
        
        ImGui.End();
    }
    private void DrawTexture(string name, int textureID)
    {
        ImGui.Text(name);
        ImGui.Image((IntPtr)textureID, new Vector2(128,128), new Vector2(0,1), new Vector2(1,0));
    }
    private bool first = true;
    System.Numerics.Vector3 rot = new System.Numerics.Vector3(0, 0, 0);
    System.Numerics.Vector3 newRotation = new();
    private float snapSize = 1.5f;
    private void DoImGui()
    {
        if (shouldShowPopup)
        {
            ShowMaterialSelectionPopup();
        }


        ImGui.SetNextWindowSize(new System.Numerics.Vector2((float)Application.Width / 3, (float)Application.Height / 2));
        ImGui.Begin("Settings", ImGuiWindowFlags.NoResize);
        ImGui.DragFloat("SSAO - Radius", ref Engine.SSAOSettings.Radius, 0.01f, 0.05f, 10.0f);
        ImGui.DragInt("SSAO - Number of Samples", ref Engine.SSAOSettings.NumberOfSamples, 1, 1, 32);
        ImGui.Spacing();
        DrawTexture("Spotlight Last ShadowMap", Engine.SpotShadowMapTexture);

        if (Engine.SelectedRenderableObjects.Count == 1)
        {
            RenderableMesh? target = (Engine.SelectedRenderableObjects[0] as RenderableMesh);

            if (target != null)
            {

                if (ImGui.Button($"Spinning ({target.Spinning})"))
                {
                    target.Spinning = !target.Spinning;
                }


                ImGui.Text($"Materials for {target.Name.Split(':').Last()}:");
                ImGui.BeginChild("Materials", new System.Numerics.Vector2(128 * 3 + 24, 165));
                //ImGui.Text("Materials");
                ImGui.Columns(3, "tables");
                if (target.Material.DiffuseTexture != -1)
                {
                    ImGui.SetColumnWidth(0, 128 + 4);
                    DrawTexture("Diffuse", target.Material.DiffuseTexture);
                    ImGui.NextColumn();
                }
                if (target.Material.NormalTexture != -1)
                {
                    ImGui.SetColumnWidth(1, 128 + 4);
                    DrawTexture("Normal", target.Material.NormalTexture);
                    ImGui.NextColumn();
                }
                if (target.Material.RoughnessTexture != -1)
                {
                    ImGui.SetColumnWidth(2, 128 + 4);
                    DrawTexture("Mask", target.Material.RoughnessTexture);
                }
                ImGui.EndChild();


                Vector3 pos = target.GetRoot().Transform.LocalPosition;
                Vector3 scale = target.GetRoot().Transform.Scale;
                Vector3 rotation = Vector3.Zero;
                target.Transform.Rotation.ToEulerAngles(out rotation);
                System.Numerics.Vector3 newPosition = new System.Numerics.Vector3(pos.X, pos.Y, pos.Z);
                System.Numerics.Vector3 newScale = new System.Numerics.Vector3(scale.X, scale.Y, scale.Z);

                ImGui.Text("Snap Size");

                ImGui.Text($"Selected Object: {target.GetRoot().Name.Split(':').Last()}");
                ImGui.Text("Transform Settings");

                if (first)
                {
                    rot = new System.Numerics.Vector3(MathHelper.RadiansToDegrees(rotation.X), MathHelper.RadiansToDegrees(rotation.Y), MathHelper.RadiansToDegrees(rotation.Z));
                    first = false;
                }

                if (ImGui.DragFloat3($"Position Object", ref newPosition))
                {
                    target.GetRoot().Transform.Position = new Vector3(newPosition.X, newPosition.Y, newPosition.Z);
                }

                if (ImGui.DragFloat3($"Rotate Object", ref rot, snapSize))
                {
                    newRotation = new System.Numerics.Vector3((rot.X), (rot.Y), (rot.Z));
                    target.GetRoot().Transform.Rotation = Rotation(newRotation.X, newRotation.Y, newRotation.Z);
                }

                if (ImGui.DragFloat3($"Scale Object", ref newScale))
                {
                    target.GetRoot().Transform.Scale = new Vector3(newScale.X, newScale.Y, newScale.Z);
                }
            }
            else if (Engine.SelectedRenderableObjects.Count == 0)
            {
                first = true;
            }

            if (ImGui.Button("Show / Hide"))
            {

                target.GetRoot().IsVisible = !target.GetRoot().IsVisible;

            }


        }
        ImGui.End();
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
            
            
        imguiController.PressChar((char)e.Unicode);
    }
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        Title =
            $"Legendary Renderer - {(1 / args.Time).ToString("0.00")} fps - Game Objects: {Engine.GameObjects.Count} - Number of Lights in scene: {Engine.GameObjects.OfType<Light>().ToArray().Length} - Number of visible Lights in Scene: {Engine.NumberOfVisibleLights} - Shadow Casters: {Engine.NumShadowCasters} - Shadow Views {Engine.ShadowViewCount} - Total Triangles Rendered {Engine.TriangleCountRendered} - Total Triangles Culled {Engine.TriangleCountCulled} ({(Engine.TriangleCountTotal > 0 ? (int)(Math.Clamp((float)Engine.TriangleCountCulled / (float)Engine.TriangleCountTotal, 0.0f, 1.0f) * 100.0f) : 0)}%) - Draw Calls {Engine.DrawCalls}";

        base.OnUpdateFrame(args);
        imguiController.Update(this, (float)args.Time);
        Engine.ActiveCamera.MousePosition = MouseState.Position;


        using (new ScopedProfiler("Engine.Update"))
        {
            Engine.Update((float)args.Time);
        }

        if (KeyboardState.IsKeyDown(Keys.Escape))
        {
            Close();
        }

        if (KeyboardState.IsKeyPressed(Keys.F1))
        {
            Engine.ActiveCamera = camera;
        }

        if (KeyboardState.IsKeyPressed(Keys.F2))
        {
            Engine.ActiveCamera = camera2;
        }

        if (KeyboardState.IsKeyDown(Keys.LeftShift) || KeyboardState.IsKeyDown(Keys.RightShift))
        {
            Engine.IsMultiSelect = true;
        }
        else
        {
            Engine.IsMultiSelect = false;
        }

        if (MouseState.IsButtonPressed(0))
        {
            Engine.ShouldDoSelectionNextFrame = true;
        }
        else
        {
            Engine.ShouldDoSelectionNextFrame = false;
        }
        
        //model2.Transform.Rotation *= Quaternion.FromEulerAngles(0, MathHelper.DegreesToRadians(900 * (float)args.Time), 0);

        for (int i = 0; i < numLights; i++)
        {
            lights[i].Transform.Rotation *= Quaternion.FromEulerAngles(0, MathHelper.DegreesToRadians(90) * (float)args.Time, 0);
            lights[i].Transform.Position = new Vector3(-8,2,3) + lights[i].Transform.Forward * 2;
        }
    }


    private void PrintDebugLogInfo()
    {
        int nrAttributes = 0;
        GL.GetInteger(GetPName.MaxVertexAttribs, out nrAttributes);
        Console.WriteLine("Maximum number of vertex attributes supported: " + nrAttributes);

    }
}