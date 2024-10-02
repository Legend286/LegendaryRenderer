using LegendaryRenderer.EngineTypes;
using LegendaryRenderer.GameObjects;
using LegendaryRenderer.Shaders;
using NUnit.Framework.Constraints;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace LegendaryRenderer.Geometry;

public class Mesh : GameObject
{
    private ShaderFile shader;
    private List<Triangle> Triangles;
    private int VertexBufferObject;
    private int VertexArrayObject;
    private int ElementBufferObject;
    
    public AABBNode Node;

    public Transform MeshTransform;
    
    public Mesh(string fileName) : base(Vector3.Zero)
    {
       Triangles = new List<Triangle>();
        
        // temporary mesh is just a triangle...

        Triangles.Add(Geometry.Triangle.Single());
        
        // do some shit to load a mesh from a file later on
        
        // initialize buffer
        Initialize();
    }

    private bool previousFrame = true;
    public override void Update(float deltaTime)
    {
        if (previousFrame)
        {
            Transform.UpdatePreviousMatrix();
        }
        previousFrame = !previousFrame;
      
    }

    public Mesh(List<Triangle> triangles) : base(Vector3.Zero)
    {
        Triangles = triangles;
        Initialize();
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
        
        Node = new AABBNode();
        Node.AddMesh(this);
        Node.Bounds.Set(Node.Position - new Vector3(-0.5f, -0.5f, -0.5f), Node.Position + new Vector3(0.5f, 0.5f, 0.5f));
        
        Console.WriteLine($"Bounds {Node.Bounds.Min} {Node.Bounds.Max} Position: {Node.Position}");

        return VertexBufferObject;
    }

    public void CreateBufferData(List<Vertex> tris)
    {
        VertexBufferObject = GL.GenBuffer();
        VertexArrayObject = GL.GenVertexArray();
        ElementBufferObject = GL.GenBuffer();
        
        GL.BindVertexArray(VertexArrayObject);

        /*
        float[] vertices = {
            0.5f,  0.5f, 0.0f,  // top right
            0.5f, -0.5f, 0.0f,  // bottom right
            -0.5f, -0.5f, 0.0f,  // bottom left
            -0.5f,  0.5f, 0.0f   // top left
        };
        */
        float[] vertices = {
            -0.5f, -0.5f, -0.5f,  
            0.5f, -0.5f, -0.5f,  
            0.5f,  0.5f, -0.5f,  
            0.5f,  0.5f, -0.5f,  
            -0.5f,  0.5f, -0.5f, 
            -0.5f, -0.5f, -0.5f,  

            -0.5f, -0.5f,  0.5f,  
            0.5f, -0.5f,  0.5f, 
            0.5f,  0.5f,  0.5f,  
            0.5f,  0.5f,  0.5f,
            -0.5f,  0.5f,  0.5f,  
            -0.5f, -0.5f,  0.5f,  

            -0.5f,  0.5f,  0.5f, 
            -0.5f,  0.5f, -0.5f, 
            -0.5f, -0.5f, -0.5f,  
            -0.5f, -0.5f, -0.5f,  
            -0.5f, -0.5f,  0.5f, 
            -0.5f,  0.5f,  0.5f,  

            0.5f,  0.5f,  0.5f,  
            0.5f,  0.5f, -0.5f,  
            0.5f, -0.5f, -0.5f,  
            0.5f, -0.5f, -0.5f, 
            0.5f, -0.5f,  0.5f,  
            0.5f,  0.5f,  0.5f,  

            -0.5f, -0.5f, -0.5f,  
            0.5f, -0.5f, -0.5f,  
            0.5f, -0.5f,  0.5f,  
            0.5f, -0.5f,  0.5f,  
            -0.5f, -0.5f,  0.5f,
            -0.5f, -0.5f, -0.5f,

            -0.5f,  0.5f, -0.5f,  
            0.5f,  0.5f, -0.5f,  
            0.5f,  0.5f,  0.5f, 
            0.5f,  0.5f,  0.5f,  
            -0.5f,  0.5f,  0.5f,  
            -0.5f,  0.5f, -0.5f,  
        };


        int[] indices =
        {
            0, 1, 3,
            1, 2, 3,
        };

        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsage.StaticDraw);
/*
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ElementBufferObject);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsage.StaticDraw);
        */  
        GL.VertexAttribPointer((uint)shader.GetAttributeLocation("aPosition"), 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);
    }

    public void BindBuffer()
    {
        GL.BindVertexArray(VertexArrayObject);
    }

    public override void Render()
    {
        shader.UseShader();
        
        int model = shader.GetUniform("model");
        int prevModel = shader.GetUniform("prevModel");
        
        int viewProjection = shader.GetUniform("viewProjection");
        int prevViewProjection = shader.GetUniform("prevViewProjection");

        //Console.WriteLine($"Model: {model}, ViewProjection: {viewProjection}");

        GL.UniformMatrix4f(model, 1, true, Transform.GetWorldMatrix());
        GL.UniformMatrix4f(viewProjection, 1, true, Engine.ActiveCamera.viewProjectionMatrix);
        GL.UniformMatrix4f(prevModel, 1, true, Transform.GetPreviousWorldMatrix());
        GL.UniformMatrix4f(prevViewProjection, 1, true, Application.Engine.ActiveCamera.previousViewProjectionMatrix);
        
        BindBuffer();
        
        GL.DrawArrays(PrimitiveType.Triangles, 0, 36);
    }

    public void Dispose()
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.DeleteBuffer(VertexBufferObject);
    }
}