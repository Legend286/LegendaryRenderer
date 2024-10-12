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

    private Mesh mesh;
    protected override void OnLoad()
    {
        base.OnLoad();
        
        GL.ClearColor(Color4.Aqua);
        PrintDebugLogInfo();
        
        Camera camera = new Camera(Vector3.One, Vector3.Zero, 45.0f, (float)Application.Width / Application.Height);
        mesh = Mesh.Triangle();
    //    mesh.Transform.SetPosition((0, 0, 0));


        Mesh mesh2;

        for (int x = -10; x < 10; x++)
        {
            for (int y = -10; y < 10; y++)
            {
                mesh2 = Mesh.Triangle();
                mesh2.Transform.SetScale((0.2f,0.2f,0.2f));
                mesh2.Transform.SetPosition(new Vector3(x,0.0f,y));
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
        Application.Width = e.Width;
        Application.Height = e.Height;
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
        Console.WriteLine($"Previous Transform {mesh.Transform.GetPreviousWorldMatrix()} \nCurrent Transform {mesh.Transform.GetWorldMatrix()}");

        mesh.Transform.SetPosition((0, MathF.Sin(deltaAccum*2) * 3, 0));
        mesh.Transform.SetRotationFromEulerAngles((0.0f, deltaAccum * 8.0f, 0.0f));
        
    }

    private void PrintDebugLogInfo()
    {
        
        int nrAttributes = 0;
        GL.GetInteger(GetPName.MaxVertexAttribs, out nrAttributes);
        Console.WriteLine("Maximum number of vertex attributes supported: " + nrAttributes);
        
    }
}