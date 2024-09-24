using OpenTK.Mathematics;

namespace LegendaryRenderer.Geometry;

public struct Vertex
{
    public Vector3 position { get; private set; }
   /* public Vector2 textureCoordinate { get; private set; }
    public Vector3 normal { get; private set; }
    public Vector3 colour { get; private set; }
*/
    public Vertex(Vector3 position)
    {
        this.position = position;
        /*
        this.textureCoordinate = Vector2.Zero;
        this.normal = Vector3.UnitZ;
        this.colour = Vector3.One;*/
    }

    public Vertex(Vector3 position, Vector2 texCoord)
    {
        this.position = position;
        /*
        this.textureCoordinate = texCoord; 
        this.normal = Vector3.UnitZ;
        this.colour = Vector3.One;*/
    }

    public Vertex(Vector3 position, Vector2 texCoord, Vector3 normal)
    {
        this.position = position;
        /*
        this.textureCoordinate = texCoord;
        this.normal = normal;
        this.colour = Vector3.One;*/
    }
}