using Geometry;
using OpenTK.Graphics.ES11;
using OpenTK.Graphics.GL;
using OpenTK.Mathematics;
using SixLabors.ImageSharp.PixelFormats;
using System.IO.Enumeration;
using System.Runtime.CompilerServices;


namespace LegendaryRenderer.FileLoaders;


public struct FaceVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 TextureCoordinate;

    public FaceVertex(Vector3 position, Vector3 normal, Vector2 texCoord)
    {
        Position = position;
        Normal = normal;
        TextureCoordinate = texCoord;
    }
}

public class ObjLoader
{
    private static Vector3[] vertices;
    private static Vector3[] normals;
    private static Vector2[] textureCoordinates;

    static string file = "null";

    private static List<Tuple<FaceVertex, FaceVertex, FaceVertex>> faces = new List<Tuple<FaceVertex, FaceVertex, FaceVertex>>();
    private static bool bIncompleteNormals = false;

    public static int VertexCount
    {
        get { return vertices.Length; }
    }

    public static int IndexCount
    {
        get { return faces.Count * 3; }
    }

    public static int NormalsCount
    {
        get { return normals.Length; }
    }

    public static Vector3[] GetVertices()
    {
        List<Vector3> verts = new List<Vector3>();

        foreach(var face in faces)
        {
            verts.Add(face.Item1.Position);
            verts.Add(face.Item2.Position);
            verts.Add(face.Item3.Position);
        }

        return verts.ToArray();
    }

    public static int[] GetIndices(int offset = 0)
    {
        return Enumerable.Range(offset, IndexCount).ToArray();
    }

    public Vector3[] GetNormals()
    {
        List<Vector3> normals = new List<Vector3>();

        foreach(var face in faces)
        {
            normals.Add(face.Item1.Normal);
            normals.Add(face.Item2.Normal);
            normals.Add(face.Item3.Normal);
        }

        return normals.ToArray();
    }

    public Vector2[] GetTextureCoordinates()
    {
        List<Vector2> coords = new List<Vector2>();

        foreach(var face in faces)
        {
            coords.Add(face.Item1.TextureCoordinate);
            coords.Add(face.Item2.TextureCoordinate);
            coords.Add(face.Item3.TextureCoordinate);
        }

        return coords.ToArray();
    }

    public static bool LoadFromFile(string fileName, out Mesh loadedMesh)
    {
        file = fileName;
        try
        {
            using (StreamReader reader = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read)))
            {
                loadedMesh = LoadFromString(reader.ReadToEnd(), fileName);
                return true;
            }
        }
        catch (FileNotFoundException e)
        {
            Console.WriteLine($"MeshLoader Error: File not found '{fileName}'.");
        }
        catch (Exception e)
        {
            Console.WriteLine($"MeshLoader Error: Error Opening file '{fileName}'. \n {e.Message}");
        }

        loadedMesh = null;
        return false;
    }

    struct TempVertex
    {
        public int Position;
        public int Normal;
        public int TextureCoordinate;

        public TempVertex(int pos, int nor, int uv)
        {
            Position = pos;
            Normal = nor;
            TextureCoordinate = uv;
        }
    }
    public static Mesh LoadFromString(string meshData, string fileName)
    {
        List<String> lines = new List<string>(meshData.Split('\n'));

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();

        List<Tuple<TempVertex, TempVertex, TempVertex>> tempFaces = new List<Tuple<TempVertex, TempVertex, TempVertex>>();

        verts.Add(new Vector3());
        uvs.Add(new Vector2());
        norms.Add(new Vector3());

        int currentIndice = 0;

        foreach (String line in lines)
        {
            if (line.StartsWith("v "))
            {
                String temp = line.Substring(2);

                Vector3 vec = new Vector3();

                if (temp.Trim().Count((char c) => c == ' ') == 2)
                {
                    String[] vertParts = temp.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    bool success = float.TryParse(vertParts[0], out vec.X);
                    success |= float.TryParse(vertParts[1], out vec.Y);
                    success |= float.TryParse(vertParts[2], out vec.Z);

                    if (!success)
                    {
                        Console.WriteLine($"Error parsing vertex: {line}");
                    }
                }
                else
                {
                    Console.WriteLine($"Error parsing vertex: {line}");
                }
                verts.Add(vec);
            }
            else if(line.StartsWith("vt "))
            {
                string temp = line.Substring(2);

                Vector2 vec = new Vector2();

                if(temp.Trim().Count((char c) => c == ' ') > 0)
                {
                    String[] texCoordParts = temp.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    bool success = float.TryParse(texCoordParts[0], out vec.X);
                    success |= float.TryParse(texCoordParts[1], out vec.Y);

                    if(!success)
                    {
                        Console.WriteLine($"Error parsing vertex texture coordinates: {line}.");
                    }
                }
                else
                {
                    Console.WriteLine($"Error parsing vertex texture coordinates: {line}.");
                }

                uvs.Add(vec);
            }
            else if (line.StartsWith("vn "))
            {
                String temp = line.Substring(2);

                Vector3 vec = new Vector3();

                if(temp.Trim().Count((char c) => c == ' ') == 2)
                {
                    String[] normalParts = temp.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    bool success = float.TryParse(normalParts[0], out vec.X);
                    success |= float.TryParse(normalParts[1],out vec.Y);
                    success |= float.TryParse(normalParts[2], out vec.Z);

                    if(!success)
                    {
                        Console.WriteLine($"Error parsing vertex normal: {line}");
                    }
                }
                else
                {
                    Console.WriteLine($"Error parsing vertex normal: {line}");
                }

                norms.Add(vec);
            }
            else if (line.StartsWith("f "))
            {
                String temp = line.Substring(2);

                Tuple<TempVertex, TempVertex, TempVertex> face = new Tuple<TempVertex, TempVertex, TempVertex>(new TempVertex(), new TempVertex(), new TempVertex());

                if (temp.Count((char c) => c == ' ') == 2)
                {
                    String[] faceParts = temp.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    int v1, v2, v3;
                    int t1, t2, t3;
                    int n1, n2, n3;

                    bool success = int.TryParse(faceParts[0].Split('/')[0], out v1);
                    success |= int.TryParse(faceParts[1].Split('/')[0], out v2);
                    success |= int.TryParse(faceParts[2].Split('/')[0], out v3);

                    if (faceParts[0].Count((char c) => c == '/') >= 2)
                    {
                        success |= int.TryParse(faceParts[0].Split('/')[1], out t1);
                        success |= int.TryParse(faceParts[1].Split('/')[1], out t2);
                        success |= int.TryParse(faceParts[2].Split('/')[1], out t3);
                        success |= int.TryParse(faceParts[0].Split('/')[2], out n1);
                        success |= int.TryParse(faceParts[1].Split('/')[2], out n2);
                        success |= int.TryParse(faceParts[2].Split('/')[2], out n3);
                    }
                    else
                    {
                        if(uvs.Count > v1 && uvs.Count > v2 && uvs.Count > v3)
                        {
                            t1 = v1;
                            t2 = v2;
                            t3 = v3;
                        }
                        else
                        {
                            t1 = 0;
                            t2 = 0;
                            t3 = 0;
                        }

                        if(norms.Count > v1 && norms.Count > v2 && norms.Count > v3)
                        {
                            n1 = v1;
                            n2 = v2;
                            n3 = v3;
                        }
                        else
                        {
                            n1 = 0;
                            n2 = 0;
                            n3 = 0;
                            bIncompleteNormals = true;
                        }
                    }

                    if(!success)
                    {
                        Console.WriteLine($"Error parsing face {line}.");
                    }
                    else
                    {
                        TempVertex ov1 = new TempVertex(v1, n1, t1);
                        TempVertex ov2 = new TempVertex(v2, n2, t2);
                        TempVertex ov3 = new TempVertex(v3, n3, t3);
                        face = new Tuple<TempVertex, TempVertex, TempVertex>(ov1, ov2, ov3);

                        tempFaces.Add(face);
                    }
                }
                else
                {
                    Console.WriteLine($"Error parsing face: {line}");
                }
            }
        }

        foreach(var face in tempFaces)
        {
            FaceVertex v1 = new FaceVertex(verts[face.Item1.Position], norms[face.Item1.Normal], uvs[face.Item1.TextureCoordinate]);
            FaceVertex v2 = new FaceVertex(verts[face.Item2.Position], norms[face.Item2.Normal], uvs[face.Item2.TextureCoordinate]);
            FaceVertex v3 = new FaceVertex(verts[face.Item3.Position], norms[face.Item3.Normal], uvs[face.Item3.TextureCoordinate]);

            faces.Add(new Tuple<FaceVertex, FaceVertex, FaceVertex>(v1, v2, v3));
        }

        vertices = verts.ToArray();

        Mesh mesh = new Mesh(fileName);

        uint[] ind = new uint[faces.Count * 3];
        float[] ver = new float[faces.Count * 24];

        for(int i = 0; i < tempFaces.Count; i++)
        {
            ind[i * 3] = (uint)tempFaces[i].Item1.Position;
            ind[i * 3 + 1] = (uint)tempFaces[i].Item2.Position;
            ind[i * 3 + 2] = (uint)tempFaces[i].Item3.Position;
        }

        int faceIndex = 0;
        Tuple<FaceVertex, FaceVertex, FaceVertex>[] nvs = new Tuple<FaceVertex, FaceVertex, FaceVertex>[faces.Count];
        if (bIncompleteNormals)
        {
            CalculateNormals(out nvs);
        }

        for (int faceID = 0; faceID < faces.Count; faceID++)
        {
            // First Vertex of a face
            ver[faceID * 24] = faces[faceIndex].Item1.Position.X;
            ver[faceID * 24 + 1] = faces[faceIndex].Item1.Position.Y;
            ver[faceID * 24 + 2] = faces[faceIndex].Item1.Position.Z;
            ver[faceID * 24 + 3] = faces[faceIndex].Item1.Normal.X;
            ver[faceID * 24 + 4] = faces[faceIndex].Item1.Normal.Y;
            ver[faceID * 24 + 5] = faces[faceIndex].Item1.Normal.Z;
            if(bIncompleteNormals)
            {
                ver[faceID * 24 + 3] = nvs[faceIndex].Item1.Normal.X;
                ver[faceID * 24 + 4] = nvs[faceIndex].Item1.Normal.Y;
                ver[faceID * 24 + 5] = nvs[faceIndex].Item1.Normal.Z;
            }
            ver[faceID * 24 + 6] = faces[faceIndex].Item1.TextureCoordinate.X;
            ver[faceID * 24 + 7] = faces[faceIndex].Item1.TextureCoordinate.Y;

            // Second vertex of a face
            ver[faceID * 24 + 8] = faces[faceIndex].Item2.Position.X;
            ver[faceID * 24 + 9] = faces[faceIndex].Item2.Position.Y;
            ver[faceID * 24 + 10] = faces[faceIndex].Item2.Position.Z;
            ver[faceID * 24 + 11] = faces[faceIndex].Item2.Normal.X;
            ver[faceID * 24 + 12] = faces[faceIndex].Item2.Normal.Y;
            ver[faceID * 24 + 13] = faces[faceIndex].Item2.Normal.Z;
            if (bIncompleteNormals)
            {
                ver[faceID * 24 + 11] = nvs[faceIndex].Item2.Normal.X;
                ver[faceID * 24 + 12] = nvs[faceIndex].Item2.Normal.Y;
                ver[faceID * 24 + 13] = nvs[faceIndex].Item2.Normal.Z;
            }
            ver[faceID * 24 + 14] = faces[faceIndex].Item2.TextureCoordinate.X;
            ver[faceID * 24 + 15] = faces[faceIndex].Item2.TextureCoordinate.Y;

            // Third vertex of a face
            ver[faceID * 24 + 16] = faces[faceIndex].Item3.Position.X;
            ver[faceID * 24 + 17] = faces[faceIndex].Item3.Position.Y;
            ver[faceID * 24 + 18] = faces[faceIndex].Item3.Position.Z;
            ver[faceID * 24 + 19] = faces[faceIndex].Item3.Normal.X;
            ver[faceID * 24 + 20] = faces[faceIndex].Item3.Normal.Y;
            ver[faceID * 24 + 21] = faces[faceIndex].Item3.Normal.Z;
            if (bIncompleteNormals)
            {
                ver[faceID * 24 + 19] = nvs[faceIndex].Item3.Normal.X;
                ver[faceID * 24 + 20] = nvs[faceIndex].Item3.Normal.Y;
                ver[faceID * 24 + 21] = nvs[faceIndex].Item3.Normal.Z;
            }
            ver[faceID * 24 + 22] = faces[faceIndex].Item3.TextureCoordinate.X;
            ver[faceID * 24 + 23] = faces[faceIndex].Item3.TextureCoordinate.Y;

            faceIndex++;

        //    Console.WriteLine($"Faces {faces.Count} tempFaces {tempFaces.Count} face index {faceIndex}");
        }

        mesh.SetVerticesAndIndices(ver, ind);

        return mesh;
    }

    public static void CalculateNormals(out Tuple<FaceVertex, FaceVertex, FaceVertex>[] normals)
    {
        Vector3[] ver = GetVertices();
        List<Tuple<FaceVertex, FaceVertex, FaceVertex>> norms = new List<Tuple<FaceVertex, FaceVertex, FaceVertex>>();
        int[] inds = GetIndices();

        for (int i = 0; i < IndexCount; i+=3)
        {
            Vector3 v1 = ver[inds[i]];
            Vector3 v2 = ver[inds[i + 1]];
            Vector3 v3 = ver[inds[i + 2]];

            Vector3 nv = Vector3.Cross(v2 - v1, v3 - v1);
            nv = nv.Normalized();
            FaceVertex n = new FaceVertex(Vector3.Zero, nv, Vector2.Zero);

            norms.Add(new Tuple<FaceVertex,FaceVertex,FaceVertex>(n,n,n));

        }
       // Console.WriteLine($"Generated {NormalsCount/3} face normals for mesh {file}.");

        normals = norms.ToArray();
    }
}