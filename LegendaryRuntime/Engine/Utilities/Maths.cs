using OpenTK.Mathematics;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Utilities;

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
    
    public static uint Color4ToUint(Color4 color)
    {
        byte r = (byte)(Math.Clamp(color.R, 0f, 1f) * 255f);
        byte g = (byte)(Math.Clamp(color.G, 0f, 1f) * 255f);
        byte b = (byte)(Math.Clamp(color.B, 0f, 1f) * 255f);
        byte a = (byte)(Math.Clamp(color.A, 0f, 1f) * 255f);

        return (uint)(r) | ((uint)(g) << 8) | ((uint)(b) << 16) | ((uint)(a) << 24);
    }
    
    public static Vector3 FloorVector(Vector3 vector)
    {
        return new Vector3(MathF.Floor(vector.X), MathF.Floor(vector.Y), MathF.Floor(vector.Z));
    }

    public static Vector2 FromNumericsVector2(System.Numerics.Vector2 vector)
    {
        return new Vector2(vector.X, vector.Y);
    }

    public static System.Numerics.Vector2 ToNumericsVector2(Vector2 vector)
    {
        return new System.Numerics.Vector2(vector.X, vector.Y);
    }
    
    public static Vector3 SnapVectorToGrid(Vector3 vector, float snapSize)
    {
        return FloorVector(vector / snapSize) * snapSize;
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
    
    public static float[] ToFloatArray(Matrix4 mat)
    {
        float[] array = new float[16];
        array[0] = mat.M11; array[1] = mat.M12; array[2] = mat.M13; array[3] = mat.M14;
        array[4] = mat.M21; array[5] = mat.M22; array[6] = mat.M23; array[7] = mat.M24;
        array[8] = mat.M31; array[9] = mat.M32; array[10] = mat.M33; array[11] = mat.M34;
        array[12] = mat.M41; array[13] = mat.M42; array[14] = mat.M43; array[15] = mat.M44;

        return array;
    }
}