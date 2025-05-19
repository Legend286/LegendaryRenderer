using OpenTK.Mathematics;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MaterialSystem;

public class Material
{
    public float Roughness { get; set; } = 0.5f;
    public float Metallic { get; set; } = 0.0f;
    public float AmbientOcclusion { get; set; } = 1.0f;
    public int DiffuseTexture { get; set; } = -1;
    public int NormalTexture { get; set; } = -1;
    public int RoughnessTexture { get; set; } = -1;
    public int MetallicTexture { get; set; } = -1;
    public int AoTexture { get; set; } = -1;
    public Color4 Colour { get; set; } = new Color4(1, 1, 1, 1);

    public Vector3 GetMaterialColourAsVector()
    {
        return new Vector3(Colour.R, Colour.G, Colour.B); 
    }
}