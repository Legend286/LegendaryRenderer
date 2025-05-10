using OpenTK.Graphics.OpenGL4;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MaterialSystem;

public class IESTextureLoader
{
    public static int LoadIESProfileAsTexture(string filePath)
    {
        IESProfile profile = IESProfile.Load(filePath);

        return LoadIESTexture(profile);
    }
    
    public static int LoadIESTexture(IESProfile profile)
    {
        int width = profile.VerticalAnglesCount;
        int height = profile.HorizontalAnglesCount;

        // Normalize candela values to [0, 1] for texture mapping
        float maxCandela = 0.0f;
        foreach (var value in profile.CandelaValues)
        {
            if (value > maxCandela)
                maxCandela = value;
        }

        // Prepare texture data
        float[] textureData = new float[width * height];
        for (int y = 0; y < height; y++) // Horizontal angles
        {
            for (int x = 0; x < width; x++) // Vertical angles
            {
                float normalizedValue = profile.CandelaValues[y, x] / maxCandela;
                textureData[y * width + x] = normalizedValue;
            }
        }

        // Generate OpenGL texture
        int textureID = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, textureID);

        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R32f, width, height, 0, PixelFormat.Red, PixelType.Float, textureData);

        // Set texture parameters for smooth sampling
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        GL.BindTexture(TextureTarget.Texture2D, 0); // Unbind texture
        return textureID;
    }
}