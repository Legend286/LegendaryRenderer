using LegendaryRenderer.Engine.Shaders;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace LegendaryRenderer.Engine.Geometry;

public class Mesh : IDisposable
{
    private ShaderFile shader;
    private List<Triangle> Triangles;
    private int VertexBufferObject;
    private int VertexArrayObject;
    private int ElementBufferObject;
    
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
        List<Triangle> tris = new List<Triangle>();
        tris.Add(Geometry.Triangle.Single());
        return new Mesh(tris);
    }

    public int Initialize()
    {
        List<Vertex> tris = new List<Vertex>();

        foreach (Triangle triangle in Triangles)
        {
            tris.Add(triangle.First);
            tris.Add(triangle.Second);
            tris.Add(triangle.Third);
        }

       
        var loaded = ShaderManager.LoadShader("basepass", out ShaderFile loadedShader);

        shader = loadedShader;
        
        CreateBufferData(tris);

        
        return VertexBufferObject;
    }

    public void CreateBufferData(List<Vertex> tris)
    {
        VertexBufferObject = GL.GenBuffer();
        VertexArrayObject = GL.GenVertexArray();
        ElementBufferObject = GL.GenBuffer();
        
        GL.BindVertexArray(VertexArrayObject);

        
        float[] vertices = {
            0.5f,  0.5f, 0.0f,  // top right
            0.5f, -0.5f, 0.0f,  // bottom right
            -0.5f, -0.5f, 0.0f,  // bottom left
            -0.5f,  0.5f, 0.0f   // top left
        };


        int[] indices =
        {
            0, 1, 3,
            1, 2, 3,
        };
        

        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsage.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ElementBufferObject);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsage.StaticDraw);
        
        GL.VertexAttribPointer(shader.GetAttributeLocation("aPosition"), 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);
    }

    public void BindBuffer()
    {
        GL.BindVertexArray(VertexArrayObject);
    }

    public void Draw()
    {
        shader.UseShader();
        BindBuffer();
        GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
    }

    public void Dispose()
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.DeleteBuffer(VertexBufferObject);
    }
}