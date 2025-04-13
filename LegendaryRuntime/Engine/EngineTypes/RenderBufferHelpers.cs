using System.Dynamic;
using OpenTK.Graphics.OpenGL;
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

    private int HandleScreenCopy = -1;
    private int HandleGBuffer = -1;
    public static int HandleLightingBuffer = -1;
    public static int HandlePickingBuffer = -1;
    public static int HandleSelectionBuffer = -1;


    private int TextureHandleAlbedo;
    private int TextureHandleDepth;
    private int TextureHandleNormal;
    private int TextureHandleVelocity;

    private int TextureHandleLightingBuffer;

    private int TextureHandlePickingBuffer;

    private int TextureHandleSelectionBuffer;
    private int TextureHandleSelectionDepthBuffer;
    
    private int TextureHandleScreenCopy;
    
    public string Name { get; }

    public RenderBufferHelpers(PixelInternalFormat format, PixelInternalFormat depthFormat, int width, int height, string name = "Generic RenderTarget")
    {
        if (Instance == null)
        {
            Instance = this;
            Format = format;
            DepthFormat = depthFormat;
            Width = width;
            Height = height;
            Name = name;

            CreateFramebuffer(Width, Height, Format, DepthFormat);
        }
    }

    public bool ValidateFramebuffer()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, HandleGBuffer); 
        
        bool first = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == FramebufferErrorCode.FramebufferComplete;

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, HandlePickingBuffer);
        bool second = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == FramebufferErrorCode.FramebufferComplete;

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, HandleLightingBuffer);
        bool third = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == FramebufferErrorCode.FramebufferComplete;
        
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, HandleSelectionBuffer);
        bool fourth = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == FramebufferErrorCode.FramebufferComplete;

        return first && second && third && fourth;
    }

    public bool CreateFramebuffer(int width, int height, PixelInternalFormat format, PixelInternalFormat depthFormat)
    {
        HandleGBuffer = GL.GenFramebuffer();
        HandleLightingBuffer = GL.GenFramebuffer();
        HandlePickingBuffer = GL.GenFramebuffer();
        HandleSelectionBuffer = GL.GenFramebuffer();
        HandleScreenCopy = GL.GenFramebuffer();
        
        if (HandleGBuffer == -1)
        {
            throw new Exception("Failed to create GBuffer");
        }
        if (HandleLightingBuffer == -1)
        {
            throw new Exception("Failed to create Lighting Buffer");
        }
        if (HandlePickingBuffer == -1)
        {
            throw new Exception("Failed to create Picking Buffer");
        }
        if (HandleSelectionBuffer == -1)
        {
            throw new Exception("Failed to create Selection Buffer");
        }
        if (HandleScreenCopy == -1)
        {
            throw new Exception("Failed to create Screen Copy");
        }
        
        
        // GBUFFER
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, HandleGBuffer);

        GL.GenTextures(1, out TextureHandleAlbedo);
        GL.BindTexture(TextureTarget.Texture2D, TextureHandleAlbedo);
        GL.TexImage2D(TextureTarget.Texture2D, 0, format, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero );
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        
        GL.BindTexture(TextureTarget.Texture2D, 0);
        
        GL.GenTextures(1, out TextureHandleDepth);
        GL.BindTexture(TextureTarget.Texture2D, TextureHandleDepth);
        GL.TexImage2D(TextureTarget.Texture2D, 0, depthFormat, width, height, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMagFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);


        GL.BindTexture(TextureTarget.Texture2D, 0);
         
        GL.GenTextures(1, out TextureHandleNormal);
        GL.BindTexture(TextureTarget.Texture2D, TextureHandleNormal);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16Snorm, width, height, 0, PixelFormat.Rgba, PixelType.HalfFloat, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMagFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        GL.BindTexture(TextureTarget.Texture2D, 0);
        
        GL.GenTextures(1, out TextureHandleVelocity);
        GL.BindTexture(TextureTarget.Texture2D, TextureHandleVelocity);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rg16f, width, height, 0, PixelFormat.Rg, PixelType.HalfFloat, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMagFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        GL.BindTexture(TextureTarget.Texture2D, 0);
        
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, TextureHandleAlbedo, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, TextureHandleNormal, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment2, TextureTarget.Texture2D, TextureHandleVelocity, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, TextureHandleDepth, 0);
        
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        
        // LIGHT ACCUM BUFFER
        
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, HandleLightingBuffer);

        GL.GenTextures(1, out TextureHandleLightingBuffer);
        GL.BindTexture(TextureTarget.Texture2D, TextureHandleLightingBuffer);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, width, height, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero );
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        
        GL.BindTexture(TextureTarget.Texture2D, 0);

        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, TextureHandleLightingBuffer, 0);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        
        // PICKING BUFFER

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, HandlePickingBuffer);

        GL.GenTextures(1, out TextureHandlePickingBuffer);
        GL.BindTexture(TextureTarget.Texture2D, TextureHandlePickingBuffer);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, width, height, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        
        GL.BindTexture(TextureTarget.Texture2D, 0);

        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, TextureHandlePickingBuffer, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, TextureHandleDepth, 0);
        
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        
        // SELECTION BUFFER

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, HandleSelectionBuffer);

        GL.GenTextures(1, out TextureHandleSelectionBuffer);
        GL.BindTexture(TextureTarget.Texture2D, TextureHandleSelectionBuffer);
        GL.TexImage2D(TextureTarget.Texture2D, 0, format, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        GL.BindTexture(TextureTarget.Texture2D, 0);
        
        GL.GenTextures(1, out TextureHandleSelectionDepthBuffer);
        GL.BindTexture(TextureTarget.Texture2D, TextureHandleSelectionDepthBuffer);
        GL.TexImage2D(TextureTarget.Texture2D, 0, depthFormat, width, height, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, TextureHandleSelectionBuffer, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, TextureHandleSelectionDepthBuffer, 0);
        
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        
        // SCREEN COPY (TO OUTPUT TO MAIN FBO)

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, HandleScreenCopy);
        
        GL.GenTextures(1, out TextureHandleScreenCopy);
        GL.BindTexture(TextureTarget.Texture2D, TextureHandleScreenCopy);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, width, height, 0, PixelFormat.Rgba, PixelType.HalfFloat, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        GL.BindTexture(TextureTarget.Texture2D, 0);
        
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, TextureHandleScreenCopy, 0);
        //GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, TextureHandleDepth, 0);
        
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        
        
        
        Console.WriteLine($"Framebuffer {HandleGBuffer} Created.\n"
                          + $"Width: {width}, Height: {height}\n"
                          + $"Format: {Format.ToString()}\n"
                          + $"DepthFormat: {DepthFormat.ToString()}\n"
                          + $"Success: {ValidateFramebuffer()}");
        
        return ValidateFramebuffer();
    }

    public void BindGBuffer()
    {
        GL.Viewport(0, 0, Width, Height);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, HandleGBuffer);
      //  GLHelpers.CheckGLError("BindFramebuffer");

        // Set the draw buffers
        GL.DrawBuffers(3, new[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1, DrawBuffersEnum.ColorAttachment2 });
       // GLHelpers.CheckGLError("DrawBuffers");
    }

    public void BindLightingFramebuffer()
    {
        GL.Viewport(0, 0, Width, Height);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, HandleLightingBuffer);

        GL.DrawBuffers(1, new[] { DrawBuffersEnum.ColorAttachment0 });
    }

    public void BindSelectionFramebuffer()
    {
        GL.Viewport(0, 0, Width, Height);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, HandleSelectionBuffer);
        
        GL.DrawBuffers(1, new[] { DrawBuffersEnum.ColorAttachment0 });
    }

    public void BindMainOutputBuffer()
    {
        GL.Viewport(0, 0, Width, Height);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, HandleScreenCopy);
        
        GL.DrawBuffers(1, new[] { DrawBuffersEnum.ColorAttachment0 });
    }


    public void GetTextureIDs(out int[] texs)
    {
        int[] textures = new int[4];
        textures[0] = TextureHandleAlbedo;
        textures[1] = TextureHandleDepth;
        textures[2] = TextureHandleNormal;
        textures[3] = TextureHandleVelocity;
        texs = textures;
    }

    public int GetTextureHandle(TextureHandle name)
    {
        switch (name)
        {
            case TextureHandle.ALBEDO:
                return TextureHandleAlbedo;
          
            case TextureHandle.PRIMARY_DEPTH:
                return TextureHandleDepth;
               
            case TextureHandle.WORLD_NORMALS:
                return TextureHandleNormal;
     
            case TextureHandle.VELOCITY:
                return TextureHandleVelocity;
        
            case TextureHandle.SELECTION_BUFFER_MASK:
                return TextureHandleSelectionBuffer;
    
            case TextureHandle.SELECTION_BUFFER_DEPTH:
                return TextureHandleSelectionDepthBuffer;
            
            case TextureHandle.LIGHTING_RESULT:
                return TextureHandleLightingBuffer;
            
            case TextureHandle.COPY:
                return TextureHandleScreenCopy;
            
            default:
                return -1;
        }
    }

    public int GetLightingBufferID()
    {
        return TextureHandleLightingBuffer;
    }

    public void OnResizeFramebuffer(int width, int height)
    {
        ReleaseRenderTarget();
        CreateFramebuffer(width, height, Format, DepthFormat);
        Width = width;
        Height = height;
    }
    public void ReleaseRenderTarget()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.DeleteTexture(TextureHandleAlbedo);
        GL.DeleteTexture(TextureHandleDepth);
        GL.DeleteTexture(TextureHandleNormal);
        GL.DeleteTexture(TextureHandleVelocity);
        GL.DeleteTexture(HandleLightingBuffer);
        GL.DeleteFramebuffer(HandleGBuffer);
        GL.DeleteFramebuffer(HandleLightingBuffer);
    }
}
