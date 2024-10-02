using System.Reflection.Metadata;
using LegendaryRenderer.Geometry;
using OpenTK.Mathematics;

namespace LegendaryRenderer.EngineTypes;

public class AABBTree
{
    public static AABBNode Root { get; private set; }
    private static readonly int MaxDepth = 10;
    
    public static void InitialiseAABBTree()
    {
        Root = new AABBNode();
        foreach (var node in AABBNode.GetNodes())
        {
            Root.Bounds.Encapsulate(node.Bounds);
            Root.Meshes.AddRange(node.Meshes);
        }

        Split(Root);
    }

    public static void RenderTree()
    {
        foreach (var node in AABBNode.GetNodes())
        {
            node.RenderNode();
        }
    }
    
    private static void Split(AABBNode parent, int depth = 0)
    {
        if (depth >= MaxDepth) return;
        
        Vector3 size = parent.Bounds.Size;
        int splitAxis = size.X > MathF.Max(size.Y, size.Z) ? 0 : size.Y > size.Z ? 1 : 2;
        float splitPos = parent.Bounds.Centre[splitAxis];
        
        parent.ChildA = new AABBNode();
        parent.ChildB = new AABBNode();

        foreach (Mesh mesh in parent.Meshes)
        {
            bool inA = mesh.Node.Bounds.Centre[splitAxis] < splitPos;

            AABBNode child = inA ? parent.ChildA : parent.ChildB;

            child.AddMesh(mesh);
            child.Position = mesh.Transform.Position;
            child.Bounds.Encapsulate(mesh.Node.Bounds);
        }
        
        Split(parent.ChildA, depth + 1);
        Split(parent.ChildB, depth + 1);
    }
}