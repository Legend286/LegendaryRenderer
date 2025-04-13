using System.Data;
using System.Runtime;
using Geometry;
using LegendaryRenderer.Application;
using LegendaryRenderer.GameObjects;
using LegendaryRenderer.Shaders;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using App = LegendaryRenderer.Application.Application;
using Eng = LegendaryRenderer.Application.Engine;
using static LegendaryRenderer.Maths;
using OpenTK.Input;


namespace LegendaryRenderer;
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
    
    public Camera(Vector3 position, Vector3 lookAt, float fieldOfView = 90.0f, float zNear = 0.1f, float zFar = 2000.0f) : base(position, $"Camera {++CameraID}")
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

            if (Application.Engine.ActiveCamera == this)
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
            Transform.Position += Transform.Forward * 20 * deltaTime;
        }
        if (ApplicationWindow.keyboardState.IsKeyDown(Keys.S) && MovingCamera)
        {
            Transform.Position += -Transform.Forward * 20 * deltaTime;
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
            Transform.Position += -Vector3.UnitY * 20 * deltaTime;
        }
        if (ApplicationWindow.keyboardState.IsKeyDown(Keys.E) && MovingCamera)
        {
            Transform.Position += Vector3.UnitY * 20 * deltaTime;
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
        
        AspectRatio = ((float)App.Width / (float)App.Height);
        Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(FieldOfView),
            AspectRatio, ZNear, ZFar,
            out Matrix4 projection);
        
        ProjectionMatrix = projection;
        
        ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;

        
    }

    public override void Render(RenderMode mode = RenderMode.Default)
    {
        if (Application.Engine.ActiveCamera != this)
        {
            if (mode == RenderMode.SelectionMask)
            {
                if (!IsVisible)
                {
                    return;
                }
                Frustum.first = true;
                Console.WriteLine($"Camera Guid {GUID}");
                Frustum.DrawFrustum(Frustum.FrustumDrawMode.Selection);
            }
            else
            {
                if (!IsVisible)
                {
                    return;
                }
                Frustum.first = true;
                Frustum.DrawFrustum(Frustum.FrustumDrawMode.Debug);
            }
        }
    }
}