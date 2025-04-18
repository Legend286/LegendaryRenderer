using OpenTK.Mathematics;

namespace LegendaryRenderer;

public static class Maths
{
    public static Vector3 Min3(Vector3 a, Vector3 b)
    {
        return new Vector3(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y), MathF.Min(a.Z, b.Z));
    }
    public static Vector3 Max3(Vector3 a, Vector3 b)
    {
        return new Vector3(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y), MathF.Max(a.Z, b.Z));
    }

    public static float Fractional(float x)
    {
        return x - (float)MathF.Floor(x);
    }

    public static Vector3 FloorVector(Vector3 vector)
    {
        return new Vector3(MathF.Floor(vector.X), MathF.Floor(vector.Y), MathF.Floor(vector.Z));
    }
    
    public static Vector3 ProjectVectorOntoPlane(Vector3 vector, Vector3 planeNormal)
    {
        Vector3 normalizedNormal = planeNormal.Normalized();
        
        float dotProduct = Vector3.Dot(vector, normalizedNormal);
        
        Vector3 projection = vector - dotProduct * normalizedNormal;
        
        return projection;
    }

    public static Quaternion Rotation(float pitch, float yaw, float roll)
    {
        return Quaternion.FromEulerAngles(MathHelper.DegreesToRadians(pitch), MathHelper.DegreesToRadians(yaw), MathHelper.DegreesToRadians(roll)).Normalized();
    }
}