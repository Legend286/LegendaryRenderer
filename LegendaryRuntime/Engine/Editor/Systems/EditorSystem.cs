using LegendaryRenderer.LegendaryRuntime.Application.ProgressReporting;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MaterialSystem;
using LegendaryRenderer.LegendaryRuntime.Engine.Renderer.MaterialSystem;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor.Systems;

public static class EditorSystem
{
    public static bool IsEditorMode { get; private set; } = true;
    public static bool IsPlaying { get; private set; } = false;
    public static bool IsPaused { get; private set; } = false;
    
    public static bool IsPlayingAndPaused => IsPlaying && IsPaused;
    public static bool IsPlayingAndNotPaused => IsPlaying && !IsPaused;
    public static bool IsNotPlaying => !IsPlaying;
    public static bool IsEditor => IsEditorMode;
  
    public static List<string> EditorTexturePaths = new List<string>();
    public static Dictionary<string, Texture> EditorTextures = new Dictionary<string, Texture>();
    public static void Initialise()
    {
        EditorTexturePaths.Clear();
        EditorTextures.Clear();

        // Load editor textures
        EditorTexturePaths.Add("Editor/add.png");
        EditorTexturePaths.Add("Editor/back_arrow.png");
        EditorTexturePaths.Add("Editor/close.png");
        EditorTexturePaths.Add("Editor/delete.png");
        EditorTexturePaths.Add("Editor/favourite.png");
        EditorTexturePaths.Add("Editor/forward_arrow.png");
        EditorTexturePaths.Add("Editor/light_bulb.png");
        EditorTexturePaths.Add("Editor/menu.png");
        EditorTexturePaths.Add("Editor/radio_off.png");
        EditorTexturePaths.Add("Editor/radio_on.png");
        EditorTexturePaths.Add("Editor/settings.png");
        EditorTexturePaths.Add("Editor/sun.png");
        EditorTexturePaths.Add("Editor/weather.png");
        EditorTexturePaths.Add("Editor/worklight_off.png");
        EditorTexturePaths.Add("Editor/worklight_on.png");

        using (var progressBar = new ConsoleProgressBar())
        {
            int i = 0;
            foreach (var texturePath in EditorTexturePaths)
            {
                progressBar.Report((double)i / EditorTexturePaths.Count, $"Processing item {i} of 100...");

                Texture texture = TextureLoader.LoadTexture(texturePath, false);
                if (texture != null)
                {
                    EditorTextures.Add(texturePath, texture);
                    i++;
                }
                else
                {
                    Console.WriteLine($"Failed to load editor texture: {texturePath}");
                }
            }
        }
    }

    public static void Shutdown()
    {
        foreach (var texture in EditorTextures.Values)
        {
            texture.Dispose();
        }
        
        EditorTextures.Clear();
        EditorTexturePaths.Clear();
    }
    
}
