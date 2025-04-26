using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;
using TextureHandle = TheLabs.LegendaryRuntime.Engine.Utilities.GLHelpers.TextureHandle;

namespace LegendaryRenderer.Engine.EngineTypes;

public class RenderBufferHelpers
{
    public PixelInternalFormat Format { get; }
    public PixelInternalFormat DepthFormat { get; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public static RenderBufferHelpers? Instance { get; private set; }

    private FramebufferBundle? CurrentFramebuffer;
    private FramebufferBundle? PendingFramebuffer;

    public int FramebufferID => CurrentFramebuffer?.TexScreenCopy ?? -1;

    private Vector2 offset;

    private bool resizePending = false;
    private int pendingWidth = -1;
    private int pendingHeight = -1;

    public string Name { get; }

    private class FramebufferBundle
    {
        public int HandleGBuffer;
        public int HandleLightingBuffer;
        public int HandlePickingBuffer;
        public int HandleSelectionBuffer;
        public int HandleScreenCopy;

        public int TexAlbedo;
        public int TexDepth;
        public int TexNormal;
        public int TexVelocity;
        public int TexLighting;
        public int TexPicking;
        public int TexSelection;
        public int TexSelectionDepth;
        public int TexScreenCopy;

        public int Width;
        public int Height;
    }

    public RenderBufferHelpers(PixelInternalFormat format, PixelInternalFormat depthFormat, int width, int height, string name = "Generic RenderTarget")
    {
        if (Instance != null)
            throw new Exception("RenderBufferHelpers Instance already exists!");

        Instance = this;
        Format = format;
        DepthFormat = depthFormat;
        Name = name;

        CurrentFramebuffer = CreateFramebuffer(width, height);
        Width = CurrentFramebuffer.Width;
        Height = CurrentFramebuffer.Height;
    }

    public void RequestResize(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        if (width == Width && height == Height)
            return;

        pendingWidth = width;
        pendingHeight = height;
        resizePending = true;
    }

    public void ApplyPendingResize()
    {
        if (!resizePending) return;

        PendingFramebuffer = CreateFramebuffer(pendingWidth, pendingHeight);

        if (CurrentFramebuffer != null)
            ReleaseFramebuffer(CurrentFramebuffer);

        CurrentFramebuffer = PendingFramebuffer;
        PendingFramebuffer = null;

        Width = pendingWidth;
        Height = pendingHeight;
        resizePending = false;

      //  GL.Viewport(0, 0, Width, Height);
    }

    private FramebufferBundle CreateFramebuffer(int width, int height)
    {
        FramebufferBundle fb = new FramebufferBundle();
        fb.Width = width;
        fb.Height = height;

        // GBUFFER
        fb.HandleGBuffer = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, fb.HandleGBuffer);

        fb.TexAlbedo = CreateTexture(width, height, Format, PixelFormat.Rgba, PixelType.UnsignedByte);
        fb.TexNormal = CreateTexture(width, height, PixelInternalFormat.Rgba16Snorm, PixelFormat.Rgba, PixelType.HalfFloat);
        fb.TexVelocity = CreateTexture(width, height, PixelInternalFormat.Rg16f, PixelFormat.Rg, PixelType.HalfFloat);
        fb.TexDepth = CreateTexture(width, height, DepthFormat, PixelFormat.DepthComponent, PixelType.Float);

        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, fb.TexAlbedo, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, fb.TexNormal, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment2, TextureTarget.Texture2D, fb.TexVelocity, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, fb.TexDepth, 0);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        // Lighting Buffer
        fb.HandleLightingBuffer = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, fb.HandleLightingBuffer);
        fb.TexLighting = CreateTexture(width, height, PixelInternalFormat.Rgba32f, PixelFormat.Rgba, PixelType.Float);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, fb.TexLighting, 0);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        // Picking Buffer
        fb.HandlePickingBuffer = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, fb.HandlePickingBuffer);
        fb.TexPicking = CreateTexture(width, height, PixelInternalFormat.Rgba32f, PixelFormat.Rgba, PixelType.Float);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, fb.TexPicking, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, fb.TexDepth, 0);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        // Selection Buffer
        fb.HandleSelectionBuffer = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, fb.HandleSelectionBuffer);
        fb.TexSelection = CreateTexture(width, height, Format, PixelFormat.Rgba, PixelType.UnsignedByte);
        fb.TexSelectionDepth = CreateTexture(width, height, DepthFormat, PixelFormat.DepthComponent, PixelType.Float);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, fb.TexSelection, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, fb.TexSelectionDepth, 0);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        // Screen Copy
        fb.HandleScreenCopy = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, fb.HandleScreenCopy);
        fb.TexScreenCopy = CreateTexture(width, height, PixelInternalFormat.Rgba16f, PixelFormat.Rgba, PixelType.HalfFloat);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, fb.TexScreenCopy, 0);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        return fb;
    }

    private void ReleaseFramebuffer(FramebufferBundle fb)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        void SafeDeleteFramebuffer(ref int handle)
        {
            if (handle != 0)
            {
                GL.DeleteFramebuffer(handle);
                handle = 0;
            }
        }

        void SafeDeleteTexture(ref int tex)
        {
            if (tex != 0)
            {
                GL.DeleteTexture(tex);
                tex = 0;
            }
        }

        SafeDeleteFramebuffer(ref fb.HandleGBuffer);
        SafeDeleteFramebuffer(ref fb.HandleLightingBuffer);
        SafeDeleteFramebuffer(ref fb.HandlePickingBuffer);
        SafeDeleteFramebuffer(ref fb.HandleSelectionBuffer);
        SafeDeleteFramebuffer(ref fb.HandleScreenCopy);

        SafeDeleteTexture(ref fb.TexAlbedo);
        SafeDeleteTexture(ref fb.TexNormal);
        SafeDeleteTexture(ref fb.TexVelocity);
        SafeDeleteTexture(ref fb.TexDepth);
        SafeDeleteTexture(ref fb.TexLighting);
        SafeDeleteTexture(ref fb.TexPicking);
        SafeDeleteTexture(ref fb.TexSelection);
        SafeDeleteTexture(ref fb.TexSelectionDepth);
        SafeDeleteTexture(ref fb.TexScreenCopy);
    }

    private int CreateTexture(int width, int height, PixelInternalFormat internalFormat, PixelFormat format, PixelType type)
    {
        int tex;
        GL.GenTextures(1, out tex);
        GL.BindTexture(TextureTarget.Texture2D, tex);
        GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, width, height, 0, format, type, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.BindTexture(TextureTarget.Texture2D, 0);
        return tex;
    }

    public void BindGBuffer()
    {
        if (CurrentFramebuffer == null) return;
        GL.Viewport((int)offset.X, (int)offset.Y, CurrentFramebuffer.Width, CurrentFramebuffer.Height);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, CurrentFramebuffer.HandleGBuffer);
        GL.DrawBuffers(3, new[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1, DrawBuffersEnum.ColorAttachment2 });
    }

    public void BindLightingFramebuffer()
    {
        if (CurrentFramebuffer == null) return;
        GL.Viewport((int)offset.X, (int)offset.Y, CurrentFramebuffer.Width, CurrentFramebuffer.Height);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, CurrentFramebuffer.HandleLightingBuffer);
        GL.DrawBuffers(1, new[] { DrawBuffersEnum.ColorAttachment0 });
    }

    public void BindSelectionFramebuffer()
    {
        if (CurrentFramebuffer == null) return;
        GL.Viewport((int)offset.X, (int)offset.Y, CurrentFramebuffer.Width, CurrentFramebuffer.Height);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, CurrentFramebuffer.HandleSelectionBuffer);
        GL.DrawBuffers(1, new[] { DrawBuffersEnum.ColorAttachment0 });
    }
    
    public void BindPickingBuffer()
    {
        if (CurrentFramebuffer == null) return;
        GL.Viewport((int)offset.X, (int)offset.Y, CurrentFramebuffer.Width, CurrentFramebuffer.Height);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, CurrentFramebuffer.HandlePickingBuffer);
        GL.DrawBuffers(1, new[] { DrawBuffersEnum.ColorAttachment0 });
    }

    public void BindMainOutputBuffer()
    {
        if (CurrentFramebuffer == null) return;
        GL.Viewport((int)offset.X, (int)offset.Y, CurrentFramebuffer.Width, CurrentFramebuffer.Height);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, CurrentFramebuffer.HandleScreenCopy);
        GL.DrawBuffers(1, new[] { DrawBuffersEnum.ColorAttachment0 });
    }

    public void GetTextureIDs(out int[] texs)
    {
        if (CurrentFramebuffer == null)
        {
            texs = new int[4];
            return;
        }

        texs = new int[]
        {
            CurrentFramebuffer.TexAlbedo,
            CurrentFramebuffer.TexDepth,
            CurrentFramebuffer.TexNormal,
            CurrentFramebuffer.TexVelocity,
        };
    }

    public int GetTextureHandle(TextureHandle name)
    {
        if (CurrentFramebuffer == null) return -1;

        return name switch
        {
            TextureHandle.ALBEDO => CurrentFramebuffer.TexAlbedo,
            TextureHandle.PRIMARY_DEPTH => CurrentFramebuffer.TexDepth,
            TextureHandle.WORLD_NORMALS => CurrentFramebuffer.TexNormal,
            TextureHandle.VELOCITY => CurrentFramebuffer.TexVelocity,
            TextureHandle.SELECTION_BUFFER_MASK => CurrentFramebuffer.TexSelection,
            TextureHandle.SELECTION_BUFFER_DEPTH => CurrentFramebuffer.TexSelectionDepth,
            TextureHandle.LIGHTING_RESULT => CurrentFramebuffer.TexLighting,
            TextureHandle.COPY => CurrentFramebuffer.TexScreenCopy,
            _ => -1,
        };
    }

    public int GetLightingBufferID()
    {
        return CurrentFramebuffer?.TexLighting ?? -1;
    }
}
