using ImGuiNET;
using LegendaryRenderer.LegendaryRuntime.Engine.Editor.Helpers;
using LegendaryRenderer.LegendaryRuntime.Engine.Editor.Systems;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MaterialSystem;
using LegendaryRenderer.LegendaryRuntime.Engine.Utilities;
using OpenTK.Mathematics;
using System.Collections.Generic;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.EngineTypes;
using Vector2 = System.Numerics.Vector2;
using Vector3 = OpenTK.Mathematics.Vector3;
using Matrix4x4 = OpenTK.Mathematics.Matrix4;
using System.IO;
using LegendaryRenderer.LegendaryRuntime.Application;
using LegendaryRenderer.LegendaryRuntime.Application.Profiling;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.ModelLoader;
using OpenTK.Graphics.ES30;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor.UserInterface;

class RollingBuffer : IDisposable
{
    public float[] buffer = new float[128];
    public int bufferTotal = 0;
    
    public void AddToRollingBuffer(float value)
    {
        if (bufferTotal < buffer.Length)
        {
            buffer[bufferTotal++] = value;
        }
        else
        {
            // Shift the buffer left and add the new value at the end
            for (int i = 1; i < buffer.Length; i++)
            {
                buffer[i - 1] = buffer[i];
            }
            buffer[buffer.Length - 1] = value;
        }
    }

    public void Dispose()
    {
        ClearBuffer();
    }
    public void ClearBuffer()
    {
        buffer = new float[128];
        bufferTotal = 0;
    }
}
public class EditorViewport
{
    public Vector2 ViewportSize { get; private set; }
    public Vector2 ViewportPosition { get; private set; }
    public Vector2 MouseViewportPosition { get; private set; }
    public static Vector2 MouseFramebufferPosition { get; private set; }    
    public bool IsFocused { get; private set; }
    public bool IsHovered { get; private set; }
    
    public Vector2 Min { get; private set; }
    public Vector2 Max { get; private set; }
    
    private bool bInitialized = false;
    
    public int FramebufferID { get; private set; }
    
    private bool showGrid = true;
    private float editorGridSpacing = 1.0f;
    private string currentGridSpacingLabel = "1.0m";

    public void SetFramebufferID(int framebufferID)
    {
        FramebufferID = framebufferID;
    }

    public event Action<int, int>? ResizeRequested;
    public event Action? ViewportFocused;
    public event Action? ViewportUnfocused;

    private int lastWidth, lastHeight;
    int pendingResizeWidth, pendingResizeHeight;
    
    public EditorViewport(int framebufferID)
    {
        FramebufferID = framebufferID;
        ViewportSize = new Vector2(0, 0);
        ViewportPosition = new Vector2(0, 0);
        MouseViewportPosition = new Vector2(0, 0);
        MouseFramebufferPosition = new Vector2(0, 0);
        lastWidth = -1;
        lastHeight = -1;
    }
    private int cycle = 0;
    private bool resizingThisFrame = false;
    
    Dictionary<string, RollingBuffer> buffers = new Dictionary<string, RollingBuffer>();
    public void Draw()
    {
        // 0) Reset resize flag
        resizingThisFrame = false;
        
        // 1) Begin window with no padding
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
        ImGui.Begin("Viewport", ImGuiWindowFlags.MenuBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        // ─────────────────────────────────────────────────────────────────────────────────────────────────

        ImGui.Begin("Statistics");

        foreach (var KVP in Profiler.Statistics)
        {
            if(buffers.TryGetValue(KVP.Key, out RollingBuffer buf))
            {
                if (buf == null)
                {
                    buf = new RollingBuffer();
                    buffers[KVP.Key] = buf;
                }
            }
            else
            {
                buf = new RollingBuffer();
                buffers.Add(KVP.Key, buf);
            }
            {
                buf.AddToRollingBuffer(KVP.Value);
                ImGui.PlotHistogram($"{KVP.Key}", ref buf.buffer[0], buf.bufferTotal, 0, $"{buf.buffer[buf.bufferTotal-1]} milliseconds", 0.001f, 32.0f);
            }
        }
        ImGui.End();
        
        if (ImGui.BeginMenuBar())
        {
            if (ImGui.BeginMenu("Create"))
            {
                if (ImGui.MenuItem("Camera"))
                {
                    var camera = new Camera(Engine.Engine.ActiveCamera.Transform.Position, Engine.Engine.ActiveCamera.Transform.Position + Engine.Engine.ActiveCamera.Transform.Forward * 10, 90.0f);
                    camera.Transform.Rotation = Engine.Engine.ActiveCamera.Transform.Rotation;
                }
                    
                if (ImGui.BeginMenu("Lights"))
                {
                    if (ImGui.MenuItem("Point Light"))
                    {
                        var light = new Light(Engine.Engine.ActiveCamera.Transform.Position, "Point Light");
                        light.Transform.Rotation = Engine.Engine.ActiveCamera.Transform.Rotation;
                        light.Type = Light.LightType.Point;
                        light.OuterCone = 75.0f;
                        light.InnerCone = 60.0f;
                        light.EnableShadows = true;
                        light.Intensity = 100.0f;
                        light.Bias = 0.0002f;
                        light.NormalBias = 0.0001f;
                        light.Colour = Color4.FromHsv(new Vector4((((float)cycle++ / 16.0f) % 16.0f, 0.5f, 1.0f, 1.0f)));
                        
                        // Set up volumetric defaults for point light
                        light.EnableVolumetrics = true;
                        light.VolumetricIntensity = 1.0f;
                        light.VolumetricAbsorption = 0.1f;
                        light.VolumetricScattering = 0.5f;
                        light.VolumetricAnisotropy = 0.0f; // Isotropic for point lights
                    }
                    if (ImGui.MenuItem("Spot Light"))
                    {
                        var light = new Light(Engine.Engine.ActiveCamera.Transform.Position, "Spot Light");
                        light.Transform.Rotation = Engine.Engine.ActiveCamera.Transform.Rotation;
                        light.Type = Light.LightType.Spot;
                        light.OuterCone = 75.0f;
                        light.InnerCone = 60.0f;
                        light.EnableShadows = true;
                        light.Intensity = 100.0f;
                        light.Bias = 0.00001f;
                        light.NormalBias = 0.0001f;
                        light.Colour = Color4.FromHsv(new Vector4((((float)cycle++ / 16.0f) % 16.0f, 0.5f, 1.0f, 1.0f)));
                        
                        // Set up volumetric defaults for spot light  
                        light.EnableVolumetrics = true;
                        light.VolumetricIntensity = 1.2f;
                        light.VolumetricAbsorption = 0.08f;
                        light.VolumetricScattering = 0.6f;
                        light.VolumetricAnisotropy = 0.4f; // Forward scattering for spot lights
                    }
                    if (ImGui.MenuItem("Directional Light"))
                    {
                        var light = new Light(Engine.Engine.ActiveCamera.Transform.Position, "Directional Light");
                        light.Transform.Rotation = Engine.Engine.ActiveCamera.Transform.Rotation;
                        light.Type = Light.LightType.Directional;
                        light.OuterCone = 75.0f;
                        light.InnerCone = 60.0f;
                        light.EnableShadows = true;
                        light.Intensity = 100.0f;
                        light.Bias = 0.00001f;
                        light.NormalBias = 0.001f;
                        light.Colour = Color4.LightYellow;
                        light.CascadeCount = 4;
                        
                        // Set up volumetric defaults for directional light (sun)
                        light.EnableVolumetrics = true;
                        light.VolumetricIntensity = 0.8f;
                        light.VolumetricAbsorption = 0.02f;
                        light.VolumetricScattering = 0.3f;
                        light.VolumetricAnisotropy = 0.7f; // Strong forward scattering for sun rays
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("View"))
            {
                string GridState = showGrid ? "Hide" : "Show";
                if (ImGui.MenuItem($"{GridState} Grid", "", ref showGrid))
                {
                    // Logic is handled by ref bool in MenuItem, or manually: showGrid = !showGrid;
                }

                if (ImGui.BeginMenu($"Grid Spacing {currentGridSpacingLabel}"))
                {
                    if (ImGui.MenuItem("0.1m"))
                    {
                        editorGridSpacing = 0.1f;
                        currentGridSpacingLabel = "0.1m";
                    }
                    if (ImGui.MenuItem("0.25m"))
                    {
                        editorGridSpacing = 0.25f;
                        currentGridSpacingLabel = "0.25m";
                    }
                    if (ImGui.MenuItem("0.5m"))
                    {
                        editorGridSpacing = 0.5f;
                        currentGridSpacingLabel = "0.5m";
                    }
                    if (ImGui.MenuItem("1.0m"))
                    {
                        editorGridSpacing = 1.0f;
                        currentGridSpacingLabel = "1.0m";
                    }
                    if (ImGui.MenuItem("2.0m"))
                    {
                        editorGridSpacing = 2.0f;
                        currentGridSpacingLabel = "2.0m";
                    }
                    if (ImGui.MenuItem("5.0m"))
                    {
                        editorGridSpacing = 5.0f;
                        currentGridSpacingLabel = "5.0m";
                    }
                    if (ImGui.MenuItem("10.0m"))
                    {
                        editorGridSpacing = 10.0f;
                        currentGridSpacingLabel = "10.0m";
                    }
                    ImGui.EndMenu();
                }
                
                string GizmoState = Gizmos.Gizmos.DrawGizmos ? "Disable" : "Enable";
                if (ImGui.MenuItem($"{GizmoState} Gizmos"))
                {
                    Gizmos.Gizmos.DrawGizmos = !Gizmos.Gizmos.DrawGizmos;
                }
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Settings"))
            {
                string ShadowState = Engine.Engine.EnableShadows ? "Disable" : "Enable";
                if (ImGui.MenuItem($"{ShadowState} Shadows"))
                {
                    Engine.Engine.EnableShadows = !Engine.Engine.EnableShadows;
                }
                if (ImGui.MenuItem("Enable Post Processing"))
                {
                    // your post processing toggle logic
                }
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Camera"))
            {
                if (ImGui.MenuItem("Reset View"))
                {
                    // your camera reset logic
                }
                ImGui.EndMenu();
            }
            ImGui.EndMenuBar();
        }

        // 2) Compute how big the content area is (in ImGui "points")
        Vector2 contentAvail = ImGui.GetContentRegionAvail();
        int viewW = (int)Math.Max(contentAvail.X, 1);
        int viewH = (int)Math.Max(contentAvail.Y, 1);

        // 3) Detect a size change
        if (viewW != lastWidth || viewH != lastHeight)
        {
            lastWidth = viewW;
            lastHeight = viewH;
            pendingResizeWidth = viewW;
            pendingResizeHeight = viewH;
            resizingThisFrame = true;
        }

        // 4) Focus / Hover callbacks (unchanged)
        bool focusedNow = ImGui.IsWindowFocused();
        bool hoveredNow = ImGui.IsWindowHovered();
        if (focusedNow && !IsFocused) ViewportFocused?.Invoke();
        if (!focusedNow && IsFocused) ViewportUnfocused?.Invoke();
        IsFocused = focusedNow;
        IsHovered = hoveredNow;

        // 5) Draw the FBO texture flush (zero item & frame padding)
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
        if (FramebufferID != -1)
            ImGui.Image(
                FramebufferID,
                new Vector2(viewW, viewH),
                new Vector2(0, 1),
                new Vector2(1, 0)
            );

        // Add drag and drop target for models
        if (ImGui.BeginDragDropTarget())
        {
            var payload = ImGui.AcceptDragDropPayload("MODEL_ASSET");
            unsafe
            {
                // Check if the native pointer of the payload is valid and data exists
                if (payload.NativePtr != null && payload.DataSize > 0)
                {
                    // Accessing payload.Data is safe here because NativePtr is not null
                    string modelPath = System.Text.Encoding.UTF8.GetString((byte*)payload.Data, payload.DataSize);
                    if (File.Exists(modelPath))
                    {
                        // Get the mouse position in viewport coordinates
                        var mousePos = ImGui.GetMousePos();
                        var viewportPos = mousePos - ImGui.GetItemRectMin();
                    
                        // Convert to normalized device coordinates (-1 to 1)
                        var ndcX = (viewportPos.X / viewW) * 2 - 1;
                        var ndcY = -((viewportPos.Y / viewH) * 2 - 1);
                    
                        // Create a ray from the camera through the mouse position
                        var rayClip = new Vector4(ndcX, ndcY, -1.0f, 1.0f);
                        var rayEye = rayClip * Matrix4.Invert(Engine.Engine.ActiveCamera.ProjectionMatrix);
                        rayEye = new Vector4(rayEye.X, rayEye.Y, -1.0f, 0.0f);
                        var rayWorld = rayEye * Matrix4.Invert(Engine.Engine.ActiveCamera.ViewMatrix);
                        var rayDir = Vector3.Normalize(new Vector3(rayWorld.X, rayWorld.Y, rayWorld.Z));
                    
                        // Create a plane at y=0 (ground plane)
                        var planeNormal = Vector3.UnitY;
                        var planePoint = Vector3.Zero;
                    
                        // Calculate intersection with ground plane
                        var denom = Vector3.Dot(planeNormal, rayDir);
                        if (Math.Abs(denom) > 0.0001f)
                        {
                            var t = Vector3.Dot(planePoint - Engine.Engine.ActiveCamera.Transform.Position, planeNormal) / denom;
                            var intersectionPoint = Engine.Engine.ActiveCamera.Transform.Position + rayDir * t;
                        
                            // Load and instantiate the model at the intersection point using the caching mechanism
                            var model = ApplicationWindow.LoadModelFromDragWithCaching(modelPath, intersectionPoint, Quaternion.Identity, Vector3.One);
                            if (model != null) // Check if the model was loaded successfully
                            {
                                // No need to add to scene here, GameObject constructor should handle it.
                                // Engine.Engine.LoadedScenes[0].AddGameObject(model); 
                                Console.WriteLine($"Model {model.Name} should have been added to the scene by its constructor.");
                            }
                        }
                    }
                }
            }
            ImGui.EndDragDropTarget();
        }

        ImGui.PopStyleVar(2); // pop FramePadding + ItemSpacing

        // 6) Capture the exact image bounds in screen coords
        Vector2 imgMin = ImGui.GetItemRectMin();
        Vector2 imgMax = ImGui.GetItemRectMax();
        Min = new Vector2(imgMin.X, imgMin.Y);
        Max = new Vector2(imgMax.X, imgMax.Y);

        // Draw Infinite Grid if enabled
        if (showGrid && Engine.Engine.ActiveCamera != null)
        {
            Gizmos.Gizmos.DrawInfiniteGrid(Maths.FromNumericsVector2(imgMin), Maths.FromNumericsVector2(imgMax), Engine.Engine.ActiveCamera, editorGridSpacing);
        }

        // 7) Expose viewport position & size (in points)
        ViewportPosition = imgMin;
        ViewportSize = new Vector2(imgMax.X - imgMin.X, imgMax.Y - imgMin.Y);

        if (EditorSystem.EditorTextures.TryGetValue("light_bulb", out Texture bulbTexture))
        {
            int textureID = bulbTexture.GetGLTexture();
            foreach (Light light in Engine.Engine.GameObjects.OfType<Light>())
            {
                if (light.Type != Light.LightType.Directional && light.WasRenderedLastFrame)
                {
                    EditorWorldIconManager.Draw(Engine.Engine.ActiveCamera, light, textureID);
                }
            }
        }
        if (EditorSystem.EditorTextures.TryGetValue("object", out Texture objectTexture))
        {
            int textureID = objectTexture.GetGLTexture();
            foreach (RenderableMesh mesh in Engine.Engine.RenderableMeshes)
            {
                if (mesh.WasRenderedLastFrame)
                {
                    EditorWorldIconManager.Draw(Engine.Engine.ActiveCamera, mesh, textureID);
                }
            }
        }

        ImGui.End();

        // 8) Compute mouse‐in‐viewport (points)
        var io = ImGui.GetIO();
        Vector2 ms = Application.ApplicationWindow.GetMousePosition();    // mouse in screen‐space points
        Vector2 local = ms - imgMin; // point‐local

        // clamp into [0, viewW/viewH]
        local.X = Math.Clamp(local.X, 0, ViewportSize.X);
        local.Y = Math.Clamp(local.Y, 0, ViewportSize.Y);
        MouseViewportPosition = Application.ApplicationWindow.GetMousePosition() - ViewportPosition;

        // 9) Convert to *pixel* coords using ImGui's framebuffer scale
        Vector2 scale = io.DisplayFramebufferScale; // e.g. (2,2) on Retina
        float px = local.X * scale.X;
        float py = local.Y * scale.Y;

        // clamp to the real pixel resolution
        px = Math.Clamp(px, 0, viewW * scale.X - 1);
        py = Math.Clamp(py, 0, viewH * scale.Y - 1);

        MouseFramebufferPosition = new Vector2(px, py);
        ImGui.PopStyleVar(); // pop WindowPadding
    }

    public void ApplyPendingResize()
    {
        if (pendingResizeWidth > 0 && pendingResizeHeight > 0 && resizingThisFrame)
        {
            RenderBufferHelpers.Instance.RequestResize(pendingResizeWidth, pendingResizeHeight);

            FramebufferID = RenderBufferHelpers.Instance.GetTextureHandle(GLHelpers.TextureHandle.COPY);
            
            pendingResizeWidth = pendingResizeHeight = -1;
            resizingThisFrame = false;
        }
    }
}