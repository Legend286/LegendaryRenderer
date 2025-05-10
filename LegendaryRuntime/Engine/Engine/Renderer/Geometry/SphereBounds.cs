using OpenTK.Mathematics;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer;

public class SphereBounds
{
    public Vector3 Centre;
    public float Radius;

    public SphereBounds(Vector3 centre, float radius)
    {
        Centre = centre;
        Radius = radius;
    }
    
    public static bool IntersectsOrTouchesSphere(SphereBounds a, SphereBounds b)
    {
        // Calculate the vector between the sphere centers
        Vector3 difference = a.Centre - b.Centre;
    
        // Calculate the squared distance between the centers
        float distanceSquared = difference.LengthSquared; // or difference.sqrMagnitude in Unity
    
        // Calculate the squared sum of the radii
        float combinedRadius = a.Radius + b.Radius;
        float combinedRadiusSquared = combinedRadius * combinedRadius;
    
        // The spheres intersect or touch if the squared distance is less than or equal to the squared sum of the radii.
        return distanceSquared <= combinedRadiusSquared;
    }
}