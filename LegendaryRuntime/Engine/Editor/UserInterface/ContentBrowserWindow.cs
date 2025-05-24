using ImGuiNET;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryRenderer.LegendaryRuntime.Engine.AssetManagement; // For AssetCacheManager
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer; // For MeshHasher (potentially)

namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor.UserInterface
{
    public class ContentBrowserWindow
    {
        public enum BrowserItemType { Folder, Model, Texture, File, CompiledMesh, CompiledTexture, Unsupported }

        private struct BrowserItem
        {
            public string Name;
            public string FullPath;
            public BrowserItemType ItemType;
            public int UiIconTextureID; // 0 if not loaded
            public string AssetIdentifier; // Path for files, or specific hash for icon lookup if known
        }

        private string _currentPath;
        private List<BrowserItem> _displayedItems = new List<BrowserItem>();
        private readonly List<string> _rootAssetPaths = new List<string>();
        private readonly Dictionary<string, int> _uiIconTextureCache = new Dictionary<string, int>(); // icon_file_path -> gl_texture_id
        
        private int _folderIconTexID = 0;
        private int _fileIconTexID = 0;
        private const int IconSize = 64; // Display size in ImGui

        // Path to the directory where default editor icons are stored.
        private static string DefaultEditorIconPath;

        public bool _isOpen = true;

        public ContentBrowserWindow()
        {
            string legendaryRuntimePath = Path.Combine(AppContext.BaseDirectory, "LegendaryRuntime");
            // string resourcesPath = Path.Combine(legendaryRuntimePath, "Resources"); // No longer an individual root
            // string contentPath = Path.Combine(legendaryRuntimePath, "Content"); 
            // string cachePath = Path.Combine(legendaryRuntimePath, "Cache");
            
            _rootAssetPaths.Clear(); // Ensure it's clean before adding the single true root
            _rootAssetPaths.Add(Path.GetFullPath(legendaryRuntimePath)); // Add LegendaryRuntime as the single root
            
            // Define the path for default editor icons (still inside Resources/Editor relative to LegendaryRuntime)
            string resourcesEditorPath = Path.Combine(legendaryRuntimePath, "Resources", "Editor");
            DefaultEditorIconPath = resourcesEditorPath; 
            InitializeDefaultIcons(); // Load default icons on construction

            NavigateTo(legendaryRuntimePath); // Initial navigation to the LegendaryRuntime folder itself
        }

        public void InitializeDefaultIcons()
        {
            string folderIconFullPath = Path.Combine(DefaultEditorIconPath, "default_folder_icon.png");
            string fileIconFullPath = Path.Combine(DefaultEditorIconPath, "default_file_icon.png");

            _folderIconTexID = LoadPngToTexture(folderIconFullPath);
            _fileIconTexID = LoadPngToTexture(fileIconFullPath);

            if (_folderIconTexID == 0)
            {
                Console.WriteLine($"Warning: Failed to load default folder icon from {folderIconFullPath}");
            }
            if (_fileIconTexID == 0)
            {
                Console.WriteLine($"Warning: Failed to load default file icon from {fileIconFullPath}");
            }
        }

        private void NavigateTo(string path)
        {
            if (Directory.Exists(path))
            {
                _currentPath = Path.GetFullPath(path);
                ScanCurrentDirectory();
            }
            else
            {
                Console.WriteLine($"ContentBrowser: Path not found or not a directory: {path}");
            }
        }

        private void ScanCurrentDirectory()
        { 
            _displayedItems.Clear();
            if (string.IsNullOrEmpty(_currentPath)) return;

            try
            {
                // Add directories
                foreach (var dirPath in Directory.GetDirectories(_currentPath))
                {
                    // If we are at a root path (e.g., LegendaryRuntime), explicitly hide the "Cache" directory itself.
                    bool isRootPath = _rootAssetPaths.Contains(Path.GetFullPath(_currentPath));
                    string dirName = Path.GetFileName(dirPath);

                    if (isRootPath && dirName.Equals("Cache", StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Skip adding the Cache directory to the displayed items
                    }

                    _displayedItems.Add(new BrowserItem
                    {
                        Name = dirName,
                        FullPath = dirPath,
                        ItemType = BrowserItemType.Folder,
                        AssetIdentifier = dirPath
                    });
                }

                // Add files
                foreach (var filePath in Directory.GetFiles(_currentPath))
                {
                    string extension = Path.GetExtension(filePath).ToLowerInvariant();
                    BrowserItemType type = BrowserItemType.File;
                    string assetIdentifier = filePath; // Default to full path

                    // Crude type detection, can be expanded
                    if (new[] { ".fbx", ".obj", ".gltf", ".glb" }.Contains(extension)) type = BrowserItemType.Model;
                    else if (new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tga" }.Contains(extension)) type = BrowserItemType.Texture;
                    else if (extension == ".meshasset")
                    {
                        type = BrowserItemType.CompiledMesh;
                        assetIdentifier = Path.GetFileNameWithoutExtension(filePath); // This is the meshContentHash
                    }
                    else if (extension == ".textureasset")
                    {
                        type = BrowserItemType.CompiledTexture;
                        assetIdentifier = Path.GetFileNameWithoutExtension(filePath); // This is the textureFileHash
                    }
                    else if (extension == ".cs" || extension == ".txt") type = BrowserItemType.File; // Keep as generic file
                    else type = BrowserItemType.Unsupported; // Or just don't add unsupported

                    if (type != BrowserItemType.Unsupported)
                    {
                         _displayedItems.Add(new BrowserItem
                        {
                            Name = Path.GetFileName(filePath),
                            FullPath = filePath,
                            ItemType = type,
                            AssetIdentifier = assetIdentifier
                        });
                    }
                }
                _displayedItems = _displayedItems.OrderBy(item => item.ItemType).ThenBy(item => item.Name).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning directory {_currentPath}: {ex.Message}");
            }
        }

        private int LoadPngToTexture(string filePath)
        {
            if (!File.Exists(filePath)) return 0;
            try
            {
                using (Image<Rgba32> image = Image.Load<Rgba32>(filePath))
                {
                    // ImageSharp loads with origin at top-left. OpenGL expects bottom-left for TexImage2D.
                    // So, flip it vertically before getting pixel data for GL.
                    image.Mutate(x => x.Flip(FlipMode.Vertical)); 

                    int handle = GL.GenTexture();
                    GL.BindTexture(TextureTarget.Texture2D, handle);

                    // Get pixel data as byte array
                    byte[] pixelData = new byte[image.Width * image.Height * 4];
                    image.CopyPixelDataTo(pixelData);

                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, image.Width, image.Height, 0,
                                  OpenTK.Graphics.OpenGL.PixelFormat.Rgba, PixelType.UnsignedByte, pixelData);

                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                    GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

                    GL.BindTexture(TextureTarget.Texture2D, 0);
                    return handle;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load UI icon texture '{filePath}': {ex.Message}");
                return 0;
            }
        }

        private int GetUiIconForItem(BrowserItem item)
        {
            if (item.UiIconTextureID != 0) return item.UiIconTextureID;

            string iconFilePath = null;
            switch (item.ItemType)
            {
                case BrowserItemType.Folder:
                    // Later: Use _folderIconTexID if loaded, or load a specific default folder icon png
                    // For now, returning 0 will mean ImGui might show a blank button or we skip image
                    return _folderIconTexID; 

                case BrowserItemType.Model:
                    // Try to get the manifest entry and the mesh content hash for this model file path.
                    // A model file can have multiple meshes; this will get the first one found.
                    if (AssetCacheManager.TryGetManifestEntry(item.FullPath, out var manifestEntry, out int meshContentHash))
                    {
                        // Use the meshContentHash from the manifest entry for the icon
                        iconFilePath = IconGenerator.GetMeshIconPath(meshContentHash);
                    }
                    break;

                case BrowserItemType.Texture:
                    string textureFileHash = AssetCacheManager.CalculateFileHash(item.FullPath);
                    if (!string.IsNullOrEmpty(textureFileHash))
                    {
                        iconFilePath = IconGenerator.GetTextureIconPath(textureFileHash);
                    }
                    break;
                
                case BrowserItemType.CompiledMesh:
                    if (int.TryParse(item.AssetIdentifier, out int compiledMeshHash))
                    {
                        iconFilePath = IconGenerator.GetMeshIconPath(compiledMeshHash);
                    }
                    else
                    {
                        Console.WriteLine($"ContentBrowser: Could not parse mesh hash from {item.AssetIdentifier} for {item.FullPath}");
                    }
                    break;

                case BrowserItemType.CompiledTexture:
                    iconFilePath = IconGenerator.GetTextureIconPath(item.AssetIdentifier); // AssetIdentifier is the textureFileHash
                    break;

                case BrowserItemType.File:
                    return _fileIconTexID; // Later: Use specific default file icon
            }

            if (!string.IsNullOrEmpty(iconFilePath) && File.Exists(iconFilePath))
            {
                if (_uiIconTextureCache.TryGetValue(iconFilePath, out int cachedTexId))
                {
                    // item.UiIconTextureID = cachedTexId; // Assign to item for next time? Be careful with struct copies.
                    return cachedTexId;
                }
                int newTexId = LoadPngToTexture(iconFilePath);
                if (newTexId != 0)
                {
                    _uiIconTextureCache[iconFilePath] = newTexId;
                    // item.UiIconTextureID = newTexId; // Assign to item for next time? Be careful with struct copies.
                    return newTexId;
                }
            }
            // Fallback if specific icon not found or failed to load
            return (item.ItemType == BrowserItemType.Model || item.ItemType == BrowserItemType.Texture || item.ItemType == BrowserItemType.CompiledMesh || item.ItemType == BrowserItemType.CompiledTexture) ? 0 : _fileIconTexID; // Fallback to generic file or nothing
        }

        private static string GetTrimmedName(string name, int maxDisplayLength = 10)
        {
            if (name.Length <= maxDisplayLength)
            {
                return name;
            }
            // Ensure we don't get a negative length for substring if maxDisplayLength is < 3
            int trimLength = Math.Max(0, maxDisplayLength - 3);
            return name.Substring(0, trimLength) + "...";
        }

        public void Draw()
        {
            if (!_isOpen) return;

            ImGui.SetNextWindowSize(new System.Numerics.Vector2(800, 600), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Content Browser", ref _isOpen))
            {
                // Navigation
                if (ImGui.Button("Up"))
                {
                    string parentDir = Path.GetDirectoryName(_currentPath);
                    // Allow going up if parentDir is valid and _currentPath is not one of the defined root paths.
                    // Path.GetFullPath is used to ensure consistent comparison.
                    if (!string.IsNullOrEmpty(parentDir) && !_rootAssetPaths.Contains(Path.GetFullPath(_currentPath)))
                    {
                        NavigateTo(parentDir);
                    }
                }
                ImGui.SameLine();
                ImGui.TextWrapped($"Path: {_currentPath}");
                ImGui.Separator();

                // Items Grid
                float itemWidth = IconSize + 16; // Icon + padding
                float itemHeight = IconSize + ImGui.GetTextLineHeightWithSpacing() + 16; // Icon + text + padding

                float availableWidth = ImGui.GetContentRegionAvail().X;
                int itemsPerRow = Math.Max(1, (int)(availableWidth / itemWidth));
                int currentColumn = 0;

                for(int i=0; i < _displayedItems.Count; i++)
                {
                    BrowserItem item = _displayedItems[i]; 
                    int iconTexId = GetUiIconForItem(item);
                    // _displayedItems[i] = item; // Not strictly needed if item is a struct and GetUiIconForItem doesn't modify its copy

                    string displayName = GetTrimmedName(item.Name);

                    ImGui.PushID(item.FullPath);
                    ImGui.BeginGroup(); 
                    if (iconTexId != 0)
                    {
                        if (ImGui.ImageButton(item.Name, (IntPtr)iconTexId, new System.Numerics.Vector2(IconSize, IconSize), new System.Numerics.Vector2(1,1), new System.Numerics.Vector2(0,0))) 
                        {
                            if (item.ItemType == BrowserItemType.Folder) NavigateTo(item.FullPath);
                            // Else: handle file click (e.g., selection)
                        }
                    }
                    else
                    {
                        // Fallback for no icon (e.g., a button with text)
                        if (ImGui.Button(displayName, new System.Numerics.Vector2(IconSize, IconSize))) 
                        {
                            if (item.ItemType == BrowserItemType.Folder) NavigateTo(item.FullPath);
                            // Else: handle file click (e.g., selection)
                        }
                    }

                    // Add drag and drop source for models (moved outside the if/else blocks)
                    if (item.ItemType == BrowserItemType.Model || item.ItemType == BrowserItemType.CompiledMesh)
                    {
                        // Check if FullPath is valid before starting drag-drop
                        if (!string.IsNullOrEmpty(item.FullPath) && ImGui.BeginDragDropSource())
                        {
                            unsafe
                            {
                                byte[] pathBytes = System.Text.Encoding.UTF8.GetBytes(item.FullPath);
                                fixed (byte* ptr = pathBytes) // Pin the byte array to get a stable pointer
                                {
                                    ImGui.SetDragDropPayload("MODEL_ASSET", (IntPtr)ptr, (uint)pathBytes.Length);
                                }
                            }
                            
                            // Use a safe name for display
                            string displayNameForDrag = string.IsNullOrEmpty(item.Name) ? "Dragging Model" : $"Dragging {item.Name}";
                            ImGui.Text(displayNameForDrag);
                            
                            ImGui.EndDragDropSource();
                        }
                    }

                    ImGui.TextWrapped(displayName);
                    ImGui.EndGroup();
                    ImGui.PopID();

                    currentColumn++;
                    if (currentColumn < itemsPerRow)
                    {
                        ImGui.SameLine();
                    }
                    else
                    {
                        currentColumn = 0;
                    }
                }
            }
            ImGui.End();
        }

        public void ToggleOpen() { _isOpen = !_isOpen; }
    }
} 