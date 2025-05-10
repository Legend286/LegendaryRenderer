using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MaterialSystem;
using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Renderer.MaterialSystem;

public static class TextureLoader
{
    private static Dictionary<string, Texture> LoadedTextures = new Dictionary<string, Texture>();

    public static Texture LoadTexture(string path, bool HDR, string modelRoot = "", bool useModelRoot = false)
    {
        string root = Path.GetDirectoryName(modelRoot);
        if (useModelRoot)
        {
            path = Path.Combine(root, path);
            Console.WriteLine($"Looking for Textures in {path}.");
        }
        else
        {
            path = Path.Combine(AppContext.BaseDirectory, Path.Combine("LegendaryRuntime", Path.Combine("Resources", path)));
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
                var PF = PixelFormat.Rgba;
                var PFI = PixelInternalFormat.Rgba;

                // Upload the texture data to the GPU
                GL.TexImage2D(TextureTarget.Texture2D,
                    0,
                    PFI,
                    image.Width,
                    image.Height,
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

                Texture tex = new Texture(image.Width, image.Height, textureId, PF, PFI, PT);
                Console.WriteLine($"Loaded Texture: {uniqueKey}");
                LoadedTextures.Add(uniqueKey, tex);
                return tex;
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
                var PFI = PixelInternalFormat.Rgb;
                var PF = PixelFormat.Rgb;



                // Upload the texture data to the GPU
                GL.TexImage2D(TextureTarget.Texture2D,
                    0,
                    PFI,
                    image.Width,
                    image.Height,
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

                Texture tex = new Texture(image.Width, image.Height, textureId, PF, PFI, PT);
                Console.WriteLine($"Loaded Texture: {uniqueKey}");
                LoadedTextures.Add(uniqueKey, tex);
                return tex;
            }
        } 
        if (LoadedTextures.ContainsKey(uniqueKey))
        {
            Console.WriteLine($"Returning loaded Texture: {uniqueKey}");
            return LoadedTextures[uniqueKey].Reference();
        }
        else
        {
            Console.WriteLine($"Returning NULL texture");
            return Texture.NullTexture().Reference();
        }
    }
}