using System.Runtime.CompilerServices;
using OpenTK.Mathematics;
using static LegendaryRenderer.Engine.Maths;

namespace LegendaryRenderer.Engine.EngineTypes;

public struct AABB
{
    public Vector3 Min = Vector3.PositiveInfinity;
    public Vector3 Max = Vector3.NegativeInfinity;

    public Vector3 Size
    {
        get => Max - Min;
    }

    public Vector3 Extents
    {
        get => (Max - Min) * 0.5f;
    }
    
    public Vector3 Centre
    {
        get => (Min + Max) * 0.5f;
    }

    public AABB(Vector3 minimum, Vector3 maximum)
    {
        Min = minimum;
        Max = maximum;
    }

    public AABB Encapsulate(AABB source)
    {
        Vector3 newMin = Min3(Min, source.Min);
        Vector3 newMax = Max3(Max, source.Max);
        
        return new AABB(newMin, newMax);
    }

    public void GrowToInclude(Vector3 source)
    {
        Vector3 newMin = Min3(Min, source);
        Vector3 newMax = Max3(Max, source);
        
        Min = newMin;
        Max = newMax;
    }

    public void Modify(float amount)
    {
        Min -= new Vector3(amount, amount, amount);
        Max += new Vector3(amount, amount, amount);
    }
}

