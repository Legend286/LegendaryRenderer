
using OpenTK.Windowing.Desktop;

namespace LegendaryRenderer.Application;

public static class Application
{
    private static ApplicationWindow windowInstance;
    public static int Width;
    public static int Height;

    public static void Initialize(int width, int height, string title, int maxFPS = 120)
    {
        Width = width;
        Height = height;
        if (windowInstance == null)
        {
            windowInstance = CreateWindow(width, height, title, maxFPS);
        }
    }

    public static void Run()
    {
        windowInstance.Run();
    }

    private static ApplicationWindow CreateWindow(int width, int height, string title, int maxFPS = 120)
    {
        var gameSettings = GameWindowSettings.Default;
        gameSettings.UpdateFrequency = maxFPS;
        return new ApplicationWindow(gameSettings,
            new NativeWindowSettings() { Size = (width, height), Title = title });
    }
}