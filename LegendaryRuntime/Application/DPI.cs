using OpenTK.Mathematics;

namespace LegendaryRenderer.Application;

public static class DPI
{
    public static Vector2 DPIScale = new Vector2(1.0f, 1.0f);
    public static void SetDPIScale(Vector2 scale)
    {
        DPIScale = scale;
    }
    public static Vector2 GetDPIScale()
    {
        return DPIScale;
    }
}