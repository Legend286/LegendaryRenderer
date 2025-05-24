using System.Numerics;
using OpenTK.Mathematics;
using Quaternion = OpenTK.Mathematics.Quaternion;
using Vector2 = OpenTK.Mathematics.Vector2;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Utilities;

public static class Maths
{
    public static Vector2 Min2(Vector2 v1, Vector2 v2)
    {
        return new Vector2(MathF.Min(v1.X, v2.X), MathF.Min(v1.Y, v2.Y));
    }

    public static Vector2 Max2(Vector2 v1, Vector2 v2)
    {
        return new Vector2(MathF.Max(v1.X, v2.X), MathF.Max(v1.Y, v2.Y));
    }
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

    public static Vector3 Color4ToVector3(Color4 color)
    {
        return new Vector3(color.R, color.G, color.B);
    }

    public static Color4 Vector3ToColor4(Vector3 vector)
    {
        return new Color4(vector.X, vector.Y, vector.Z, 1f);
    }
    
    public static Vector3 FloorVector(Vector3 vector)
    {
        return new Vector3(MathF.Floor(vector.X), MathF.Floor(vector.Y), MathF.Floor(vector.Z));
    }

    public static Vector2 FromNumericsVector2(System.Numerics.Vector2 vector)
    {
        return new Vector2(vector.X, vector.Y);
    }

    public static Vector3 FromNumericsVector3(System.Numerics.Vector3 vector)
    {
        return new Vector3(vector.X, vector.Y, vector.Z);
    }

    public static Matrix3 ToRotationMatrix(Matrix4 matrix4)
    {
        Vector3 x = new Vector3(matrix4.M11, matrix4.M12, matrix4.M13).Normalized();
        Vector3 y = new Vector3(matrix4.M21, matrix4.M22, matrix4.M23).Normalized();
        Vector3 z = new Vector3(matrix4.M31, matrix4.M32, matrix4.M33).Normalized();

        Matrix3 rotationOnly = new Matrix3(
            x.X, x.Y, x.Z,
            y.X, y.Y, y.Z,
            z.X, z.Y, z.Z
        );
        
        return rotationOnly;
    }

    public static System.Numerics.Vector2 ToNumericsVector2(Vector2 vector)
    {
        return new System.Numerics.Vector2(vector.X, vector.Y);
    }
    
    public static System.Numerics.Vector3 ToNumericsVector3(Vector3 vector)
    {
        return new System.Numerics.Vector3(vector.X, vector.Y, vector.Z);
    }

    public static System.Numerics.Matrix4x4 ToNumericsMatrix4x4(Matrix4 matrix)
    {
        Matrix4x4 m = new Matrix4x4();
        m.M11 = matrix.M11;
        m.M12 = matrix.M12;
        m.M13 = matrix.M13;
        m.M14 = matrix.M14;
        m.M21 = matrix.M21;
        m.M22 = matrix.M22;
        m.M23 = matrix.M23;
        m.M24 = matrix.M24;
        m.M31 = matrix.M31;
        m.M32 = matrix.M32;
        m.M33 = matrix.M33;
        m.M34 = matrix.M34;
        m.M41 = matrix.M41;
        m.M42 = matrix.M42;
        m.M43 = matrix.M43;
        m.M44 = matrix.M44;
        return m;
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