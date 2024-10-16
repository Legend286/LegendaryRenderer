using System.Diagnostics;
using Geometry;
using LegendaryRenderer.FileLoaders;
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

    Mesh mesh;

    Mesh[] instances = new Mesh[100];

    protected override void OnLoad()
    {
        base.OnLoad();
        
        GL.ClearColor(Color4.Aqua);
        PrintDebugLogInfo();

        if (ObjLoader.LoadFromFile("LegendaryRuntime/Resources/teapot.model", out Mesh msh))
        {
            mesh = msh;

        }

        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(CullFaceMode.Back);

        Camera camera = new Camera(Vector3.One, Vector3.Zero, 45.0f, (float)Application.Width / Application.Height);

        //mesh = new CubeMesh(Vector3.Zero);

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

    float dtAccum = 0;

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        dtAccum += (float)args.Time;
        base.OnRenderFrame(args);
        Engine.TriangleCountTotal = 0;

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        // mesh2.localTransform.SetPosition(new Vector3(2.0f, MathF.Sin(dtAccum * 2) * 2, 0.0f));
        // mesh2.localTransform.SetRotationFromEulerAngles(new Vector3(dtAccum*8, dtAccum * 2, 0));
        // mesh2.localTransform.SetScale(Vector3.One * 0.05f);

        //  mesh3.localTransform.SetPosition(new Vector3(-6.0f, MathF.Sin(dtAccum * 2) * 2, 0.0f));
        //  mesh3.localTransform.SetRotationFromEulerAngles(new Vector3(dtAccum * 8, dtAccum * 2, 0));
        //  mesh3.localTransform.SetScale(Vector3.One * 0.25f);

        GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

        mesh.Render();

        Engine.Render();

        SwapBuffers();
        
        Title = $"Legendary Renderer - {(1/args.Time).ToString("0.00")} fps - Game Objects: {Engine.GameObjects.Count} - Total Triangles {Engine.TriangleCountTotal}";
    }

    private float deltaAccum = 0;
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        deltaAccum += (float)args.Time;

        Engine.Update((float)args.Time);

        mesh.localTransform.SetPosition(new Vector3(0.0f, -1+ MathF.Sin(dtAccum * 2) * 0.5f, 0.0f));
        mesh.localTransform.SetRotationFromEulerAngles(new Vector3(0, dtAccum * 0.5f, 0));
        mesh.localTransform.SetScale(Vector3.One * 0.3f);

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