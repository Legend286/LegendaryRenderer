using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer; // For MeshHasher, CombinedMesh
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MeshInstancing; // For MeshHasher, if needed for types
// Make sure Assimp is available if you directly use its types here. For now, we only need Mesh for HashMesh.
// using Assimp;

namespace LegendaryRenderer.LegendaryRuntime.Engine.AssetManagement
{
    public static class AssetCacheManager
    {
        private const string MeshManifestFileName = "asset_manifest.json";
        private const string TextureManifestFileName = "texture_manifest.json";
        private const string CacheSubDir = "Cache";
        private const string ContentSubDir = "Content";
        private const string CompiledTexturesSubDir = "CompiledTextures";

        private static string LegendaryRuntimePath;
        private static string CacheBasePath;
        private static string ContentBasePath;
        private static string CompiledTexturesPath;
        private static string MeshManifestFilePath;
        private static string TextureManifestFilePath;

        private static Dictionary<int, AssetManifestEntry> meshManifest;
        private static Dictionary<string, AssetManifestEntry> textureManifest;
        private static bool isInitialized = false;

        public static void EnsureInitialized()
        {
            if (isInitialized) return;

            string baseDir = AppContext.BaseDirectory;
            LegendaryRuntimePath = Path.Combine(baseDir, "LegendaryRuntime"); 
            
            CacheBasePath = Path.Combine(LegendaryRuntimePath, CacheSubDir);
            ContentBasePath = Path.Combine(LegendaryRuntimePath, ContentSubDir);
            CompiledTexturesPath = Path.Combine(CacheBasePath, CompiledTexturesSubDir);
            MeshManifestFilePath = Path.Combine(CacheBasePath, MeshManifestFileName);
            TextureManifestFilePath = Path.Combine(CacheBasePath, TextureManifestFileName);

            Console.WriteLine($"AssetCacheManager: LegendaryRuntimePath set to: {LegendaryRuntimePath}");
            Console.WriteLine($"AssetCacheManager: CacheBasePath set to: {CacheBasePath}");
            Console.WriteLine($"AssetCacheManager: ContentBasePath set to: {ContentBasePath}");
            Console.WriteLine($"AssetCacheManager: CompiledTexturesPath set to: {CompiledTexturesPath}");
            Console.WriteLine($"AssetCacheManager: MeshManifestFilePath set to: {MeshManifestFilePath}");
            Console.WriteLine($"AssetCacheManager: TextureManifestFilePath set to: {TextureManifestFilePath}");

            if (!Directory.Exists(CacheBasePath))
            {
                try { Directory.CreateDirectory(CacheBasePath); }
                catch (Exception ex) { Console.WriteLine($"Failed to create CacheBasePath {CacheBasePath}: {ex.Message}"); return; }
            }
            if (!Directory.Exists(ContentBasePath))
            {
                try { Directory.CreateDirectory(ContentBasePath); }
                catch (Exception ex) { Console.WriteLine($"Failed to create ContentBasePath {ContentBasePath}: {ex.Message}"); return; }
            }
            if (!Directory.Exists(CompiledTexturesPath))
            {
                try { Directory.CreateDirectory(CompiledTexturesPath); }
                catch (Exception ex) { Console.WriteLine($"Failed to create CompiledTexturesPath {CompiledTexturesPath}: {ex.Message}"); return; }
            }

            LoadMeshManifest();
            LoadTextureManifest();
            isInitialized = true;
        }

        private static void LoadMeshManifest()
        {
            if (File.Exists(MeshManifestFilePath))
            {
                try
                {
                    string json = File.ReadAllText(MeshManifestFilePath);
                    meshManifest = JsonSerializer.Deserialize<Dictionary<int, AssetManifestEntry>>(json) ?? new Dictionary<int, AssetManifestEntry>();
                    Console.WriteLine($"Loaded mesh manifest from {MeshManifestFilePath} with {meshManifest.Count} entries.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading mesh manifest from {MeshManifestFilePath}: {ex.Message}. Creating new mesh manifest.");
                    meshManifest = new Dictionary<int, AssetManifestEntry>();
                }
            }
            else
            {
                Console.WriteLine($"Mesh manifest not found at {MeshManifestFilePath}. Creating new mesh manifest.");
                meshManifest = new Dictionary<int, AssetManifestEntry>();
            }
        }

        private static void SaveMeshManifest()
        {
            if (meshManifest == null)
            {
                Console.WriteLine("Mesh manifest is null, cannot save.");
                return;
            }
            try
            {
                string json = JsonSerializer.Serialize(meshManifest, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(MeshManifestFilePath, json);
                Console.WriteLine($"Saved mesh manifest to {MeshManifestFilePath} with {meshManifest.Count} entries.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving mesh manifest to {MeshManifestFilePath}: {ex.Message}");
            }
        }

        private static void LoadTextureManifest()
        {
            if (File.Exists(TextureManifestFilePath))
            {
                try
                {
                    string json = File.ReadAllText(TextureManifestFilePath);
                    textureManifest = JsonSerializer.Deserialize<Dictionary<string, AssetManifestEntry>>(json) ?? new Dictionary<string, AssetManifestEntry>();
                    Console.WriteLine($"Loaded texture manifest from {TextureManifestFilePath} with {textureManifest.Count} entries.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading texture manifest from {TextureManifestFilePath}: {ex.Message}. Creating new texture manifest.");
                    textureManifest = new Dictionary<string, AssetManifestEntry>();
                }
            }
            else
            {
                Console.WriteLine($"Texture manifest not found at {TextureManifestFilePath}. Creating new texture manifest.");
                textureManifest = new Dictionary<string, AssetManifestEntry>();
            }
        }

        private static void SaveTextureManifest()
        {
            if (textureManifest == null)
            {
                Console.WriteLine("Texture manifest is null, cannot save.");
                return;
            }
            try
            {
                string json = JsonSerializer.Serialize(textureManifest, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(TextureManifestFilePath, json);
                Console.WriteLine($"Saved texture manifest to {TextureManifestFilePath} with {textureManifest.Count} entries.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving texture manifest to {TextureManifestFilePath}: {ex.Message}");
            }
        }
        
        public static string CalculateFileHash(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"CalculateFileHash: File not found at {filePath}");
                return null;
            }
            try
            {
                using (var sha256 = SHA256.Create())
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        byte[] hashBytes = sha256.ComputeHash(stream);
                        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error calculating file hash for {filePath}: {ex.Message}");
                return null;
            }
        }

        public static bool TryGetCachedMeshData(int meshContentHash, string originalFilePath, out byte[] meshData)
        {
            EnsureInitialized();
            meshData = null;

            if (string.IsNullOrEmpty(originalFilePath) || !File.Exists(originalFilePath))
            {
                Console.WriteLine($"TryGetCachedMeshData: Original file path '{originalFilePath}' is invalid or file does not exist.");
                return false;
            }

            if (meshManifest.TryGetValue(meshContentHash, out var entry))
            {
                string currentFileHash = CalculateFileHash(originalFilePath);
                if (currentFileHash == null) return false;

                if (entry.OriginalFileHash != currentFileHash)
                {
                     Console.WriteLine($"Original file {originalFilePath} for mesh content hash {meshContentHash} has changed. Invalidating cache entry.");
                     meshManifest.Remove(meshContentHash); 
                     SaveMeshManifest();
                     return false;
                }

                string fullBinaryPath = Path.Combine(LegendaryRuntimePath, entry.BinaryAssetPath);
                if (File.Exists(fullBinaryPath))
                {
                    try { meshData = File.ReadAllBytes(fullBinaryPath); Console.WriteLine($"Loaded compiled mesh data for hash {meshContentHash} from {fullBinaryPath}"); return true; }
                    catch (Exception ex) { Console.WriteLine($"Error reading compiled mesh from {fullBinaryPath}: {ex.Message}"); return false; }
                }
                else
                {
                    Console.WriteLine($"Compiled mesh file not found at {fullBinaryPath}. Removing manifest entry.");
                    meshManifest.Remove(meshContentHash);
                    SaveMeshManifest();
                    return false;
                }
            }
            return false;
        }

        public static string StoreCompiledMesh(int meshContentHash, string originalFilePath, byte[] meshData)
        {
            EnsureInitialized();
            string modelName = Path.GetFileNameWithoutExtension(originalFilePath);
            string relativeModelDir = Path.Combine(ContentSubDir, modelName);
            string meshAssetFileName = $"{meshContentHash}.meshasset";
            string relativeBinaryPath = Path.Combine(relativeModelDir, meshAssetFileName);
            
            string fullTargetDirectory = Path.Combine(LegendaryRuntimePath, relativeModelDir);
            string fullBinaryPath = Path.Combine(fullTargetDirectory, meshAssetFileName);
            
            try
            {
                Directory.CreateDirectory(fullTargetDirectory);
                File.WriteAllBytes(fullBinaryPath, meshData);
                string originalFileContentHash = CalculateFileHash(originalFilePath);
                meshManifest[meshContentHash] = new AssetManifestEntry { OriginalFilePathHint = originalFilePath, BinaryAssetPath = relativeBinaryPath, LastCompiledUtc = DateTime.UtcNow, OriginalFileHash = originalFileContentHash ?? "" };
                SaveMeshManifest();
                Console.WriteLine($"Stored compiled mesh for content hash {meshContentHash} (from original: {originalFilePath}) to {fullBinaryPath}");
                return relativeBinaryPath;
            }
            catch (Exception ex) { Console.WriteLine($"Error storing compiled mesh for content hash {meshContentHash} to {fullBinaryPath}: {ex.Message}"); return null; }
        }

        // Method to get the full path to a compiled asset, if needed externally
        public static string GetFullBinaryAssetPath(string relativeBinaryPath)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(relativeBinaryPath)) return null;
            return Path.Combine(LegendaryRuntimePath, relativeBinaryPath);
        }

        // --- Texture Caching Methods ---
        public static bool TryGetCachedSerializableTextureData(string textureFileHash, string originalFilePath, out SerializableTextureData loadedTextureData)
        {
            EnsureInitialized();
            loadedTextureData = null;
            if (string.IsNullOrEmpty(originalFilePath) || !File.Exists(originalFilePath)) { Console.WriteLine($"TryGetCachedSerializableTextureData: Original file path '{originalFilePath}' is invalid or file does not exist."); return false; }

            if (textureManifest.TryGetValue(textureFileHash, out var entry))
            {
                string currentDiskFileHash = CalculateFileHash(originalFilePath);
                if (currentDiskFileHash == null) return false;

                if (entry.OriginalFileHash != currentDiskFileHash) 
                {
                     Console.WriteLine($"Original texture file {originalFilePath} (hash {textureFileHash}) has changed on disk (new hash: {currentDiskFileHash}). Invalidating cache entry.");
                     textureManifest.Remove(textureFileHash);
                     SaveTextureManifest();
                     return false;
                }

                string fullBinaryPath = Path.Combine(LegendaryRuntimePath, entry.BinaryAssetPath);
                if (File.Exists(fullBinaryPath))
                {
                    try
                    {
                        using var fileStream = File.OpenRead(fullBinaryPath);
                        using var reader = new BinaryReader(fileStream);
                        loadedTextureData = SerializableTextureData.Deserialize(reader);
                        Console.WriteLine($"Loaded compiled texture data for file hash {textureFileHash} from {fullBinaryPath}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading/deserializing compiled texture from {fullBinaryPath}: {ex.Message}");
                        textureManifest.Remove(textureFileHash);
                        SaveTextureManifest();
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine($"Compiled texture file not found at {fullBinaryPath} for file hash {textureFileHash}. Removing manifest entry.");
                    textureManifest.Remove(textureFileHash);
                    SaveTextureManifest();
                    return false;
                }
            }
            return false;
        }

        public static string StoreCompiledTextureData(string textureFileHash, string originalFilePath, SerializableTextureData textureDataToCompile)
        {
            EnsureInitialized();
            string relativeDir = Path.Combine(CacheSubDir, CompiledTexturesSubDir);
            string binaryFileName = $"{textureFileHash}.textureasset"; 
            string relativeBinaryPath = Path.Combine(relativeDir, binaryFileName);
            string fullBinaryPath = Path.Combine(LegendaryRuntimePath, relativeBinaryPath);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullBinaryPath));
                using var fileStream = File.Create(fullBinaryPath);
                using var writer = new BinaryWriter(fileStream);
                textureDataToCompile.Serialize(writer);
                
                textureManifest[textureFileHash] = new AssetManifestEntry 
                { 
                    OriginalFilePathHint = originalFilePath, 
                    BinaryAssetPath = relativeBinaryPath, 
                    LastCompiledUtc = DateTime.UtcNow, 
                    OriginalFileHash = textureFileHash
                };
                SaveTextureManifest();
                Console.WriteLine($"Stored compiled texture for file hash {textureFileHash} (from original: {originalFilePath}) to {fullBinaryPath}");
                return relativeBinaryPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error storing compiled texture for file hash {textureFileHash} to {fullBinaryPath}: {ex.Message}");
                return null;
            }
        }

        public static bool TryGetManifestEntry(string originalModelFilePath, out AssetManifestEntry? entry, out int meshContentHash)
        {
            EnsureInitialized(); // Make sure manifests are loaded
            entry = null;
            meshContentHash = 0;

            if (meshManifest != null)
            {
                foreach (var kvp in meshManifest)
                {
                    // Key of meshManifest is meshContentHash (int), Value is AssetManifestEntry
                    if (kvp.Value.OriginalFilePathHint == originalModelFilePath)
                    {
                        entry = kvp.Value;
                        meshContentHash = kvp.Key; // The key is the meshContentHash
                        return true;
                    }
                }
            }
            return false;
        }
    }
} 