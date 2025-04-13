using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Renderer.MaterialSystem;

public static class TextureLoader
{
    private static Dictionary<string, int> LoadedTextures = new Dictionary<string, int>();

    public static int LoadTexture(string path, bool HDR, string modelRoot = "", bool useModelRoot = false)
    {
        string root = Path.GetDirectoryName(modelRoot);
        if (useModelRoot)
        {
            path = Path.Combine(Path.Combine(root, "textures"), path);
            Console.WriteLine($"Looking for Textures in {path}.");
        }
        string uniqueKey = Path.GetFullPath(path);

        Console.WriteLine($"Attempting to Load Texture {uniqueKey}...");
        if (File.Exists(Path.GetFullPath(path)) && !LoadedTextures.ContainsKey(uniqueKey))
        {
            if (!HDR)
            {
                var image = Image.Load<Rgba32>(Path.GetFullPath(path)); // Load the image as RGBA

                // Flip the image vertically because OpenGL expects the origin at the bottom-left
                image.Mutate(x => x.Flip(FlipMode.Vertical));

                // Create a byte array to hold the pixel data
                var pixelData = new byte[image.Width * image.Height * 4]; // 4 bytes per pixel (RGBA)

                image.CopyPixelDataTo(pixelData);

                // Generate and bind a new OpenGL texture
                int textureId = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, textureId);

                var PT = PixelType.UnsignedByte;

                // Upload the texture data to the GPU
                GL.TexImage2D(TextureTarget.Texture2D,
                    0,
                    PixelInternalFormat.Rgba,
                    image.Width,
                    image.Height,
                    0,
                    PixelFormat.Rgba,
                    PT,
                    pixelData);

                // Set texture parameters
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

                GL.BindTexture(TextureTarget.Texture2D, 0);

                Console.WriteLine($"Loaded Texture: {uniqueKey}");
                LoadedTextures.Add(uniqueKey, textureId);
                return textureId;
            }
            else
            {
                var image = Image.Load<Rgb48>(Path.GetFullPath(path)); // Load the image as RGBA

                // Flip the image vertically because OpenGL expects the origin at the bottom-left
                image.Mutate(x => x.Flip(FlipMode.Vertical));

                // Create a byte array to hold the pixel data
                var pixelData = new byte[image.Width * image.Height * 6];

                image.CopyPixelDataTo(pixelData);

                // Generate and bind a new OpenGL texture
                int textureId = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, textureId);

                var PT = PixelType.HalfFloat;

                // Upload the texture data to the GPU
                GL.TexImage2D(TextureTarget.Texture2D,
                    0,
                    PixelInternalFormat.Rgb,
                    image.Width,
                    image.Height,
                    0,
                    PixelFormat.Rgb,
                    PT,
                    pixelData);

                // Set texture parameters
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

                GL.BindTexture(TextureTarget.Texture2D, 0);

                Console.WriteLine($"Loaded Texture: {uniqueKey}");
                LoadedTextures.Add(uniqueKey, textureId);
                return textureId;
            }
        }
        if (LoadedTextures.ContainsKey(uniqueKey))
        {
            Console.WriteLine($"Returning loaded Texture: {uniqueKey}");
            return LoadedTextures[uniqueKey];
        }
        return -1;
    }
}