using Geometry;
using OpenTK.Mathematics;


namespace LegendaryRenderer.FileLoaders;

public class ObjLoader
{
    private Vector3[] vertices;
    private Vector3[] normals;
    private Vector2[] textureCoordinates;

    private List<Tuple<int, int, int>> faces = new List<Tuple<int, int, int>>();
    
    public int VertexCount
    {
        get { return vertices.Length; }
    }

    public int IndexCount
    {
        get { return faces.Count * 3; }
    }

    public int NormalsCount
    {
        get { return normals.Length; }
    }

    public Vector3[] GetVertices()
    {
        return vertices;
    }

    public int[] GetIndices(int offset = 0)
    {
        List<int> temp = new List<int>();

        foreach (var face in faces)
        {
            temp.Add(face.Item1 + offset);
            temp.Add(face.Item2 + offset);
            temp.Add(face.Item3 + offset);
        }

        return temp.ToArray();
    }

    public Vector3[] GetNormals()
    {
        return normals;
    }

    public Vector2[] GetTextureCoordinates()
    {
        return textureCoordinates;
    }

    public static bool LoadFromFile(string fileName, out Mesh loadedMesh)
    {
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

    public static Mesh LoadFromString(string meshData, string fileName)
    {
        List<String> lines = new List<string>(meshData.Split('\n'));

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();

        List<Tuple<int, int, int>> faces = new List<Tuple<int, int, int>>();

        foreach (String line in lines)
        {
            if (line.StartsWith("v "))
            {
                String temp = line.Substring(2);

                Vector3 vec = new Vector3();

                if (temp.Count((char c) => c == ' ') == 2)
                {
                    String[] vertParts = temp.Split(' ');

                    bool success = float.TryParse(vertParts[0], out vec.X);
                    success &= float.TryParse(vertParts[1], out vec.Y);
                    success &= float.TryParse(vertParts[2], out vec.Z);

                    norms.Add(new Vector3((float)Math.Sin(vec.Z), (float)Math.Sin(vec.Z), (float)Math.Sin(vec.Z)));
                    uvs.Add(new Vector2((float)Math.Sin(vec.Z), (float)Math.Sin(vec.Z)));

                    if (!success)
                    {
                        Console.WriteLine($"Error parsing vertex: {line}");
                    }

                    verts.Add(vec);
                }
            }
            else if (line.StartsWith("f "))
            {
                String temp = line.Substring(2);

                Tuple<int, int, int> face = new Tuple<int, int, int>(0, 0, 0);
                
                if(temp.Count((char c) => c == ' ') == 2)
                {
                    String[] faceParts = temp.Split(' ');

                    int i1, i2, i3;

                    bool success = int.TryParse(faceParts[0], out i1);
                    success &= int.TryParse(faceParts[1], out i2);
                    success &= int.TryParse(faceParts[2], out i3);

                    if (!success)
                    {
                        Console.WriteLine($"Error parsing face {line}).");
                    }
                    else
                    {
                        face = new Tuple<int, int, int>(i1 - 1, i2 - 1, i3 - 1);
                        faces.Add(face);
                    }
                }
            }
        }

        Mesh mesh = new Mesh(fileName);

        uint[] ind = new uint[faces.Count * 3];
        float[] ver = new float[verts.Count * 8];

        for(int i = 0; i < faces.Count; i++)
        {
            ind[i * 3] = (uint)faces[i].Item1;
            ind[i * 3 + 1] = (uint)faces[i].Item2;
            ind[i * 3 + 2] = (uint)faces[i].Item3;
        }

        for (int vertex = 0; vertex < verts.Count; vertex++)
        {
            ver[vertex * 8] = verts[vertex].X;
            ver[vertex * 8 + 1] = verts[vertex].Y;
            ver[vertex * 8 + 2] = verts[vertex].Z;
            ver[vertex * 8 + 3] = norms[vertex].X;
            ver[vertex * 8 + 4] = norms[vertex].Y;
            ver[vertex * 8 + 5] = norms[vertex].Z;
            ver[vertex * 8 + 6] = uvs[vertex].X;
            ver[vertex * 8 + 7] = uvs[vertex].Y;

        }

        mesh.SetVerticesAndIndices(ver, ind);
        return mesh;
    }
    
    
}