using OpenTK.Graphics.Vulkan;
using OpenTK.Mathematics;

namespace LegendaryRenderer.Engine.Geometry;

public struct Transform
{
    public Transform()
    {
        Position = Vector3.Zero;
        Rotation = Quaternion.Identity;
        Scale = Vector3.One;
        
        UpdateTransformMatrix();
    }

    public Transform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
        
        UpdateTransformMatrix();
    }

    public Transform(Vector3 position, Vector3 rotationEuler, Vector3 scale)
    {
        Position = position;
        Rotation = Quaternion.FromEulerAngles(rotationEuler);
        Scale = scale;
        
        UpdateTransformMatrix();
    }
    
    public void SetRotationFromEulerAngles(Vector3 rotation)
    {
        Rotation = Quaternion.FromEulerAngles(rotation.X, rotation.Y, rotation.Z);
    }
    
    
    private Matrix4 ObjectToWorld;
    private Matrix4 PreviousObjectToWorld;
    
    public Vector3 Position
    {
        get => Position;
        set
        {
            Position = value;
            UpdateTransformMatrix();
        }
    }
    private Quaternion Rotation
    {
        get => Rotation;
        set
        {
            Rotation = value;
            UpdateTransformMatrix();
        }
    }
    public Vector3 Scale
    {
        get => Scale;
        set
        {
            Scale = value;
            UpdateTransformMatrix();
        }
    }

    private void UpdateTransformMatrix()
    {
        PreviousObjectToWorld = ObjectToWorld;
        
        Matrix4 target = Matrix4.Identity;
        Matrix4 translation = Matrix4.Identity;
        Matrix4 rotation = Matrix4.Identity;
        Matrix4 scale = Matrix4.Identity;
        
        Matrix4.CreateTranslation(Position, out translation);
        Matrix4.CreateFromQuaternion(Rotation, out rotation);
        Matrix4.CreateScale(Scale, out scale);

        ObjectToWorld = translation * rotation * scale;
    }

    public Matrix4 GetWorldMatrix()
    {
        return ObjectToWorld;
    }

    public Matrix4 GetPreviousWorldMatrix()
    {
        return PreviousObjectToWorld;
    }

    public Matrix4 Inverse()
    {
        Matrix4.Invert(ObjectToWorld, out Matrix4 inv);
        return inv;
    }
}