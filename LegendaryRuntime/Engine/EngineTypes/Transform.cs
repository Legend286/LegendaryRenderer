using OpenTK.Mathematics;

namespace LegendaryRenderer.Geometry;

public class Transform
{

    private Matrix4 ObjectToWorld;

    private Matrix4 PreviousObjectToWorld;

    public Vector3 Position { get; private set; }
    public Quaternion Rotation { get; private set; }
    public Vector3 Scale { get; private set; }

    public Transform()
    {
        Position = Vector3.Zero;
        Rotation = Quaternion.Identity;
        Scale = Vector3.One;
        
        UpdateTransformMatrix();
    }

    public Transform(Vector3 position)
    {
        Position = position;
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

    public void SetPosition(Vector3 position)
    {
        Position = position;
        UpdateTransformMatrix();
    }
    
    public void SetRotationFromEulerAngles(Vector3 rotation)
    {
        Rotation = Quaternion.FromEulerAngles(rotation.X, rotation.Y, rotation.Z);
        UpdateTransformMatrix();
    }

    public void SetScale(Vector3 scale)
    {
        Scale = scale;
        UpdateTransformMatrix();
    }
    
    public void UpdatePreviousMatrix()
    {
        PreviousObjectToWorld = ObjectToWorld;
    }
    private void UpdateTransformMatrix()
    {
        Matrix4.CreateTranslation(Position, out Matrix4 translation);
        Matrix4.CreateFromQuaternion(Rotation, out Matrix4 rotation);
        Matrix4.CreateScale(Scale, out Matrix4 scale);

        ObjectToWorld = scale * rotation * translation;
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