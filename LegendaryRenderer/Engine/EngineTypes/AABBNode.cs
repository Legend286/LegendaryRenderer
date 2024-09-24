using LegendaryRenderer.Engine.Geometry;

namespace LegendaryRenderer.Engine.EngineTypes;

public class AABBNode
{
    public AABB Bounds { get; private set; }
    public List<Mesh> Meshes { get; private set; }
    public AABBNode ChildA { get; set; }
    public AABBNode ChildB { get; set; }

    public void AddMesh(Mesh mesh)
    {
        Meshes.Add(mesh);
    }
    
}