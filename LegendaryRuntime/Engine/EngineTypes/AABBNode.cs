using LegendaryRenderer.Geometry;

namespace LegendaryRenderer.EngineTypes;

public class AABBNode
{
    public AABB Bounds { get; private set; }

    public int SplitDirection { get; set; }
    public List<Mesh> Meshes { get; private set; }
    public AABBNode ChildA { get; set; }
    public AABBNode ChildB { get; set; }

    public void AddMesh(Mesh mesh)
    {
        Meshes.Add(mesh);
    }

    public AABBNode(int splitDirection)
    {
        Meshes = new List<Mesh>();
        SplitDirection = splitDirection;
    }
    
}