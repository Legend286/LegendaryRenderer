using Assimp;
using LegendaryRenderer.LegendaryRuntime.Application.Profiling;
using LegendaryRenderer.LegendaryRuntime.Application.ProgressReporting;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MeshInstancing;
using LegendaryRenderer.LegendaryRuntime.Engine.Renderer.MaterialSystem;
using OpenTK.Mathematics;
using Quaternion = OpenTK.Mathematics.Quaternion;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.ModelLoader;

public static class ModelLoader
{
    static float[][] SceneVertexBuffers = new float[1000][];
    static uint[][] SceneIndexBuffers = new uint[1000][];
    private static ConsoleProgressBar LoadingProgressBar;

    private static int Loaded = 0;
    public static GameObject LoadModel(string fileName, Vector3 position, Quaternion rotation, Vector3 scale, bool purePath = false)
    {
        progress = 0;
        if (LoadingProgressBar != null)
        {
            LoadingProgressBar.Dispose();
        }
        
        // Create a new progress bar for each model load
       
        
        GameObject rootNode = new GameObject(position, $"Loaded Model {fileName}");
        rootNode.Transform.Rotation = rotation;

        using (new ScopedProfiler($"Load Model '{fileName}' ({Loaded++})."))
        {
            AssimpContext importer = new AssimpContext();
            // Get the absolute base directory of the executable.
            string basePath = AppContext.BaseDirectory;
            // Build your shader folder path using Path.Combine for proper platform independence.

            string fullFile;
            if (!purePath)
            {
                fullFile = Path.Combine(Path.Combine(Path.Combine(basePath, "LegendaryRuntime"), "Resources"), fileName);
            }
            else
            {
                fullFile = fileName;
            }

            Assimp.Scene scene = importer.ImportFile(fullFile,
                PostProcessSteps.CalculateTangentSpace | PostProcessSteps.Triangulate |
                PostProcessSteps.GenerateBoundingBoxes | PostProcessSteps.GenerateSmoothNormals);

            Console.WriteLine($"Scene '{fileName}' has {scene.RootNode.ChildCount} nodes.");
            Console.WriteLine($"Scene is requesting to add {scene.MeshCount} meshes to the Scene.");

            rootNode.Transform.Rotation = rotation;
            rootNode.Transform.Scale = DeriveScale(Matrix3FromMatrix4(scene.RootNode.Transform)) * scale;

            int numMaterials = scene.MaterialCount;

            using (LoadingProgressBar = new ConsoleProgressBar())
            {
                AddMeshData(rootNode, scene, scene.RootNode, scene.RootNode.Transform, fileName);
            }
        }
        return rootNode;
    }

    static int mshID = 0;

    static SphereBounds ComputeMeshBounds(Mesh mesh, Matrix4x4 transform)
    {
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            
        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            var x = mesh.Vertices[i].X;
            var y = mesh.Vertices[i].Y;
            var z = mesh.Vertices[i].Z;
                
            Vector3 vertex = new Vector3(x, y, z);

            max = Vector3.ComponentMax(vertex, max);
            min = Vector3.ComponentMin(vertex, min);
        }

        Vector3 centre = (min + max) / 2;
        SphereBounds output = new SphereBounds(centre, DeriveScale(Matrix3FromMatrix4(transform, false)).Length * MathF.Sqrt(((max - centre).Length * (max - centre).Length)));

        return output;
    }

    private static int progress = 0;

    static void AddMeshData(GameObject rootNode, Assimp.Scene scene, Node node, Matrix4x4 parentTransform, string fileName)
    {
         // Combine the parent's transform with the node's local transform.
        Matrix4x4 currentTransform = parentTransform * node.Transform;
        var obj = 0;
        // For each mesh attached to this node...
        for (int i = 0; i < node.MeshCount; i++)
        {
            progress++;
            // Update the progress bar
            LoadingProgressBar.Report((float)progress / scene.MeshCount, $"Processing node {node.Name} ({progress} of {scene.MeshCount})...");

            Mesh mesh = scene.Meshes[node.MeshIndices[i]];

            MeshHasher.CombinedMesh gpuMesh = MeshHasher.AddOrGetMesh(mesh);
            
            RenderableMesh msh = new RenderableMesh($"{fileName}:{mesh.Name}",obj++);
            msh.mesh = gpuMesh;
            
            var mats = scene.Materials[mesh.MaterialIndex].GetMaterialTextures(TextureType.BaseColor);

            foreach (var material in mats)
            {
            //    Console.WriteLine($"Material Textures {material.FilePath}");
            }

           // Console.WriteLine($"Computing Bounds for {msh.Name}...");
            SphereBounds b = ComputeMeshBounds(mesh, node.Transform);
           // Console.WriteLine($"Bounds for {msh.Name}: {b.Centre} {b.Radius}.");
            // Set mesh data (VAO info, index count, etc.)
            msh.SetMeshData(gpuMesh.ShadowMesh.Vao, gpuMesh.RenderMesh.Vao, true, mesh.GetIndices().Length, mesh.VertexCount, b);

            // Add this mesh as a child of the current root node.
            rootNode.AddChild(msh);
            rootNode = msh;
            
            // Use the combined transform for this mesh.
            // The combined transform is the world transform from the hierarchy.
            msh.Transform.Rotation = Quaternion.FromMatrix(Matrix3FromMatrix4(currentTransform));
            msh.Transform.Position = (new Vector4(0,0,0,1) * FromMatrix(currentTransform)).Xyz;
            msh.Transform.Scale = DeriveScale(Matrix3FromMatrix4(currentTransform, false));

            if (scene.HasMaterials)
            {
                msh.Material.Colour = FromColor4D(scene.Materials[mesh.MaterialIndex].ColorDiffuse);
                msh.Material.Roughness = 1.0f - scene.Materials[mesh.MaterialIndex].Shininess;

                if (scene.Materials[mesh.MaterialIndex].IsPBRMaterial)
                {
                    if (scene.Materials[mesh.MaterialIndex].GetMaterialTexture(TextureType.BaseColor, 0, out TextureSlot diff))
                    {
                        string modelDirectory = Path.GetDirectoryName(fileName);
                        int diffuseTex = TextureLoader.LoadTexture(diff.FilePath, false, modelDirectory, true).GetGLTexture();
                        msh.Material.DiffuseTexture = diffuseTex;
                    }
                    else
                    {
                  //      Console.WriteLine($"Model {mesh.Name} has no BaseColor texture.");
                    }
                }
                else
                {
                    if (scene.Materials[mesh.MaterialIndex].GetMaterialTexture(TextureType.Diffuse, 0, out TextureSlot diff))
                    {
                        string modelDirectory = Path.GetDirectoryName(fileName);
                        int diffuseTex = TextureLoader.LoadTexture(diff.FilePath, false, modelDirectory, true).GetGLTexture();
                     //   Console.WriteLine(diff.FilePath);
                        msh.Material.DiffuseTexture = diffuseTex;
                    }
                    else
                    {
                        if (scene.Materials[mesh.MaterialIndex].HasTextureDiffuse)
                        {
                            string modelDirectory = Path.GetDirectoryName(fileName);
                            int diffuseTex = TextureLoader.LoadTexture(scene.Materials[mesh.MaterialIndex].TextureDiffuse.FilePath, false, modelDirectory, true).GetGLTexture();
                            msh.Material.DiffuseTexture = diffuseTex;
                        }
                        else
                        {
                    //        Console.WriteLine($"Model {mesh.Name} has no Diffuse texture.");
                        }
                    }
                }
                if (scene.Materials[mesh.MaterialIndex].GetMaterialTexture(TextureType.NormalCamera, 0, out TextureSlot norm))
                {
                    string modelDirectory = Path.GetDirectoryName(fileName);
                    int normalTex = TextureLoader.LoadTexture(norm.FilePath, false, modelDirectory, true).GetGLTexture();
                    msh.Material.NormalTexture = normalTex;
                }
                else
                { 
                    if (scene.Materials[mesh.MaterialIndex].HasTextureNormal)
                    {
                        string modelDirectory = Path.GetDirectoryName(fileName);
                        int normalTex = TextureLoader.LoadTexture(scene.Materials[mesh.MaterialIndex].TextureNormal.FilePath, false, modelDirectory, true).GetGLTexture();
                        msh.Material.NormalTexture = normalTex;
                    }
                    else
                    {
                  //      Console.WriteLine($"Model {mesh.Name} has no Normal texture.");
                    }
                }

                if (scene.Materials[mesh.MaterialIndex]
                    .GetMaterialTexture(TextureType.Roughness, 0, out TextureSlot rough))
                {
                    string modelDirectory = Path.GetDirectoryName(fileName);
                    int roughnessTex = TextureLoader.LoadTexture(rough.FilePath, false, modelDirectory, true).GetGLTexture();
                    msh.Material.RoughnessTexture = roughnessTex;
                }
                else
                {
                    if (scene.Materials[mesh.MaterialIndex].HasTextureSpecular)
                    {
                        string modelDirectory = Path.GetDirectoryName(fileName);
                        int roughnessTex = TextureLoader.LoadTexture(scene.Materials[mesh.MaterialIndex].TextureSpecular.FilePath, false, modelDirectory, true).GetGLTexture();
                        msh.Material.RoughnessTexture = roughnessTex;
                    }
                    else
                    {
                  //      Console.WriteLine($"Model {mesh.Name} has no Roughness texture.");
                    }
                }
            }

         //   Console.WriteLine("Node Transform: " + FromMatrix(node.Transform));
         //   Console.WriteLine("Combined Transform: " + FromMatrix(currentTransform));
        }
        
        // Recurse into child nodes using the accumulated current transform.
        foreach (Node child in node.Children)
        {
            AddMeshData(rootNode, scene, child, currentTransform, fileName);
        }
    }

    static Color4 FromColor4D(Color4D color)
    {
        return new Color4(color.R, color.G, color.B, color.A);
    }

    public static Vector3 DeriveScale(Matrix3 matrix)
    {

// Extract scale by calculating the magnitude of each row
        float scaleX = new Vector3(matrix.M11, matrix.M12, matrix.M13).Length;
        float scaleY = new Vector3(matrix.M21, matrix.M22, matrix.M23).Length;
        float scaleZ = new Vector3(matrix.M31, matrix.M32, matrix.M33).Length;

        return new Vector3(scaleX, scaleY, scaleZ);
    }


    private static Matrix4 FromMatrix(Matrix4x4 mat)
    {
        Matrix4 m = new Matrix4();
        m.M11 = mat.A1;
        m.M12 = mat.A2;
        m.M13 = mat.A3;
        m.M14 = mat.A4;
        m.M21 = mat.B1;
        m.M22 = mat.B2;
        m.M23 = mat.B3;
        m.M24 = mat.B4;
        m.M31 = mat.C1;
        m.M32 = mat.C2;
        m.M33 = mat.C3;
        m.M34 = mat.C4;
        m.M41 = mat.D1;
        m.M42 = mat.D2;
        m.M43 = mat.D3;
        m.M44 = mat.D4;
        m.Transpose();
        return m;
    }

    public static Matrix3 Matrix3FromMatrix4(Matrix4x4 assimpMatrix, bool normalize = true)
    {

// Extract the top-left 3x3 rotation + scaling part
        Vector3 row1 = new Vector3(assimpMatrix.A1, assimpMatrix.A2, assimpMatrix.A3);
        Vector3 row2 = new Vector3(assimpMatrix.B1, assimpMatrix.B2, assimpMatrix.B3);
        Vector3 row3 = new Vector3(assimpMatrix.C1, assimpMatrix.C2, assimpMatrix.C3);

        if (normalize)
        {
// Remove scaling by normalizing each row
            row1 = row1.Normalized();
            row2 = row2.Normalized();
            row3 = row3.Normalized();
        }

// Construct the pure rotation matrix
        Matrix3 rotationMatrix = new Matrix3(
            row1.X, row1.Y, row1.Z,
            row2.X, row2.Y, row2.Z,
            row3.X, row3.Y, row3.Z
        );
        return rotationMatrix;
    }

    private static Vector3 FromVector(Vector3D vec)
    {
        Vector3 v;
        v.X = vec.X;
        v.Y = vec.Y;
        v.Z = vec.Z;
        return v;
    }

    private static Color4 FromColor(Color4D color)
    {
        Color4 c;
        c.R = color.R;
        c.G = color.G;
        c.B = color.B;
        c.A = color.A;
        return c;
    }
}


