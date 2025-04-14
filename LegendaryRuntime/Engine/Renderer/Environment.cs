using Geometry;
using LegendaryRenderer.LegendaryRuntime.Engine.Renderer.MaterialSystem;

namespace TheLabs.LegendaryRuntime.Engine.Renderer;

public static class Environment
{
    public static int EnvmapID { get; private set; }

    static Environment()
    {
        EnvmapID = TextureLoader.LoadTexture("HDRMap.tif", true);
    }
}