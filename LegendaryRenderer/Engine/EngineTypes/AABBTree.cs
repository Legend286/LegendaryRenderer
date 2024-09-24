using LegendaryRenderer.Engine.EngineTypes;
using LegendaryRenderer.Engine.Geometry;
using OpenTK.Mathematics;

namespace LegendaryRenderer.LegendaryRenderer.Engine.EngineTypes;

public class AABBTree
{
    public AABBNode Root { get; private set; }
    private int MaxDepth = 10;
    
    public AABBTree(List<AABBNode> nodes)
    {
        Root = new AABBNode(-1);
        
        foreach (var node in nodes)
        {
            Root.Bounds.Encapsulate(node.Bounds);
            Root.Meshes.AddRange(node.Meshes);
        }

        Split(Root);
    }

    public void Split(AABBNode parent, int depth = 0)
    {
        if (depth >= MaxDepth) return;
        
        Vector3 size = Root.Bounds.Size;
        int splitAxis = size.X > MathF.Max(size.Y, size.Z) ? 0 : size.Y > size.Z ? 1 : 2;
        float splitPos = parent.Bounds.Centre[splitAxis];
        
        parent.ChildA = new AABBNode(splitAxis);
        parent.ChildB = new AABBNode(splitAxis);

        foreach (Mesh mesh in parent.Meshes)
        {
            bool inA = mesh.Bounds.Centre[splitAxis] < splitPos;

            AABBNode child = inA ? parent.ChildA : parent.ChildB;

            child.AddMesh(mesh);
            child.Bounds.Encapsulate(mesh.Bounds);
        }

        Split(parent.ChildA, depth + 1);
        Split(parent.ChildB, depth + 1);
    }
}