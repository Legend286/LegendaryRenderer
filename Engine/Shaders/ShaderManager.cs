namespace LegendaryRenderer.Engine.Shaders;

public static class ShaderManager
{
    private static List<ShaderFile> LoadedShaders = new List<ShaderFile>();

    public static bool LoadShader(string file)
    {
        ShaderFile shader = new ShaderFile(file);

        if (shader.IsCompiled)
        {
            LoadedShaders.Add(shader);
            return true;
        }

        return false;
    }
    public static void Dispose()
    {
        foreach (ShaderFile shader in LoadedShaders)
        {
            LoadedShaders.Remove(shader);
            shader.Dispose();
        }
    }
}