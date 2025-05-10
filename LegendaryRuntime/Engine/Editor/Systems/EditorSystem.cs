using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MaterialSystem;

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
    
    public static TextureBundle EditorTextureBundle { get; private set; }
    
    public static void Initialise()
    {
        
    }
    
}