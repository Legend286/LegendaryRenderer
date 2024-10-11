using System.Diagnostics;
using LegendaryRenderer.Geometry;
using LegendaryRenderer.Shaders;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace LegendaryRenderer.Application;

public class ApplicationWindow : GameWindow
{
    public ApplicationWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings)
    {
        
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        
        GL.ClearColor(Color4.Aqua);
        PrintDebugLogInfo();

        GL.Enable(EnableCap.DepthTest);

        Camera camera = new Camera(Vector3.One, Vector3.Zero, 45.0f, (float)Application.Width / Application.Height);
       



        for (int x = -1; x < 1; x++)
        {
            for (int y = -1; y < 1; y++)
            {
               //
            }
        }
        
       

    }

    protected override void OnUnload()
    {
        ShaderManager.Dispose();
        
        base.OnUnload();
    }

    protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
    {
        base.OnFramebufferResize(e);

        GL.Viewport(0, 0, e.Width, e.Height);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        Engine.Render();
        
        SwapBuffers();
        
        Title = $"Legendary Renderer - {(1/args.Time).ToString("0.00")} fps - Game Objects: {Engine.GameObjects.Count}";
    }

    private float deltaAccum = 0;
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
        
        deltaAccum += (float)args.Time;
     
        Engine.Update((float)args.Time);
        
        if (KeyboardState.IsKeyDown(Keys.Escape))
        {
            Close();
        }
    
    }

    private void PrintDebugLogInfo()
    {
        
        int nrAttributes = 0;
        GL.GetInteger(GetPName.MaxVertexAttribs, out nrAttributes);
        Console.WriteLine("Maximum number of vertex attributes supported: " + nrAttributes);
        
    }
}