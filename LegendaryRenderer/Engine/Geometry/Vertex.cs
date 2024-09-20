using OpenTK.Mathematics;

namespace LegendaryRenderer.Engine.Geometry;

public struct Vertex
{
    private Vector3 position;
    private Vector2 textureCoordinate;
    private Vector3 normal;
    private Vector3 colour;

    public Vertex(Vector3 position)
    {
        this.position = position;
        this.textureCoordinate = Vector2.Zero;
        this.normal = Vector3.UnitZ;
        this.colour = Vector3.One;
    }

    public Vertex(Vector3 position, Vector2 texCoord)
    {
        this.position = position;
        this.textureCoordinate = texCoord; 
        this.normal = Vector3.UnitZ;
        this.colour = Vector3.One;
    }

    public Vertex(Vector3 position, Vector2 texCoord, Vector3 normal)
    {
        this.position = position;
        this.textureCoordinate = texCoord;
        this.normal = normal;
        this.colour = Vector3.One;
    }
}