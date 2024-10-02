using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using LegendaryRenderer.Shaders;
using OpenTK.Graphics.OpenGL.Compatibility;
using OpenTK.Mathematics;
using static LegendaryRenderer.Maths;

namespace LegendaryRenderer.EngineTypes;

public class AABB
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

    public void Encapsulate(AABB source)
    {
        Vector3 newMin = Min3(Min, source.Min);
        Vector3 newMax = Max3(Max, source.Max);
        
        Min = newMin;
        Max = newMax;
    }

    public void Set(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    public void RenderDebugVolume()
    {
        ShaderManager.LoadShader("", out ShaderFile shader);
        shader.UseShader();
        GL.Enable(EnableCap.LineStipple);
        GL.LineWidth(2.0f);
        GL.Color3f(Color3.Black);
        GL.LineStipple(1,0x00FF);
        GL.Begin(PrimitiveType.Triangles);
        GL.Vertex3f(0,0,0);
        GL.Vertex3f(0,1,0);
        GL.Vertex3f(0,1,1);
        GL.Vertex3f(1,1,1);
        GL.End();
        GL.Flush();
        GL.Disable(EnableCap.LineStipple);
        
       // Console.WriteLine($"AABB {Min} {Max}");
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

    public bool HasVolume()
    {
        return HasDepth() || HasWidth() || HasHeight();
    }

    public bool HasDepth()
    {
        return Extents.X > Single.Epsilon;
    }

    public bool HasWidth()
    {
        return Extents.Y > Single.Epsilon;
    }

    public bool HasHeight()
    {
        return Extents.Z > Single.Epsilon;
    }
}

