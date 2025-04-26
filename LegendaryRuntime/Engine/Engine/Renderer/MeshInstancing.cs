using System.Security.Cryptography;
using Assimp;
using OpenTK.Graphics.ES30;
using OpenTK.Mathematics;
namespace TheLabs.LegendaryRuntime.Engine.Renderer;

// This file has taken some effort, it has some amazing performance stuff :D

struct MeshInstance
{
    public int MeshHash;
    public Matrix4 ModelMatrix;
}
public struct GpuMesh
{
    public int Vao;
    public int IndexCount;
}

public struct ShadowCasterInstance
{
    public MeshHasher.CombinedMesh Mesh;
    public Matrix4 ModelMatrix;
    public Matrix4 LightViewProjection;
    public Vector2 AtlasOffset;
    public Vector2 AtlasScale;
    public float TileSize;
    private float padding;
}

public static class MeshInstancing
{
    
}




public static class MeshHasher
{
    
    public static void WriteBinaryQuantized(BinaryWriter writer, float value)
    {
        writer.Write((int)MathF.Floor(value * 10000.0f));
    }

    public static void WriteBinaryVector(BinaryWriter writer, Vector3 value)
    {
        WriteBinaryQuantized(writer, value.X);
        WriteBinaryQuantized(writer, value.Y);
        WriteBinaryQuantized(writer, value.Z);
    }

    public static byte[] SerializeMeshData(Mesh mesh)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);


        for (int i = 0; i < mesh.VertexCount; i++)
        {
            var v = mesh.Vertices[i];

            WriteBinaryVector(writer, new Vector3(v.X, v.Y, v.Z));

            if (mesh.HasNormals)
            {
                var n = mesh.Normals[i];
                WriteBinaryVector(writer, new Vector3(n.X, n.Y, n.Z));

            }

            if (mesh.HasTextureCoords(0))
            {
                var uv = mesh.TextureCoordinateChannels[0][i];

                WriteBinaryQuantized(writer, uv.X);
                WriteBinaryQuantized(writer, uv.Y);
                
            }

            if (mesh.HasTangentBasis)
            {
                var tangent = mesh.Tangents[i];
                WriteBinaryVector(writer, new Vector3(tangent.X, tangent.Y, tangent.Z));

                
                var bitangent = mesh.BiTangents[i];
                WriteBinaryVector(writer, new Vector3(bitangent.X, bitangent.Y, bitangent.Z));

            }
            
        }

        foreach (var face in mesh.Faces)
        {
            foreach (var index in face.Indices)
            {
                writer.Write(index);
            }
        }
        
        writer.Flush();

        return stream.ToArray();
    }

    public static int HashMesh(Mesh mesh)
    {
        byte[] data = SerializeMeshData(mesh);

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);

        return BitConverter.ToInt32(hash, 0);
    }

    public struct CombinedMesh
    {
        public GpuMesh RenderMesh;
        public int numVertices;
        public GpuMesh ShadowMesh;
        public int numShadowVertices;
    }
    
    private static Dictionary<int, CombinedMesh> MeshHashMap = new Dictionary<int, CombinedMesh>();

    private static int numMeshesHashed;
    private static int numMeshesInstanced;
    
    public static void ResetStats()
    {
        numMeshesHashed = 0;
        numMeshesInstanced = 0;
    }
    
    public static CombinedMesh AddOrGetMesh(Mesh mesh)
    {
        int hash = HashMesh(mesh);
        
        if (!MeshHashMap.TryGetValue(hash, out CombinedMesh hashedMesh))
        {
            MeshHashMap.Add(hash, new CombinedMesh
                { 
                    RenderMesh = UploadMeshToGPU(mesh), 
                    ShadowMesh = BuildShadowMesh(mesh), 
                });
            numMeshesHashed++;

            return MeshHashMap[hash];
        }
        numMeshesInstanced++;
        
        return hashedMesh;
    }

    public static void PrintStats()
    {
        Console.WriteLine($"Meshes Hashed: {numMeshesHashed} Meshes Instanced {numMeshesInstanced}.");
    }
    public static GpuMesh BuildShadowMesh(Mesh mesh)
    {
        // Create a VAO for the shadow mesh.
        int vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        // Create a VBO for vertex positions only.
        int vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
    
        // Create an array with 3 floats per vertex.
        int numElements = 3;
        if (mesh.HasTextureCoords(0))
        {
            numElements += 2;
        }
        float[] vertices = new float[mesh.VertexCount * numElements];
        for (int i = 0; i < mesh.VertexCount; i++)
        {
            vertices[i * numElements + 0] = mesh.Vertices[i].X;
            vertices[i * numElements + 1] = mesh.Vertices[i].Y;
            vertices[i * numElements + 2] = mesh.Vertices[i].Z;
            if (mesh.HasTextureCoords(0))
            {
                vertices[i * 5 + 3] = mesh.TextureCoordinateChannels[0][i].X;
                vertices[i * 5 + 4] = mesh.TextureCoordinateChannels[0][i].Y;
            }

        }
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

        // Set up the position attribute (location 0).
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, numElements * sizeof(float), 0);

        if (mesh.HasTextureCoords(0))
        {
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, numElements * sizeof(float), 3 * sizeof(float));
        }
        // Create an element buffer (EBO) using the mesh's indices.
        int ebo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        List<int> indices = new List<int>();
        foreach (var face in mesh.Faces)
        {
            indices.AddRange(face.Indices);
        }
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(int), indices.ToArray(), BufferUsageHint.StaticDraw);

        // Unbind the VAO (the EBO binding is stored in the VAO).
        GL.BindVertexArray(0);

        return new GpuMesh { Vao = vao, IndexCount = indices.Count };
    }
    
    private static GpuMesh UploadMeshToGPU(Mesh mesh)
    {
        int vao = GL.GenVertexArray();

        GL.BindVertexArray(vao);

        int vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

        float[] vertices = new float[mesh.VertexCount * 12];

        for (int i = 0; i < mesh.VertexCount; i++)
        {
            vertices[i * 12 + 0] = mesh.Vertices[i].X;
            vertices[i * 12 + 1] = mesh.Vertices[i].Y;
            vertices[i * 12 + 2] = mesh.Vertices[i].Z;

            if (mesh.HasNormals)
            {
                vertices[i * 12 + 3] = mesh.Normals[i].X;
                vertices[i * 12 + 4] = mesh.Normals[i].Y;
                vertices[i * 12 + 5] = mesh.Normals[i].Z;
            }
            else
            {
                vertices[i * 12 + 3] = 0;
                vertices[i * 12 + 4] = 0;
                vertices[i * 12 + 5] = 1;
            }

            if (mesh.HasTangentBasis)
            {
                Vector3 normal = new Vector3(mesh.Normals[i].X, mesh.Normals[i].Y, mesh.Normals[i].Z).Normalized();
                Vector3 tangent = new Vector3(mesh.Tangents[i].X, mesh.Tangents[i].Y, mesh.Tangents[i].Z).Normalized();
                Vector3 bitangent = new Vector3(mesh.BiTangents[i].X, mesh.BiTangents[i].Y, mesh.BiTangents[i].Z).Normalized();

                // orthonormalize

                tangent = (tangent - normal * Vector3.Dot(normal, tangent)).Normalized();

                vertices[i * 12 + 6] = tangent.X;
                vertices[i * 12 + 7] = tangent.Y;
                vertices[i * 12 + 8] = tangent.Z;

                // tangent sign to optimise vertex size :)
                vertices[i * 12 + 9] = Vector3.Dot(Vector3.Cross(tangent, normal), bitangent) < 0.0f ? -1.0f : 1.0f;
            }
            else
            {
                vertices[i * 12 + 6] = 0;
                vertices[i * 12 + 7] = 0;
                vertices[i * 12 + 8] = 0;
                // force normalmapping off here :)
                vertices[i * 12 + 9] = 10;
            }

            if (mesh.HasTextureCoords(0))
            {
                var uv = mesh.TextureCoordinateChannels[0][i];
                vertices[i * 12 + 10] = uv.X;
                vertices[i * 12 + 11] = uv.Y;
            }
            else
            {
                vertices[i * 12 + 10] = 0;
                vertices[i * 12 + 11] = 0;
            }
        }

        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12 * sizeof(float), 0 * sizeof(float));
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 12 * sizeof(float), 3 * sizeof(float));
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, 12 * sizeof(float), 6 * sizeof(float));
        GL.EnableVertexAttribArray(3);
        GL.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, 12 * sizeof(float), 10 * sizeof(float));

        int ebo = GL.GenBuffer();
        
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        List<int> indices = new List<int>();

        foreach (var face in mesh.Faces)
        {
            indices.AddRange(face.Indices);
        }
        
        int[] indicesArray = indices.ToArray();

        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(int), indicesArray, BufferUsageHint.StaticDraw);

        GL.BindVertexArray(0);

        return new GpuMesh { Vao = vao, IndexCount = indices.Count };
    }
    
    
    
}

