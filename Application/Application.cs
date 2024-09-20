
using OpenTK.Windowing.Desktop;

namespace LegendaryRenderer.Application;

public class Application
{
    private ApplicationWindow windowInstance;

    public Application(int width, int height, string title, int maxFPS = 120)
    {
        if (windowInstance == null)
        {
            windowInstance = CreateWindow(width, height, title, maxFPS);
        }
    }

    public void Run()
    {
        windowInstance.Run();
    }

    private ApplicationWindow CreateWindow(int width, int height, string title, int maxFPS = 120)
    {
        var gameSettings = GameWindowSettings.Default;
        gameSettings.UpdateFrequency = maxFPS;
        return new ApplicationWindow(gameSettings,
            new NativeWindowSettings() { Size = (width, height), Title = title });
    }
}