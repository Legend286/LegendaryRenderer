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
    public Vector2 MouseViewpotPosition { get; private set; }
    public Vector2 MouseFramebufferPosition { get; private set; }    
    public bool IsFocused { get; private set; }
    public bool IsHovered { get; private set; }
    
    private bool bInitialized = false;
    
    public int FramebufferID { get; private set; }

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
        MouseViewpotPosition = new Vector2(0, 0);
        MouseFramebufferPosition = new Vector2(0, 0);
        lastWidth = -1;
        lastHeight = -1;
    }

    private bool resizingThisFrame = false;
    public void Draw()
    {
        resizingThisFrame = false;

        ImGui.Begin("Viewport", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        ViewportSize = ImGui.GetWindowSize();
        ViewportPosition = ImGui.GetCursorScreenPos();

        int width = (int)Math.Max(ViewportSize.X, 1);
        int height = (int)Math.Max(ViewportSize.Y, 1);

        if (width != lastWidth || height != lastHeight)
        {
            lastWidth = width;
            lastHeight = height;
            pendingResizeWidth = width;
            pendingResizeHeight = height;
            resizingThisFrame = true;
        }

        bool focusedNow = ImGui.IsWindowFocused();
        bool hoveredNow = ImGui.IsWindowHovered();

        if (focusedNow && !IsFocused)
            ViewportFocused?.Invoke();
        if (!focusedNow && IsFocused)
            ViewportUnfocused?.Invoke();

        IsFocused = focusedNow;
        IsHovered = hoveredNow;

        // Render framebuffer texture
        if (FramebufferID != -1)
        {
            ImGui.Image(FramebufferID, ViewportSize, new Vector2(0, 1), new Vector2(1, 0));
        }

        var io = ImGui.GetIO();
        Vector2 mouseScreen = new Vector2(io.MousePos.X, io.MousePos.Y);
        MouseViewpotPosition = mouseScreen - ViewportPosition;

        float xNorm = MouseViewpotPosition.X / ViewportSize.X;
        float yNorm = MouseViewpotPosition.Y / ViewportSize.Y;
        MouseFramebufferPosition = new Vector2(xNorm * width, yNorm * height);

        ImGui.End();
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