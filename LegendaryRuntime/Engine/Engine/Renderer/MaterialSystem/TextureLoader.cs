using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MaterialSystem;
using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using LegendaryRenderer.LegendaryRuntime.Engine.AssetManagement;
using System.Security.Cryptography;
using System.IO;
using LegendaryRenderer.LegendaryRuntime.Engine.Editor;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Renderer.MaterialSystem;

public static class TextureLoader
{
    private static Dictionary<string, Texture> LoadedTextures = new Dictionary<string, Texture>();

    public static Texture LoadTexture(string path, bool HDR, string modelRoot = "", bool useModelRoot = false)
    {
        AssetCacheManager.EnsureInitialized();

        string originalInputPath = path;
        string fullPath;
        string effectiveBasePath = ""; // For security check

        if (useModelRoot)
        {
            // When useModelRoot is true, modelRoot IS the directory of the model file.
            // The 'path' is relative to this directory.
            effectiveBasePath = Path.GetFullPath(modelRoot); // The intended root directory for relative paths
            fullPath = Path.Combine(effectiveBasePath, path);
            // Console.WriteLine($"TextureLoader (useModelRoot=true): modelRoot='{modelRoot}', path='{path}', resolved fullPath='{fullPath}");
        }
        else
        {
            effectiveBasePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "LegendaryRuntime", "Resources"));
            fullPath = Path.Combine(effectiveBasePath, path);
            // Console.WriteLine($"TextureLoader (useModelRoot=false): ResourcesPath='{effectiveBasePath}', path='{path}', resolved fullPath='{fullPath}");
        }
        
        try
        {
             fullPath = Path.GetFullPath(fullPath); // Normalize the path (e.g. resolve . and ..)
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting full path for texture '{originalInputPath}' (initial combined: '{fullPath}'): {ex.Message}. Returning NULL texture.");
            return Texture.NullTexture().Reference();
        }

        // Security check: Ensure the final path is within the intended base directory
        // This is more critical for the non-modelRoot case, but good to be mindful of.
        if (!fullPath.StartsWith(Path.GetFullPath(effectiveBasePath)))
        {
            // Allow for case where effectiveBasePath might end with a slash and fullPath might not, or vice-versa after GetFullPath
            // A more robust check might involve comparing directory segments.
            // For now, a simple StartsWith after GetFullPath on both should be mostly effective.
            if (!(Path.GetFullPath(effectiveBasePath) + Path.DirectorySeparatorChar).Equals(Path.GetFullPath(Path.GetDirectoryName(fullPath) + Path.DirectorySeparatorChar)) && !fullPath.Equals(effectiveBasePath)){
                 Console.WriteLine($"Warning: Texture path '{fullPath}' resolved outside of its expected base directory '{effectiveBasePath}'. This might be a path traversal attempt or misconfiguration. Path requested: '{originalInputPath}'.");
                 // Depending on security policy, you might want to return null here.
                 // For now, we allow it but log a warning.
            }
        }

        string uniqueKey = fullPath;

        if (LoadedTextures.TryGetValue(uniqueKey, out Texture alreadyLoadedTexture))
        {
            return alreadyLoadedTexture.Reference();
        }

        if (!File.Exists(fullPath))
        {
            Console.WriteLine($"Texture file not found: {fullPath}. Returning NULL texture.");
            return Texture.NullTexture().Reference();
        }

        string fileHash = AssetCacheManager.CalculateFileHash(fullPath);
        if (fileHash != null)
        {
            if (AssetCacheManager.TryGetCachedSerializableTextureData(fileHash, fullPath, out SerializableTextureData cachedTexData))
            {
                Console.WriteLine($"Cache hit for texture: {uniqueKey} (Hash: {fileHash}). Loading from binary asset.");
                PixelInternalFormat pfi;
                PixelFormat pf;
                PixelType pt;
                CachedTexturePixelFormat ctpFormat = cachedTexData.PixelFormat;

                if (ctpFormat == CachedTexturePixelFormat.Rgba32)
                {
                    pfi = PixelInternalFormat.Rgba8;
                    pf = PixelFormat.Rgba;
                    pt = PixelType.UnsignedByte;
                }
                else if (ctpFormat == CachedTexturePixelFormat.Rgb48)
                {
                    pfi = PixelInternalFormat.Rgb16f;
                    pf = PixelFormat.Rgb;
                    pt = PixelType.HalfFloat;
                }
                else
                {
                    Console.WriteLine($"Unknown cached texture format: {ctpFormat} for {uniqueKey}. Cannot load from cache.");
                    goto CacheMiss;
                }

                try
                {
                    int textureId = GL.GenTexture();
                    GL.BindTexture(TextureTarget.Texture2D, textureId);
                    GL.TexImage2D(TextureTarget.Texture2D, 0, pfi, cachedTexData.Width, cachedTexData.Height, 0, pf, pt, cachedTexData.PixelData);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
                    GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
                    GL.BindTexture(TextureTarget.Texture2D, 0);

                    Texture tex = new Texture(cachedTexData.Width, cachedTexData.Height, textureId, pf, pfi, pt);
                    Console.WriteLine($"Successfully loaded texture from cache: {uniqueKey}");
                    LoadedTextures.Add(uniqueKey, tex);

                    // Generate icon if it doesn't exist
                    if (fileHash != null)
                    {
                        IconGenerator.GenerateTextureIcon(fullPath, fileHash);
                    }

                    return tex.Reference();
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Error creating OpenGL texture from cached data for {uniqueKey}: {ex.Message}. Falling back to standard load.");
                    goto CacheMiss;
                }
            }
        }
        CacheMiss:

        Console.WriteLine($"Cache miss or issue for texture: {uniqueKey}. Attempting standard load.");

        byte[] pixelDataForGpu;
        int imageWidth, imageHeight;
        PixelInternalFormat finalPFI;
        PixelFormat finalPF;
        PixelType finalPT;
        CachedTexturePixelFormat finalCtpFormat;

        if (!HDR)
        {
            try
            {
                var image = Image.Load<Rgba32>(fullPath);
                image.Mutate(x => x.Flip(FlipMode.Vertical));
                pixelDataForGpu = new byte[image.Width * image.Height * 4];
                image.CopyPixelDataTo(pixelDataForGpu);
                imageWidth = image.Width;
                imageHeight = image.Height;

                finalPT = PixelType.UnsignedByte;
                finalPF = PixelFormat.Rgba;
                finalPFI = PixelInternalFormat.Rgba8;
                finalCtpFormat = CachedTexturePixelFormat.Rgba32;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading LDR texture {fullPath} with ImageSharp: {ex.Message}. Returning NULL texture.");
                return Texture.NullTexture().Reference();
            }
        }
        else
        {
            try
            {
                var image = Image.Load<Rgb48>(fullPath);
                image.Mutate(x => x.Flip(FlipMode.Vertical));
                pixelDataForGpu = new byte[image.Width * image.Height * 6]; 
                image.CopyPixelDataTo(pixelDataForGpu);
                imageWidth = image.Width;
                imageHeight = image.Height;

                finalPT = PixelType.HalfFloat; 
                finalPF = PixelFormat.Rgb;
                finalPFI = PixelInternalFormat.Rgb16f; 
                finalCtpFormat = CachedTexturePixelFormat.Rgb48;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading HDR texture {fullPath} with ImageSharp: {ex.Message}. Returning NULL texture.");
                return Texture.NullTexture().Reference();
            }
        }

        try
        {
            int textureId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, textureId);
            GL.TexImage2D(TextureTarget.Texture2D, 0, finalPFI, imageWidth, imageHeight, 0, finalPF, finalPT, pixelDataForGpu);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            GL.BindTexture(TextureTarget.Texture2D, 0);

            Texture tex = new Texture(imageWidth, imageHeight, textureId, finalPF, finalPFI, finalPT);
            Console.WriteLine($"Successfully loaded texture (standard path): {uniqueKey}");
            LoadedTextures.Add(uniqueKey, tex);

            if (fileHash != null)
            {
                Console.WriteLine($"Storing texture in cache: {uniqueKey} (Hash: {fileHash})");
                SerializableTextureData texToCache = new SerializableTextureData(imageWidth, imageHeight, finalCtpFormat, pixelDataForGpu);
                AssetCacheManager.StoreCompiledTextureData(fileHash, fullPath, texToCache);
            }

            // Generate icon if it doesn't exist
            if (fileHash != null) // Also generate after standard load if hash is available
            {
                IconGenerator.GenerateTextureIcon(fullPath, fileHash);
            }

            return tex.Reference();
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Error creating OpenGL texture (standard path) for {uniqueKey}: {ex.Message}. Returning NULL texture.");
            return Texture.NullTexture().Reference();
        }
    }
}