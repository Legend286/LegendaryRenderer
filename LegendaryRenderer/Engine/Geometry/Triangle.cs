using OpenTK.Mathematics;

namespace LegendaryRenderer.Engine.Geometry;

public class Triangle
{
    private Vertex[] vertices;

    public Vertex First
    {
        get => vertices[0];
    }
    public Vertex Second
    {
        get => vertices[1];
    }
    public Vertex Third
    {
        get => vertices[2]; 
    }

    public Triangle(Vector3 a, Vector3 b, Vector3 c)
    {
        vertices = new Vertex[3];
        vertices[0] = new Vertex(a);
        vertices[1] = new Vertex(b);
        vertices[2] = new Vertex(c);
    }

    public static Triangle Single()
    {
        Vector3 a = new Vector3(-0.5f, -0.5f, 0.0f);
        Vector3 b = new Vector3(0.5f, -0.5f, 0.0f);
        Vector3 c = new Vector3(0.0f, 0.5f, 0.0f);
        
        return new Triangle(a, b, c);
    }
}