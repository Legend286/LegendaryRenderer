using Geometry;

namespace TheLabs.LegendaryRuntime.Engine.Renderer;

public static class Environment
{
    public static int EnvmapID { get; private set; }

    static Environment()
    {
        EnvmapID = RenderableMesh.LoadTexture("LegendaryRuntime/Resources/HDRMap.tif", "", true);
    }
}