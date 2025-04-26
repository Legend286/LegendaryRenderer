using ImGuiNET;
using OpenTK.Graphics.ES30;
using System.Numerics;
using LegendaryRenderer.Engine.EngineTypes;
using TheLabs.LegendaryRuntime.Engine.Utilities;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor;

public class EditorViewport
{
    public Vector2 ViewportSize { get; private set; }
    public Vector2 ViewportPosition { get; private set; }
    public Vector2 MouseViewportPosition { get; private set; }
    public Vector2 MouseFramebufferPosition { get; private set; }    
    public bool IsFocused { get; private set; }
    public bool IsHovered { get; private set; }
    
    private bool bInitialized = false;
    
    public int FramebufferID { get; private set; }
    
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

    private bool resizingThisFrame = false;
    public void Draw()
    {
        // 0) Reset resize flag
        resizingThisFrame = false;

        // 1) Begin the window with NO padding
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
        ImGui.Begin("Viewport", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleVar(); // pop WindowPadding

        // 2) Compute how big the content area is (in ImGui “points”)
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
        ImGui.PopStyleVar(2); // pop FramePadding + ItemSpacing

        // 6) Capture the exact image bounds in screen coords
        Vector2 imgMin = ImGui.GetItemRectMin();
        Vector2 imgMax = ImGui.GetItemRectMax();

        // 7) Expose viewport position & size (in points)
        ViewportPosition = imgMin;
        ViewportSize = new Vector2(viewW, viewH);

        ImGui.End();

        // 8) Compute mouse‐in‐viewport (points)
        var io = ImGui.GetIO();
        Vector2 ms = io.MousePos;    // mouse in screen‐space points
        Vector2 local = ms - imgMin; // point‐local

        // clamp into [0, viewW/viewH]
        local.X = Math.Clamp(local.X, 0, ViewportSize.X);
        local.Y = Math.Clamp(local.Y, 0, ViewportSize.Y);
        MouseViewportPosition = local;

        // 9) Convert to *pixel* coords using ImGui’s framebuffer scale
        Vector2 scale = io.DisplayFramebufferScale; // e.g. (2,2) on Retina
        float px = local.X * scale.X;
        float py = local.Y * scale.Y;

        // clamp to the real pixel resolution
        px = Math.Clamp(px, 0, viewW * scale.X - 1);
        py = Math.Clamp(py, 0, viewH * scale.Y - 1);

        MouseFramebufferPosition = new Vector2(px, py);
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