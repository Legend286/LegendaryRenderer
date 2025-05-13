using LegendaryRenderer.Application;
using LegendaryRenderer.LegendaryRuntime.Application;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using App = LegendaryRenderer.LegendaryRuntime.Application.Application;
using Eng = LegendaryRenderer.LegendaryRuntime.Engine.Engine.Engine;
using static LegendaryRenderer.LegendaryRuntime.Engine.Utilities.Maths;
using FrustumDrawMode = LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.Frustum.FrustumDrawMode;


namespace LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
public class Camera : GameObject
{
    public Matrix4 ProjectionMatrix;
    public Matrix4 ViewMatrix;

    private static int CameraID;
    public Vector3 Target;

    public Frustum Frustum;
    
    public Matrix4 ViewProjectionMatrix;
    public Matrix4 PreviousViewProjectionMatrix;

    public Vector2 MousePosition;

    public float ZNear = 0.1f;
    public float ZFar = 1000.0f;
    public float FieldOfView = 90.0f;
    public float AspectRatio = 1.0f;

    public bool PauseCameraFrustum = false;
    
    public Camera(Vector3 position, Vector3 lookAt, float fieldOfView = 90.0f, float zNear = 0.1f, float zFar = 4000.0f) : base(position, $"Camera {++CameraID}")
    {
        ZNear = zNear;
        ZFar = zFar;
        FieldOfView = fieldOfView;
        Target = lookAt;
        AspectRatio = ((float)App.Width / (float)App.Height);
        
        ViewMatrix = Matrix4.LookAt(position, lookAt, Vector3.UnitY);
        Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(FieldOfView),
                                            AspectRatio, ZNear, ZFar, out Matrix4 projection);
        ProjectionMatrix = projection;
        ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;
        Frustum = new Frustum(this);
        
    }
    Vector2 LastMousePosition = Vector2.Zero;
    
    private bool previousFrame = true;
    private Vector2 AccumDelta;
    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);
        
        bool MovingCamera = false;
        PreviousViewProjectionMatrix = ViewProjectionMatrix;

       

        if (ApplicationWindow.mouseState.IsButtonDown(MouseButton.Right))
        {
            App.SetCursorVisible(false);

            if (Eng.ActiveCamera == this)
            {
                MovingCamera = true;
            }
            else
            {
                MovingCamera = false;
            }

        }
        else
        {
            App.SetCursorVisible(true);
            MovingCamera = false;
        }


        if (MovingCamera)
        {
            Vector2 delta = LastMousePosition - MousePosition;

            AccumDelta += delta * 0.1f;

            AccumDelta.Y = Math.Clamp(AccumDelta.Y, -89.0f, 89.0f);

            Quaternion pitch = Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(AccumDelta.Y));
            Quaternion yaw = Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(AccumDelta.X));

            Transform.Rotation = yaw * pitch;
        }

        if (ApplicationWindow.keyboardState.IsKeyDown(Keys.W) && MovingCamera)
        {
            Transform.Position += Transform.Forward * 5 * deltaTime;
        }
        if (ApplicationWindow.keyboardState.IsKeyDown(Keys.S) && MovingCamera)
        {
            Transform.Position += -Transform.Forward * 5 * deltaTime;
        }
        if (ApplicationWindow.keyboardState.IsKeyDown(Keys.A) && MovingCamera)
        {
            Transform.Position += ProjectVectorOntoPlane(-Transform.Right, Vector3.UnitY) * 20 * deltaTime;
        }
        if (ApplicationWindow.keyboardState.IsKeyDown(Keys.D) && MovingCamera)
        {
            Transform.Position += ProjectVectorOntoPlane(Transform.Right, Vector3.UnitY) * 20 * deltaTime;
        }
        if (ApplicationWindow.keyboardState.IsKeyDown(Keys.Q) && MovingCamera)
        {
            Transform.Position += -Vector3.UnitY * 5 * deltaTime;
        }
        if (ApplicationWindow.keyboardState.IsKeyDown(Keys.E) && MovingCamera)
        {
            Transform.Position += Vector3.UnitY * 5 * deltaTime;
        }

        if (ApplicationWindow.keyboardState.IsKeyReleased(Keys.P))
        {
            PauseCameraFrustum = !PauseCameraFrustum;
        }

        LastMousePosition = MousePosition;

        if (!PauseCameraFrustum)
        {
            Frustum.UpdateFrustumPlanes(ViewProjectionMatrix);
        }
        
        ViewMatrix = Matrix4.LookAt(Transform.LocalPosition, Transform.Position + Transform.Forward * 50, Vector3.UnitY);
        
        AspectRatio = ((float)Eng.EditorViewport.ViewportSize.X / (float)Eng.EditorViewport.ViewportSize.Y);
        Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(FieldOfView),
            AspectRatio, ZNear, ZFar,
            out Matrix4 projection);
        
        ProjectionMatrix = projection;
        
        ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;

        
    }

    public override void Render(RenderMode mode = RenderMode.Default)
    {
        if (Eng.ActiveCamera != this)
        {
            if (mode == RenderMode.SelectionMask)
            {
                if (!IsVisible)
                {
                    return;
                }
                Frustum.first = true;
                Console.WriteLine($"Camera Guid {GUID}");
                Frustum.DrawFrustum(FrustumDrawMode.Selection);
            }
            else
            {
                if (!IsVisible)
                {
                    return;
                }
                Frustum.first = true;
                Frustum.DrawFrustum(FrustumDrawMode.Debug);
            }
        }
    }
    
    public Vector3 Unproject(Vector2 mousePos, Vector2 viewportSize)
    {
        // Normalize screen coordinates to range [-1, 1]
        Vector2 ndc = new Vector2(
            (mousePos.X / viewportSize.X) * 2f - 1f,
            1f - (mousePos.Y / viewportSize.Y) * 2f // Y is flipped
        );

        // NDC coordinates at near plane (z = -1) and far plane (z = 1)
        Vector4 nearPointNDC = new Vector4(ndc.X, ndc.Y, -1f, 1f);
        Vector4 farPointNDC = new Vector4(ndc.X, ndc.Y, 1f, 1f);

        Matrix4 invVP = Matrix4.Invert(ViewMatrix * ProjectionMatrix);
       
        // Unproject to world space
        Vector4 nearWorld = nearPointNDC * invVP;
        Vector4 farWorld = farPointNDC * invVP;
        nearWorld /= nearWorld.W;
        farWorld /= farWorld.W;

        // Ray direction
        Vector3 rayDir = Vector3.Normalize((farWorld.Xyz - nearWorld.Xyz));

        return rayDir;
    }
}