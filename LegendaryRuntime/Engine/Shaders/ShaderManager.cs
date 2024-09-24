using System.Net;

namespace LegendaryRenderer.Shaders;

public static class ShaderManager
{
   // private static List<ShaderFile> LoadedShaders = new List<ShaderFile>();
    private static Dictionary<string, ShaderFile> LoadedShaders = new Dictionary<string, ShaderFile>();

    public enum ShaderLoadStatus
    {
        ERROR_LOADING_FROM_DISK,
        COMPILE_ERROR,
        LINK_ERROR,
        LOADED_FROM_CACHE,
        SUCCESS,
    }
    public static ShaderLoadStatus LoadShader(string file, out ShaderFile loadedShader)
    {
        if (LoadedShaders.ContainsKey(file))
        {
            LoadedShaders.TryGetValue(file, out ShaderFile result);

            if (result != null)
            {
                loadedShader = result;

                return ShaderLoadStatus.LOADED_FROM_CACHE;
            }
        }
        
        ShaderFile shader = new ShaderFile(file);

        if (shader.IsCompiled)
        {
            LoadedShaders.Add(file, shader);

            loadedShader = shader;
            return ShaderLoadStatus.SUCCESS;
        }
        else
        {
            loadedShader = ShaderFile.GetErrorShader();
            return ShaderLoadStatus.COMPILE_ERROR;
        }
    }
    public static void Dispose()
    {
        foreach(KeyValuePair<string, ShaderFile> shaderKV in LoadedShaders)
        {
            shaderKV.Value.Dispose();
            LoadedShaders.Remove(shaderKV.Key);
        }
    }
}