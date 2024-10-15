using System.Diagnostics;
using LegendaryRenderer.GameObjects;
using OpenTK.Mathematics;
using static LegendaryRenderer.Application.Application;

namespace LegendaryRenderer;

public class Camera : GameObject
{
    private Matrix4 projectionMatrix;
    private Matrix4 viewMatrix;
    public Vector3 Target;

    public Matrix4 viewProjectionMatrix;
    public Matrix4 previousViewProjectionMatrix;

    public Camera(Vector3 position, Vector3 lookAt, float fieldOfView, float aspectRatio) : base(position)
    {
        viewMatrix = Matrix4.Identity;
        viewMatrix = Matrix4.LookAt(position, lookAt, Vector3.UnitY);
        
        
        Target = lookAt;

        projectionMatrix = Matrix4.Identity;
        Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f),
                                            ((float)Width / (float)Height),
                                            0.1f, 100.0f, out Matrix4 projection);
        
        
        projectionMatrix = projection;

        viewProjectionMatrix = viewMatrix * projectionMatrix;
    }

    private float deltaAccum = 0;
    private bool previousFrame = true;
    public override void Update(float deltaTime)
    {
        if (previousFrame)
        {
            previousViewProjectionMatrix = viewProjectionMatrix;
        }

        previousFrame = !previousFrame;

        viewMatrix = Matrix4.Identity;
        viewMatrix = Matrix4.LookAt(Transform.Position, Target, Vector3.UnitY);
        projectionMatrix = Matrix4.Identity;
        Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f),
            ((float)Width / (float)Height), 0.1f, 100.0f,
            out Matrix4 projection);
        projectionMatrix = projection;

        viewProjectionMatrix = viewMatrix * projectionMatrix;
        
        deltaAccum += deltaTime;
        Transform.SetPosition(new Vector3(8, 4, 0));
    }
}