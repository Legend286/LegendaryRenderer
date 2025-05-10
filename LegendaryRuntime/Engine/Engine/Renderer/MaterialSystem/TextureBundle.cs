using LegendaryRenderer.LegendaryRuntime.Engine.Renderer.MaterialSystem;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MaterialSystem;

public class TextureBundle
{
    List<Texture> BundleTextures { get; }
    public int BundleID { get; private set; }
    public int BundleSize { get; private set; }
    public int BundleWidth { get; private set; }
    public int BundleHeight { get; private set; }
    public int BundleRefCount { get; private set; }
    
    public TextureBundle(int bundleID, int bundleSize, int bundleWidth, int bundleHeight)
    {
        BundleID = bundleID;
        BundleSize = bundleSize;
        BundleWidth = bundleWidth;
        BundleHeight = bundleHeight;
        BundleTextures = new List<Texture>(bundleSize);
        BundleRefCount = 0;
    }
}