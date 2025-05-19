using System.IO;
using LegendaryRenderer.LegendaryRuntime.Engine.AssetManagement; // For AssetCacheManager (if needed for path)
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using LegendaryRenderer.Shaders; // Added for ShaderManager and ShaderFile
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MaterialSystem; // For TextureLoader
using OpenTK.Graphics.OpenGL; // For OpenGL calls
using OpenTK.Mathematics;
using Assimp;
using System.Collections.Generic; // For List<float>
using System.Linq;
using LegendaryRenderer.LegendaryRuntime.Engine.Renderer.MaterialSystem; // For Min/Max on bounding box
using System; // Explicitly for System.Console

namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor
{
    public static class IconGenerator
    {
        private static string baseIconPath;
        private static string meshIconPath;
        private static string textureIconPath;
        private const int DefaultIconSize = 128;

        public static void Initialize()
        {
            string legendaryRuntimePath = Path.Combine(AppContext.BaseDirectory, "LegendaryRuntime");
            string cacheEditorPath = Path.Combine(legendaryRuntimePath, "Cache", "Editor");
            
            baseIconPath = Path.Combine(cacheEditorPath, "Icons");
            meshIconPath = Path.Combine(baseIconPath, "Meshes");
            textureIconPath = Path.Combine(baseIconPath, "Textures");
            Console.WriteLine($"IconGenerator.Initialize: baseIconPath SET TO: '{Path.GetFullPath(baseIconPath)}'");
            Console.WriteLine($"IconGenerator.Initialize: meshIconPath SET TO: '{Path.GetFullPath(meshIconPath)}'");
            Console.WriteLine($"IconGenerator.Initialize: textureIconPath SET TO: '{Path.GetFullPath(textureIconPath)}'");

            EnsureIconCacheDirectoryExists();
        }

        public static void EnsureIconCacheDirectoryExists()
        {
            if (string.IsNullOrEmpty(baseIconPath)) Initialize(); // Ensure paths are set

            Directory.CreateDirectory(baseIconPath);
            Directory.CreateDirectory(meshIconPath);
            Directory.CreateDirectory(textureIconPath);
        }

        public static string GetMeshIconPath(int meshHash)
        {
            if (string.IsNullOrEmpty(meshIconPath)) Initialize();
            return Path.Combine(meshIconPath, $"{meshHash}.png");
        }

        public static string GetTextureIconPath(string textureFileHash)
        {
            if (string.IsNullOrEmpty(textureIconPath)) Initialize();
            return Path.Combine(textureIconPath, $"{textureFileHash}.png");
        }

        public static bool GenerateTextureIcon(string originalTexturePath, string textureFileHash, int iconSize = DefaultIconSize)
        {
            Console.WriteLine($"IconGenerator.GenerateTextureIcon: Current textureIconPath at entry: '{ (string.IsNullOrEmpty(textureIconPath) ? "NULL OR EMPTY" : Path.GetFullPath(textureIconPath)) }'");
            if (string.IsNullOrEmpty(textureIconPath)) 
            {
                Console.WriteLine("IconGenerator.GenerateTextureIcon: textureIconPath is NULL/EMPTY, calling Initialize().");
                Initialize(); 
            }
            
            Console.WriteLine($"IconGenerator: GenerateTextureIcon CALLED for: originalPath='{originalTexturePath}', hash='{textureFileHash}'"); 

            string iconPath = GetTextureIconPath(textureFileHash);
            // Ensure iconPath is absolute for File.Exists if GetTextureIconPath might return relative somehow (though unlikely with current setup)
            string fullIconPath = Path.GetFullPath(iconPath);
            Console.WriteLine($"IconGenerator: Icon will be saved to/checked at (relative attempt): '{iconPath}'");
            Console.WriteLine($"IconGenerator: Icon will be saved to/checked at ACTUAL FULL PATH: '{fullIconPath}'"); 

            if (File.Exists(fullIconPath)) // Use fullIconPath for the check
            {
                Console.WriteLine($"IconGenerator: Icon already exists at '{fullIconPath}'. Skipping generation."); 
                return true;
            }

            if (!File.Exists(originalTexturePath))
            {
                Console.WriteLine($"IconGenerator: ERROR - Original file not found at {originalTexturePath}");
                return false;
            }

            try
            {
                Console.WriteLine($"IconGenerator: Attempting to load original image: '{originalTexturePath}'"); 
                using (Image image = Image.Load(originalTexturePath))
                {
                    Console.WriteLine($"IconGenerator: Original image loaded. Resizing to {iconSize}x{iconSize}."); 
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new SixLabors.ImageSharp.Size(iconSize, iconSize), 
                        Mode = ResizeMode.Crop 
                    }));
                    Console.WriteLine($"IconGenerator: Resized. Attempting to save icon to '{fullIconPath}'."); 
                    image.Save(fullIconPath, new PngEncoder()); // Use fullIconPath for saving
                    Console.WriteLine($"IconGenerator: Successfully generated and saved icon: '{fullIconPath}'."); 
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"IconGenerator: ERROR generating texture icon for {originalTexturePath} (intended icon path: {fullIconPath}): {e.Message}\nStackTrace: {e.StackTrace}"); 
                return false;
            }
        }

        public static bool GenerateMeshIcon(Assimp.Scene scene, Assimp.Mesh assimpMesh, string modelFilePath, int meshContentHash, int iconSize = DefaultIconSize)
        {
            if (string.IsNullOrEmpty(meshIconPath)) Initialize();
            string iconPath = GetMeshIconPath(meshContentHash);

            if (File.Exists(iconPath)) return true;
            if (assimpMesh == null || scene == null) return false;

            OffscreenFramebuffer fbo = null;
            ShaderFile pbrIconShaderFile = null; // Changed type to ShaderFile
            int vao = -1, vbo = -1, ebo = -1;
            Texture albedoTex = null, normalTex = null, rmaTex = null;

            try
            {
                fbo = new OffscreenFramebuffer(iconSize, iconSize);

                // --- 1. Shader Setup ---
                string shaderKey = "mesh_icon_pbr"; // Base path for ShaderManager
                ShaderManager.ShaderLoadStatus status = ShaderManager.LoadShader(shaderKey, out pbrIconShaderFile);

                if (status != ShaderManager.ShaderLoadStatus.SUCCESS && status != ShaderManager.ShaderLoadStatus.LOADED_FROM_CACHE)
                {
                    Console.WriteLine($"Failed to load PBR Icon shader ({shaderKey}). Status: {status}. Cannot generate mesh icon.");
                    return false;
                }
                
                // --- 2. Mesh Data Preparation (VAO/VBO) ---
                List<float> vertexData = new List<float>();
                BoundingBox boundingBox = new BoundingBox(new Assimp.Vector3D(float.MaxValue), new Assimp.Vector3D(float.MinValue));

                for (int i = 0; i < assimpMesh.VertexCount; i++)
                {
                    var v = assimpMesh.Vertices[i];
                    vertexData.Add(v.X); vertexData.Add(v.Y); vertexData.Add(v.Z);
                    boundingBox.Min.X = Math.Min(boundingBox.Min.X, v.X);
                    boundingBox.Min.Y = Math.Min(boundingBox.Min.Y, v.Y);
                    boundingBox.Min.Z = Math.Min(boundingBox.Min.Z, v.Z);
                    boundingBox.Max.X = Math.Max(boundingBox.Max.X, v.X);
                    boundingBox.Max.Y = Math.Max(boundingBox.Max.Y, v.Y);
                    boundingBox.Max.Z = Math.Max(boundingBox.Max.Z, v.Z);

                    var n = assimpMesh.HasNormals ? assimpMesh.Normals[i] : new Vector3D(0, 1, 0);
                    vertexData.Add(n.X); vertexData.Add(n.Y); vertexData.Add(n.Z);

                    if (assimpMesh.HasTangentBasis)
                    {
                        var t = assimpMesh.Tangents[i];
                        var b = assimpMesh.BiTangents[i];
                        Vector3 tangent = new Vector3(t.X, t.Y, t.Z).Normalized();
                        Vector3 normal = new Vector3(n.X, n.Y, n.Z).Normalized();
                        Vector3 bitangent = new Vector3(b.X, b.Y, b.Z).Normalized();
                        tangent = (tangent - normal * Vector3.Dot(normal, tangent)).Normalized();
                        float tangentW = Vector3.Dot(Vector3.Cross(normal, tangent), bitangent) > 0.0f ? 1.0f : -1.0f;
                        vertexData.Add(tangent.X); vertexData.Add(tangent.Y); vertexData.Add(tangent.Z); vertexData.Add(tangentW);
                    }
                    else
                    {
                        vertexData.AddRange(new[] { 1f, 0f, 0f, 1f }); // Default tangent & sign
                    }

                    var uv = assimpMesh.HasTextureCoords(0) ? assimpMesh.TextureCoordinateChannels[0][i] : new Vector3D(0, 0, 0);
                    vertexData.Add(uv.X); vertexData.Add(uv.Y);
                }
                var indices = assimpMesh.GetIndices();

                vao = GL.GenVertexArray();
                GL.BindVertexArray(vao);
                vbo = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
                GL.BufferData(BufferTarget.ArrayBuffer, vertexData.Count * sizeof(float), vertexData.ToArray(), BufferUsageHint.StaticDraw);
                ebo = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
                GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(int), indices, BufferUsageHint.StaticDraw);

                int stride = 12 * sizeof(float);
                GL.EnableVertexAttribArray(0); GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);                      // Pos
                GL.EnableVertexAttribArray(1); GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));     // Norm
                GL.EnableVertexAttribArray(2); GL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));     // Tan4
                GL.EnableVertexAttribArray(3); GL.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, stride, 10 * sizeof(float));    // UV
                GL.BindVertexArray(0);

                // --- 3. Texture Loading ---
                string modelDir = Path.GetDirectoryName(modelFilePath);
                var material = scene.Materials[assimpMesh.MaterialIndex];

                if (material.GetMaterialTexture(TextureType.Diffuse, 0, out TextureSlot texSlotDiffuse))
                {
                    albedoTex = TextureLoader.LoadTexture(texSlotDiffuse.FilePath, false, modelDir, true);
                }
                if (material.GetMaterialTexture(TextureType.Normals, 0, out TextureSlot texSlotNormal))
                {
                    normalTex = TextureLoader.LoadTexture(texSlotNormal.FilePath, false, modelDir, true);
                }
                // For RMA, try specific PBR slots or fallback to Unknown if necessary
                // Assimp's TextureType.Unknown (or specific PBR ones if your Assimp version supports them well)
                if (material.GetMaterialTexture(TextureType.Unknown, 0, out TextureSlot texSlotRMA)) // Common for packed maps
                {
                    // You might need to check texSlotRMA.FilePath for keywords like "_rma", "_metallicRoughness", etc.
                    rmaTex = TextureLoader.LoadTexture(texSlotRMA.FilePath, false, modelDir, true);
                }
                else if (material.GetMaterialTexture(TextureType.Metalness, 0, out TextureSlot texSlotMetal)) 
                { // Less common to be separate AND what we expect for RMA packed
                    // This would be if metallic and roughness are separate, and we only got metallic here.
                    // For true RMA, we usually expect one map.
                    // Let's assume if Metalness exists, it might be part of a 2-map workflow (Metal + Rough) or a single channel metalness.
                    // For simplicity, if we find Metalness, and then Roughness separately, we can't directly make an RMA map here.
                    // The user confirmed RMA texture, so TextureType.Unknown is the best bet for finding it if not in a standard PBR slot.
                }


                // --- 4. Camera Setup ---
                Vector3 center = (new Vector3(boundingBox.Min.X, boundingBox.Min.Y, boundingBox.Min.Z) + new Vector3(boundingBox.Max.X, boundingBox.Max.Y, boundingBox.Max.Z)) / 2;
                float size = (boundingBox.Max-boundingBox.Min).Length();
                if (size < 0.001f) size = 1.0f; // Avoid issues with empty/flat meshes

                Matrix4 modelMatrix = Matrix4.CreateTranslation(-center); // Center the mesh at origin first
                
                // Simple orthographic view for icons
                float orthoSize = size * 0.75f; // Zoom out a bit to frame it
                Matrix4 projectionMatrix = Matrix4.CreateOrthographic(orthoSize * ((float)iconSize / iconSize), orthoSize, 0.01f, size * 2.0f + 10f);

                Vector3 camPos = new Vector3(0, 0, size + 1.0f); // Place camera in front, looking at origin (where mesh is now centered)
                Matrix4 viewMatrix = Matrix4.LookAt(camPos, Vector3.Zero, Vector3.UnitY);


                // --- 5. Rendering ---
                fbo.Bind();
                GL.Viewport(0, 0, iconSize, iconSize);
                GL.ClearColor(0.2f, 0.2f, 0.2f, 0.0f); // Transparent background with grey tint for visibility
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                GL.Enable(EnableCap.DepthTest);
                // GL.Enable(EnableCap.CullFace); // Optional: depends if you want to see backfaces for thin meshes

                pbrIconShaderFile.UseShader(); // Use ShaderFile's method
                pbrIconShaderFile.SetShaderMatrix4x4("model", modelMatrix, false); // Model matrix centers the mesh
                pbrIconShaderFile.SetShaderMatrix4x4("view", viewMatrix, false);
                pbrIconShaderFile.SetShaderMatrix4x4("projection", projectionMatrix, false);
                pbrIconShaderFile.SetShaderVector3("u_CameraPosWorld", camPos); // Camera position in world space (relative to centered mesh)
                // u_LightDirWorld and u_LightColor are defaults in shader

                pbrIconShaderFile.SetShaderInt("hasAlbedoMap", albedoTex != null && albedoTex.GetGLTexture() != 0 ? 1 : 0);
                if (albedoTex != null && albedoTex.GetGLTexture() != 0) { GL.ActiveTexture(TextureUnit.Texture0); GL.BindTexture(TextureTarget.Texture2D, albedoTex.GetGLTexture()); pbrIconShaderFile.SetShaderInt("albedoMap", 0); }
                
                pbrIconShaderFile.SetShaderInt("hasNormalMap", normalTex != null && normalTex.GetGLTexture() != 0 ? 1 : 0);
                if (normalTex != null && normalTex.GetGLTexture() != 0) { GL.ActiveTexture(TextureUnit.Texture1); GL.BindTexture(TextureTarget.Texture2D, normalTex.GetGLTexture()); pbrIconShaderFile.SetShaderInt("normalMap", 1); }

                pbrIconShaderFile.SetShaderInt("hasRmaMap", rmaTex != null && rmaTex.GetGLTexture() != 0 ? 1 : 0);
                if (rmaTex != null && rmaTex.GetGLTexture() != 0) { GL.ActiveTexture(TextureUnit.Texture2); GL.BindTexture(TextureTarget.Texture2D, rmaTex.GetGLTexture()); pbrIconShaderFile.SetShaderInt("rmaMap", 2); }
                
                // Defaults for material params are in shader, could set them here too if needed.

                GL.BindVertexArray(vao);
                GL.DrawElements(BeginMode.Triangles, indices.Length, DrawElementsType.UnsignedInt, 0);
                GL.BindVertexArray(0);

                fbo.Unbind();
                // Restore viewport? Application should handle main viewport after this.

                // --- 6. Save Icon & Cleanup ---
                fbo.SaveAsPng(iconPath);
                Console.WriteLine($"Generated mesh icon: {iconPath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating mesh icon for hash {meshContentHash} (from model: {modelFilePath}): {ex.Message}\n{ex.StackTrace}");
                return false;
            }
            finally
            {
                // --- 7. Cleanup ---
                fbo?.Unbind(); // Unbind before disposing, just in case
                fbo?.Dispose();
                // pbrIconShaderFile is managed by ShaderManager

                if (vao != -1) GL.DeleteVertexArray(vao);
                if (vbo != -1) GL.DeleteBuffer(vbo);
                if (ebo != -1) GL.DeleteBuffer(ebo);

                // Textures loaded via TextureLoader are managed by it (cached, ref-counted)
            }
        }
    }
} 