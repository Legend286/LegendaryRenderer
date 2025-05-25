using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
using LegendaryRenderer.LegendaryRuntime.Engine.Utilities;
using OpenTK.Mathematics;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Engine.EngineTypes;

public class Transform
{
    private Matrix4 ObjectToWorld = Matrix4.Identity;
    private Matrix4 PreviousObjectToWorld = Matrix4.Identity;

    private Vector3 position = Vector3.Zero;
    private Quaternion rotation = Quaternion.Identity;
    private Vector3 scale = Vector3.One;

    public bool HasChanged = true;

    public GameObject? gameObject { get; private set; }

    private Transform? parent;
    public Transform? Parent
    {
        get { return parent; }
        set
        {
            if (value == this || IsAncestor(value))
                throw new InvalidOperationException("Circular reference detected in Transform hierarchy.");

            if (parent == value)
                return; // No change if the parent is the same

            // Store the current local transform before parenting
            Vector3 previousLocalPosition = LocalPosition;
            Quaternion previousLocalRotation = LocalRotation;
        
            if (value != null)
            {
                // Preserve the world rotation by recalculating the local rotation
                localRotation = Quaternion.Normalize(Quaternion.Invert(value.Rotation) * Rotation);
                localPosition = (new Vector4(Position, 1.0f) * value.Inverse()).Xyz;
                localScale = Scale / value.Scale;
            }
            else
            {
                position = Position;
                rotation = Rotation;
                scale = Scale;
            }

            parent = value;
            HasChanged = true;

            // Reapply the stored local transform after parenting
            LocalPosition = previousLocalPosition;
            LocalRotation = previousLocalRotation;
        }
    }

    private Vector3 localPosition = Vector3.Zero;
    private Quaternion localRotation = Quaternion.Identity;
    private Vector3 localScale = Vector3.One;

    public Vector3 LocalPosition
    {
        get { return localPosition; }
        set
        {
            HasChanged = true;
            localPosition = value;
        }
    }

    public Quaternion LocalRotation
    {
        get { return localRotation; }
        set
        {
            HasChanged = true;
            localRotation = value;
        }
    }
    
    public Vector3 Position
    {
        get
        {
            if (parent != null)
            {
                return (new Vector4(localPosition, 1.0f) * parent.GetWorldMatrix()).Xyz;
            }
            return position;
        }
        set
        {
            HasChanged = true;
            if (parent != null)
            {
                position = (new Vector4(value, 1.0f) * parent.Inverse()).Xyz;
                localPosition = value;
            }
            else
            {
                position = value;
                localPosition = value;
            }
        }
    }

    public Vector3 EulerAngles;                                           // In degrees, editable from UI
    public Quaternion Rotation
    {
        get
        {
            rotation = Maths.Rotation(EulerAngles.X, EulerAngles.Y, EulerAngles.Z).Normalized();
            return rotation;
        }
        set
        {
            // Optional: only call this during deserialization or external assignment
            rotation = Quaternion.Normalize(value);
            var rotRadEuler = rotation.ToEulerAngles();
            EulerAngles = new Vector3(
                MathHelper.RadiansToDegrees(rotRadEuler.X),
                MathHelper.RadiansToDegrees(rotRadEuler.Y),
                MathHelper.RadiansToDegrees(rotRadEuler.Z)
            );
        }
    }

    public Vector3 Scale
    {
        get
        {
            if (parent != null)
            {
                return scale * parent.Scale;
            }
            else
            {
                return scale;
            }
        }
        set
        {
            HasChanged = true;
            if (parent != null)
            {
                localScale = value;
            }
            else
            {
                scale = value;
                localScale = value;
            }
        }
    }

    private Quaternion normalizedRotation => Quaternion.Normalize(Rotation);
    private Quaternion normalizedLocalRotation => Quaternion.Normalize(LocalRotation);

    public Vector3 Right => GetRight();
    private Vector3 GetRight() => normalizedRotation * Vector3.UnitX;

    public Vector3 LocalRight => GetLocalRight();
    private Vector3 GetLocalRight() => normalizedLocalRotation * Vector3.UnitX;

    public Vector3 Up => GetUp();
    private Vector3 GetUp() => normalizedRotation * Vector3.UnitY;

    public Vector3 LocalUp => GetLocalUp();
    private Vector3 GetLocalUp() => normalizedLocalRotation * Vector3.UnitY;

    public Vector3 Forward => GetForward();
    private Vector3 GetForward() => normalizedRotation * -Vector3.UnitZ;

    public Vector3 LocalForward => GetLocalForward();
    private Vector3 GetLocalForward() => normalizedLocalRotation * -Vector3.UnitZ;

    public Transform()
    {
        HasChanged = true;
    }

    public Transform(Vector3 position, GameObject go)
    {
        this.gameObject = go;
        this.position = position;
        this.rotation = Quaternion.Identity;
        this.scale = Vector3.One;
        
        HasChanged = true;
    }
    
    public Transform(Vector3 position, Vector3 rotationEuler, Vector3 scale)
    {
        this.position = position;
        this.rotation = Quaternion.FromEulerAngles(rotationEuler);
        this.scale = scale;
        HasChanged = true;
    }

    public void ComposeTransforms(Transform? p)
    {
        HasChanged = true;
        if (p != null)
        {
            Matrix4 mat = p.GetWorldMatrix();
            this.position = (new Vector4(localPosition, 1.0f) * mat).Xyz;
            this.rotation = Quaternion.Normalize(p.Rotation * localRotation); // Ensure correct order and normalization
            this.scale = p.Scale * localScale;
        }
    }

    public void SetRotationFromEulerAngles(Vector3 rotation)
    {
        Rotation = Quaternion.FromEulerAngles(rotation.X, rotation.Y, rotation.Z);
        HasChanged = true;
    }

    public void UpdatePreviousMatrix()
    {
        PreviousObjectToWorld = ObjectToWorld;
    }
    private bool frameDelay = false;

    private void UpdateTransformMatrix()
    {
        UpdatePreviousMatrix();
        if (HasChanged)
        {
            Matrix4 localTranslation = Matrix4.CreateTranslation(localPosition);

            LocalRotation = Maths.Rotation(EulerAngles.X, EulerAngles.Y, EulerAngles.Z).Normalized();
            Matrix4 localRotationMatrix = Matrix4.CreateFromQuaternion(LocalRotation);
            Matrix4 localScaleMatrix = Matrix4.CreateScale(localScale);
            
            
            Matrix4 localMatrix = localScaleMatrix * localRotationMatrix * localTranslation;

            if (parent != null)
            {
                // Combine local matrix with parent's world matrix
                ObjectToWorld = localMatrix * parent.GetWorldMatrix();
            }
            else
            {
                ObjectToWorld = localMatrix;
            }

            HasChanged = false;
        }
    }


    public void Update()
    {
        if (parent != null && parent.HasChanged)
        {
            HasChanged = true;
            ComposeTransforms(parent);
            UpdateTransformMatrix();
        }
        else
        {
            HasChanged = true;
            UpdateTransformMatrix();
        }
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

    private bool IsAncestor(Transform? potentialAncestor)
    {
        Transform? current = this;
        while (current != null)
        {
            if (current == potentialAncestor)
                return true;
            current = current.parent;
        }
        return false;
    }
}
