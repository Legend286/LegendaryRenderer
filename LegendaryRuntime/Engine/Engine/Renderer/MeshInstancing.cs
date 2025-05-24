using System.Security.Cryptography;
using Assimp;
using OpenTK.Graphics.ES30;
using OpenTK.Mathematics;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MeshInstancing;

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
    // Helper to read quantized float
    public static float ReadBinaryQuantizedFloat(BinaryReader reader)
    {
        return reader.ReadInt32() / 10000.0f;
    }

    // Helper to read Vector3 from quantized floats
    public static Vector3 ReadBinaryVector3(BinaryReader reader)
    {
        float x = ReadBinaryQuantizedFloat(reader);
        float y = ReadBinaryQuantizedFloat(reader);
        float z = ReadBinaryQuantizedFloat(reader);
        return new Vector3(x, y, z);
    }
    
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

        // Header
        writer.Write(mesh.VertexCount);
        var indices = mesh.GetIndices();
        writer.Write(indices.Length);
        bool hasNormals = mesh.HasNormals;
        bool hasUVs = mesh.HasTextureCoords(0);
        bool hasTangents = mesh.HasTangentBasis;
        writer.Write(hasNormals);
        writer.Write(hasUVs);
        writer.Write(hasTangents);

        // Vertex Data
        for (int i = 0; i < mesh.VertexCount; i++)
        {
            var v = mesh.Vertices[i];
            WriteBinaryVector(writer, new Vector3(v.X, v.Y, v.Z));

            if (hasNormals)
            {
                var n = mesh.Normals[i];
                WriteBinaryVector(writer, new Vector3(n.X, n.Y, n.Z));
            }

            if (hasTangents)
            {
                Vector3 normal = new Vector3(mesh.Normals[i].X, mesh.Normals[i].Y, mesh.Normals[i].Z).Normalized();
                Vector3 tangent = new Vector3(mesh.Tangents[i].X, mesh.Tangents[i].Y, mesh.Tangents[i].Z).Normalized();
                Vector3 bitangent = new Vector3(mesh.BiTangents[i].X, mesh.BiTangents[i].Y, mesh.BiTangents[i].Z).Normalized();
                
                tangent = (tangent - normal * Vector3.Dot(normal, tangent)).Normalized();
                
                float tangentW = Vector3.Dot(Vector3.Cross(tangent, normal), bitangent) < 0.0f ? -1.0f : 1.0f;
                
                WriteBinaryVector(writer, tangent);
                WriteBinaryQuantized(writer, tangentW);
            }

            if (hasUVs)
            {
                var uv = mesh.TextureCoordinateChannels[0][i];
                WriteBinaryQuantized(writer, uv.X);
                WriteBinaryQuantized(writer, uv.Y);
            }
        }

        // Index Data
        foreach (var index in indices) // Use the pre-fetched indices
        {
            writer.Write(index);
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
        public int VertexCount;
        public GpuMesh ShadowMesh;
        public int numShadowVertices;
        public SphereBounds LocalBounds;
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
            // Original path
            hashedMesh = new CombinedMesh
            { 
                RenderMesh = UploadMeshToGPU(mesh), 
                ShadowMesh = BuildShadowMesh(mesh),
                // numVertices and numShadowVertices could be set here if needed by CombinedMesh directly
            };
            MeshHashMap.Add(hash, hashedMesh);
            numMeshesHashed++;
            return hashedMesh;
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

        // New standardized layout: Pos (3), Norm (3), Tangent (4), UV (2) -> 12 floats total
        List<float> vertexList = new List<float>();

        for (int i = 0; i < mesh.VertexCount; i++)
        {
            // Position
            vertexList.Add(mesh.Vertices[i].X);
            vertexList.Add(mesh.Vertices[i].Y);
            vertexList.Add(mesh.Vertices[i].Z);

            // Normal
            if (mesh.HasNormals)
            {
                vertexList.Add(mesh.Normals[i].X);
                vertexList.Add(mesh.Normals[i].Y);
                vertexList.Add(mesh.Normals[i].Z);
            }
            else
            {
                vertexList.AddRange(new[] { 0f, 0f, 1f }); // Default normal
            }

            // Tangent (XYZ) and Tangent Sign (W)
            if (mesh.HasTangentBasis && mesh.HasNormals) // Need normals to calculate tangent sign accurately
            {
                Vector3 normal = new Vector3(mesh.Normals[i].X, mesh.Normals[i].Y, mesh.Normals[i].Z).Normalized();
                Vector3 tangent = new Vector3(mesh.Tangents[i].X, mesh.Tangents[i].Y, mesh.Tangents[i].Z).Normalized();
                Vector3 bitangent = new Vector3(mesh.BiTangents[i].X, mesh.BiTangents[i].Y, mesh.BiTangents[i].Z).Normalized();

                // Orthogonalize tangent
                tangent = (tangent - normal * Vector3.Dot(normal, tangent)).Normalized();
                
                float tangentW = Vector3.Dot(Vector3.Cross(normal, tangent), bitangent) > 0.0f ? 1.0f : -1.0f; // Corrected cross product order for sign

                vertexList.Add(tangent.X);
                vertexList.Add(tangent.Y);
                vertexList.Add(tangent.Z);
                vertexList.Add(tangentW);
            }
            else
            {
                // Default Tangent (X,Y,Z) and Sign (W)
                vertexList.AddRange(new[] { 1f, 0f, 0f, 1f }); 
            }
            
            // UV Coordinates
            if (mesh.HasTextureCoords(0))
            {
                var uv = mesh.TextureCoordinateChannels[0][i];
                vertexList.Add(uv.X);
                vertexList.Add(uv.Y);
            }
            else
            {
                vertexList.AddRange(new[] { 0f, 0f }); // Default UVs
            }
        }
        float[] verticesArray = vertexList.ToArray();
        GL.BufferData(BufferTarget.ArrayBuffer, verticesArray.Length * sizeof(float), verticesArray, BufferUsageHint.StaticDraw);

        int stride = (3 + 3 + 4 + 2) * sizeof(float); // 12 floats: Pos(3), Norm(3), Tan(4), UV(2)

        // Position attribute (location 0)
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        
        // Normal attribute (location 1)
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        
        // Tangent attribute (location 2) - Vec4
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, (3 + 3) * sizeof(float));
        
        // UV attribute (location 3)
        GL.EnableVertexAttribArray(3);
        GL.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, stride, (3 + 3 + 4) * sizeof(float));
        
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
    
    public static CombinedMesh GetOrAddMeshFromBinary(int precomputedMeshHash, byte[] serializedMeshData)
    {
        if (MeshHashMap.TryGetValue(precomputedMeshHash, out CombinedMesh existingMesh))
        {
            numMeshesInstanced++; // Count as instanced if loaded from cache and found in memory
            return existingMesh;
        }

        // Deserialize and upload
        using var stream = new MemoryStream(serializedMeshData);
        using var reader = new BinaryReader(stream);

        // Header
        int vertexCount = reader.ReadInt32();
        int indexCount = reader.ReadInt32();
        bool hasNormals = reader.ReadBoolean();
        bool hasUVs = reader.ReadBoolean();
        bool hasTangents = reader.ReadBoolean();

        // Temporary lists to hold data
        List<Vector3> vertices = new List<Vector3>(vertexCount);
        List<Vector3> normals = hasNormals ? new List<Vector3>(vertexCount) : null;
        List<OpenTK.Mathematics.Vector2> uvs = hasUVs ? new List<OpenTK.Mathematics.Vector2>(vertexCount) : null; // Changed to OpenTK.Mathematics.Vector2
        List<Vector3> tangents = hasTangents ? new List<Vector3>(vertexCount) : null;
        List<float> tangentSigns = hasTangents ? new List<float>(vertexCount) : null;
        
        List<float> rawShadowVertices = new List<float>(); // For BuildShadowMeshFromData


        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 pos = ReadBinaryVector3(reader);
            vertices.Add(pos);
            rawShadowVertices.AddRange(new[] { pos.X, pos.Y, pos.Z });


            if (hasNormals)
            {
                Vector3 norm = ReadBinaryVector3(reader);
                normals.Add(norm);
            }

            if (hasTangents) // Tangent (vec3) and Tangent Sign (float W)
            {
                Vector3 tan = ReadBinaryVector3(reader);
                tangents.Add(tan);
                float tangentW = ReadBinaryQuantizedFloat(reader);
                tangentSigns.Add(tangentW);
            }

            if (hasUVs)
            {
                float u = ReadBinaryQuantizedFloat(reader);
                float v = ReadBinaryQuantizedFloat(reader);
                uvs.Add(new OpenTK.Mathematics.Vector2(u,v)); // Changed to OpenTK.Mathematics.Vector2
                rawShadowVertices.AddRange(new[] { u, v }); // Shadow mesh also uses UVs if available
            }
        }
        
        List<int> indicesList = new List<int>(indexCount);
        for (int i = 0; i < indexCount; i++)
        {
            indicesList.Add(reader.ReadInt32());
        }

        // Create GpuMeshes using new private methods that take raw data
        GpuMesh renderMesh = _UploadRenderMeshFromRawData(vertexCount, vertices, normals, uvs, tangents, tangentSigns, indicesList, hasNormals, hasUVs, hasTangents);
        GpuMesh shadowMesh = _BuildShadowMeshFromRawData(vertexCount, vertices, uvs, indicesList, hasUVs);


        CombinedMesh newCombinedMesh = new CombinedMesh 
        { 
            RenderMesh = renderMesh, 
            ShadowMesh = shadowMesh,
            // numVertices = vertexCount, // if needed
        };
        
        MeshHashMap.Add(precomputedMeshHash, newCombinedMesh);
        numMeshesHashed++; // Count as a "new" mesh processed from binary
        return newCombinedMesh;
    }

    // New private method for render mesh from raw data
    private static GpuMesh _UploadRenderMeshFromRawData(int vertexCount, List<Vector3> poss, List<Vector3> norms, List<OpenTK.Mathematics.Vector2> texCoards, List<Vector3> tans, List<float> tangentSigns, List<int> indices, bool hasNormals, bool hasUVs, bool hasTangents)
    {
        int vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        int vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

        // Vertex layout: Pos (3), Norm (3), Tangent (4), UV (2) -> Stride 12 floats
        List<float> vboData = new List<float>();
        for (int i = 0; i < vertexCount; i++)
        {
            // Position
            vboData.Add(poss[i].X); vboData.Add(poss[i].Y); vboData.Add(poss[i].Z);

            // Normal
            if (hasNormals && norms != null) { vboData.Add(norms[i].X); vboData.Add(norms[i].Y); vboData.Add(norms[i].Z); }
            else { vboData.AddRange(new[] { 0f, 0f, 1f }); } // Default normal

            // Tangent (XYZ) and Tangent Sign (W)
            if (hasTangents && tans != null && tangentSigns != null) 
            { 
                vboData.Add(tans[i].X); vboData.Add(tans[i].Y); vboData.Add(tans[i].Z);
                vboData.Add(tangentSigns[i]);
            }
            else // Default Tangent XYZ and Sign W
            {
                 vboData.AddRange(new[] { 1f, 0f, 0f, 1.0f }); 
            }

            // UV
            if (hasUVs && texCoards != null) { vboData.Add(texCoards[i].X); vboData.Add(texCoards[i].Y); }
            else { vboData.AddRange(new[] { 0f, 0f }); } // Default UV
        }
        GL.BufferData(BufferTarget.ArrayBuffer, vboData.Count * sizeof(float), vboData.ToArray(), BufferUsageHint.StaticDraw);

        int stride = (3 + 3 + 4 + 2) * sizeof(float); // Pos(3), Norm(3), Tan(4), UV(2)
        
        // Position attribute (location 0)
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        
        // Normal attribute (location 1)
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        
        // Tangent attribute (location 2) - Vec4
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, (3 + 3) * sizeof(float));
        
        // UV attribute (location 3)
        GL.EnableVertexAttribArray(3);
        GL.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, stride, (3 + 3 + 4) * sizeof(float));
        
        int ebo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(int), indices.ToArray(), BufferUsageHint.StaticDraw);

        GL.BindVertexArray(0);
        return new GpuMesh { Vao = vao, IndexCount = indices.Count };
    }

    // New private method for shadow mesh from raw data
    private static GpuMesh _BuildShadowMeshFromRawData(int vertexCount, List<Vector3> poss, List<OpenTK.Mathematics.Vector2> uvs, List<int> indices, bool hasUVs)
    {
        int vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        int vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

        List<float> vboData = new List<float>();
        int components = 3; // Pos
        if (hasUVs) components += 2; // UV

        for (int i = 0; i < vertexCount; i++)
        {
            vboData.Add(poss[i].X); vboData.Add(poss[i].Y); vboData.Add(poss[i].Z);
            if (hasUVs)
            {
                vboData.Add(uvs[i].X); vboData.Add(uvs[i].Y);
            }
        }
        GL.BufferData(BufferTarget.ArrayBuffer, vboData.Count * sizeof(float), vboData.ToArray(), BufferUsageHint.StaticDraw);

        int stride = components * sizeof(float);
        // Position
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        // UV (if present)
        if (hasUVs)
        {
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        }
        
        int ebo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(int), indices.ToArray(), BufferUsageHint.StaticDraw);

        GL.BindVertexArray(0);
        return new GpuMesh { Vao = vao, IndexCount = indices.Count };
    }
}

