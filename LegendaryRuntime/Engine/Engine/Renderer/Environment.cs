using LegendaryRenderer.LegendaryRuntime.Engine.Renderer.MaterialSystem;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer;

public static class Environment
{
    public static int EnvmapID { get; private set; }

    static Environment()
    {
        EnvmapID = TextureLoader.LoadTexture("HDRMap.tif", true).Reference().GetGLTexture();
    }
}