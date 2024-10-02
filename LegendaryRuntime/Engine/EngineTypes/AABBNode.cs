using LegendaryRenderer.Geometry;
using OpenTK.Mathematics;

namespace LegendaryRenderer.EngineTypes;

public class AABBNode
{
    private static List<AABBNode> AllNodes = new List<AABBNode>();
    public AABB Bounds { get; private set; } = new AABB((0.0f,0.0f,0.0f), (0.0f,0.0f,0.0f));
    
    public Vector3 Position { get; set; }
    
    public int SplitDirection { get; set; } = 0;
    public List<Mesh> Meshes { get; private set; }
    public AABBNode ChildA { get; set; }
    public AABBNode ChildB { get; set; }

    public void AddMesh(Mesh mesh)
    {
        Meshes.Add(mesh);
        Position = mesh.Transform.Position;
    }

    public void RenderNode()
    {
        Bounds.RenderDebugVolume();
    }

    public AABBNode()
    {
        Bounds = new AABB((Vector3.Zero - Vector3.One), (Vector3.One));
        Meshes = new List<Mesh>();
        AddNode(this);
    }

    public static void AddNode(AABBNode node)
    {
        AllNodes.Add(node);
    }

    public static List<AABBNode> GetNodes()
    {
        return AllNodes;
    }
    
}