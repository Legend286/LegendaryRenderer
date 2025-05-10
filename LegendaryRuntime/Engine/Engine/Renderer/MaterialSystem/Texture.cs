using OpenTK.Graphics.OpenGL;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MaterialSystem;

public class Texture
{
    private int GLTextureID;
    private PixelFormat PixelFormat;
    private PixelInternalFormat ImageFormat;
    private PixelType PixelType;
    public int Width { get; private set; }
    public int Height { get; private set; }
    private int RefCount;

    public Texture(int width, int height, int id, PixelFormat pixelFormat, PixelInternalFormat imageFormat, PixelType pixelType)
    {
        GLTextureID = id;
        PixelFormat = pixelFormat;
        ImageFormat = imageFormat;
        PixelType = pixelType;
        Width = width;
        Height = height;
    }

    public int GetGLTexture()
    {
        return GLTextureID;
    }

    public Texture Reference()
    {
        RefCount++;
        return this;
    }
    private static int nullTexID = -1;
    private static Texture NullTextureObject;
    
    public static Texture NullTexture()
    {
        if (nullTexID == -1)
        {
            int textureId = GL.GenTexture();
            var pixelData = new byte[1 * 4];

            pixelData[0] = 255;
            pixelData[1] = 255;
            pixelData[2] = 255;
            pixelData[3] = 255;
            
            // 4 bytes per pixel (RGBA)
            int width = 1;
            int height = 1;
            GL.BindTexture(TextureTarget.Texture2D, textureId);

            var PT = PixelType.UnsignedByte;
            var PF = PixelFormat.Rgba;
            var PFI = PixelInternalFormat.Rgba;

            // Upload the texture data to the GPU
            GL.TexImage2D(TextureTarget.Texture2D,
                0,
                PFI,
                width,
                height,
                0,
                PF,
                PT,
                pixelData);

            // Set texture parameters
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            GL.BindTexture(TextureTarget.Texture2D, 0);

            NullTextureObject = new Texture(width, height, textureId, PF, PFI, PT);
            return NullTextureObject.Reference();
        } 
        return NullTextureObject.Reference();
    }
    
    public void Dispose()
    {
        if (RefCount > 0)
        {
            RefCount--;
        }
        else
        {
            GL.DeleteTexture(GLTextureID);
            GLTextureID = -1;
        }
    }
}