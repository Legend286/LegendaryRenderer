using OpenTK.Mathematics;

namespace Geometry;

public class AABB
{
    public Vector3 Centre { get; private set; }
    public Vector3 Extents { get; private set; }
    public Vector3 Min { get; private set; }
    public Vector3 Max { get; private set; }

    public AABB(Vector3 Min, Vector3 Max)
    {
        this.Min = Min;
        this.Max = Max;
        this.Centre = (Max + Min) * 0.5f;
        this.Extents = Max - Centre;
    }
}