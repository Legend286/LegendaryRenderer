using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace LegendaryRenderer.Engine.Geometry;

public class Mesh : IDisposable
{
    private List<Triangle> Triangles;
    private int VertexBufferObject;

    public Mesh(string fileName)
    {
        Triangles = new List<Triangle>();
        
        // temporary mesh is just a triangle...

        Triangles.Add(Geometry.Triangle.Single());
        
        // do some shit to load a mesh from a file later on
        
        // initialize buffer
        Initialize();
    }

    public Mesh(List<Triangle> triangles)
    {
        Triangles = triangles;
        Initialize();
    }

    public static Mesh Triangle()
    {
        List<Triangle> Triangles = new List<Triangle>();
        Triangles.Add(Geometry.Triangle.Single());
        return new Mesh(Triangles);
    }

    public int Initialize()
    {
        VertexBufferObject = GL.GenBuffer();

        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);

        List<Vertex> tris = new List<Vertex>();

        foreach (Triangle triangle in Triangles)
        {
            tris.Add(triangle.First);
            tris.Add(triangle.Second);
            tris.Add(triangle.Third);
        }
        GL.BufferData(BufferTarget.ArrayBuffer, tris.Count * sizeof(float), tris.ToArray(), BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        return VertexBufferObject;
    }

    public void Dispose()
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.DeleteBuffer(VertexBufferObject);
    }
}