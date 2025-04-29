using External.ImguiController;
using ImGuiNET;
using LegendaryRenderer.Application;
using LegendaryRenderer.Geometry;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using TheLabs.LegendaryRuntime.Engine.GameObjects;


namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor.Gizmos;

public static class Gizmos
{
    private enum Axis
    {
        None = -1,
        X = 0,
        Y = 1,
        Z = 2
    }

    private static Axis _activeAxis = Axis.None;
    private static bool _dragging = false;
    public static bool isGizmoActive = false;
    
    private static bool ClipLineToRect(ref Vector2 a, ref Vector2 b, Vector2 min, Vector2 max)
    {
        // Cohen–Sutherland clipping
        int codeA = ComputeOutCode(a, min, max);
        int codeB = ComputeOutCode(b, min, max);

        while (true)
        {
            if ((codeA | codeB) == 0)
            {
                // Trivially accepted
                return true;
            }
            if ((codeA & codeB) != 0)
            {
                // Trivially rejected
                return false;
            }

            int outCode = (codeA != 0) ? codeA : codeB;
            Vector2 intersection = Vector2.Zero;

            if ((outCode & 8) != 0) // Top
            {
                intersection.X = a.X + (b.X - a.X) * (max.Y - a.Y) / (b.Y - a.Y);
                intersection.Y = max.Y;
            }
            else if ((outCode & 4) != 0) // Bottom
            {
                intersection.X = a.X + (b.X - a.X) * (min.Y - a.Y) / (b.Y - a.Y);
                intersection.Y = min.Y;
            }
            else if ((outCode & 2) != 0) // Right
            {
                intersection.Y = a.Y + (b.Y - a.Y) * (max.X - a.X) / (b.X - a.X);
                intersection.X = max.X;
            }
            else if ((outCode & 1) != 0) // Left
            {
                intersection.Y = a.Y + (b.Y - a.Y) * (min.X - a.X) / (b.X - a.X);
                intersection.X = min.X;
            }

            if (outCode == codeA)
            {
                a = intersection;
                codeA = ComputeOutCode(a, min, max);
            }
            else
            {
                b = intersection;
                codeB = ComputeOutCode(b, min, max);
            }
        }
    }

    private static int ComputeOutCode(Vector2 p, Vector2 min, Vector2 max)
    {
        int code = 0;
        if (p.X < min.X) code |= 1;
        else if (p.X > max.X) code |= 2;
        if (p.Y < min.Y) code |= 4;
        else if (p.Y > max.Y) code |= 8;
        return code;
    }

    public static void DrawSpotLightCone(
        Camera camera,
        Light light,
        Vector2 viewportSizeInPoints,
        Vector2 viewportPosition
    )
    {
        var drawList = ImGui.GetForegroundDrawList();

        // 1) Project light origin to screen-space
        Vector2 originSS = Project(camera, light.Transform.Position, viewportSizeInPoints);

        // 2) Build a basis (right, up) for the light
        Vector3 up = Vector3.UnitY;
        if (Vector3.Dot(up, light.Transform.Forward) > 0.99f) // If light is pointing almost up, switch up vector
            up = Vector3.UnitZ;

        Vector3 right = Vector3.Normalize(Vector3.Cross(light.Transform.Forward, up));
        up = Vector3.Normalize(Vector3.Cross(right, light.Transform.Forward));

        // 3) Calculate the radius of the cone base
        float halfAngleRad = MathHelper.DegreesToRadians(light.OuterCone * 0.5f);
        float radius = MathF.Tan(halfAngleRad) * light.Range;

        // Viewport rectangle
        Vector2 vpMin = viewportPosition;
        Vector2 vpMax = viewportPosition + viewportSizeInPoints;

        // 4) Draw several lines around the base
        const int numSegments = 16;
        for (int i = 0; i < numSegments; i++)
        {
            float angle0 = MathF.PI * 2f * (i / (float)numSegments);
            float angle1 = MathF.PI * 2f * ((i + 1) / (float)numSegments);

            Vector3 offset0 = (right * MathF.Cos(angle0) + up * MathF.Sin(angle0)) * radius;
            Vector3 offset1 = (right * MathF.Cos(angle1) + up * MathF.Sin(angle1)) * radius;

            Vector3 worldPos0 = light.Transform.Position + light.Transform.Forward * light.Range + offset0;
            Vector3 worldPos1 = light.Transform.Position + light.Transform.Forward * light.Range + offset1;

            Vector2 screen0 = Project(camera, worldPos0, viewportSizeInPoints);
            Vector2 screen1 = Project(camera, worldPos1, viewportSizeInPoints);
          
            // Offset by viewport top-left position
            screen0 += viewportPosition;
            screen1 += viewportPosition;

            uint colour = Maths.Color4ToUint(light.Colour);
            if (ClipLineToRect(ref screen0, ref screen1, vpMin, vpMax))
            {
                // draw base circle
                drawList.AddLine(
                    new System.Numerics.Vector2(screen0.X, screen0.Y),
                    new System.Numerics.Vector2(screen1.X, screen1.Y),
                    colour, // semi-transparent white
                    1.5f
                );
            }
            
            

            Vector2 screenOrigin = new Vector2(originSS.X + viewportPosition.X, originSS.Y + viewportPosition.Y);

            // draw lines from tip to base
             
            screen0 = Project(camera, worldPos0, viewportSizeInPoints);
            // Offset by viewport top-left position
            screen0 += viewportPosition;
            
            if (ClipLineToRect(ref screenOrigin, ref screen0, vpMin, vpMax))
            {
                
                drawList.AddLine(
                    new System.Numerics.Vector2(screen0.X, screen0.Y),
                    new System.Numerics.Vector2(screenOrigin.X, screenOrigin.Y),
                    colour,
                    1.0f
                );
            }
        }
    }

    public static void DrawAndHandle(
        ref Transform initial,
        ref readonly Camera camera,
        ref readonly Vector2 rawMousePos,
        ref readonly Vector2 viewportSizeInPoints,
        ref readonly Vector2 viewportPosition
    )
    {

        /*
        var dl = ImGui.GetWindowDrawList();
        
        dl.AddCallback((ImDrawListPtr parentList, ImDrawCmdPtr cmd) =>
        {
            var io = ImGui.GetIO();

            var custom = Application.Engine.ActiveCamera.ViewProjectionMatrix;

            GL.UniformMatrix4(ImGuiController.Shader, false, ref custom);
        });*/
        
        var drawList = ImGui.GetForegroundDrawList();
        var io = ImGui.GetIO();

        Vector2 originSS = Project(camera, initial.Position, viewportSizeInPoints);

        const float gizmoLength = 500f;
        const float baseThickness = 6f;

        Vector3[] worldDirs = { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
        uint[] axisColors = { 0xFF0000FF, 0xFF00FF00, 0xFFFF0000 };
        uint[] hoverColors = { 0xFF8888FF, 0xFF88FF88, 0xFFFF8888 };

        // Viewport rectangle
        Vector2 vpMin = viewportPosition;
        Vector2 vpMax = viewportPosition + viewportSizeInPoints;

        // Precompute endpoints
        Vector2[] endSS = new Vector2[3];
        for (int i = 0; i < 3; i++)
        {
            Vector3 worldEnd = initial.Position + worldDirs[i] * (gizmoLength * 0.01f);
            endSS[i] = Project(camera, worldEnd, viewportSizeInPoints);
        }

        // Mouse in imgui points
        Vector2 scale = DPI.GetDPIScale();
        Vector2 mouseSS = new Vector2(
            rawMousePos.X / scale.X,
            rawMousePos.Y / scale.Y
        );

        // Hover detection
        Axis hoverAxis = Axis.None;
        if (!_dragging && Application.Engine.EditorViewport.IsHovered)
        {
            hoverAxis = PickAxis(originSS, endSS, mouseSS - viewportPosition);
        }

        // Draw each axis
        for (int i = 0; i < 3; i++)
        {
            bool isActive = (_dragging && _activeAxis == (Axis)i);
            bool isHover = (!_dragging && hoverAxis == (Axis)i);

            float thickness = isActive
                ? baseThickness * 1.5f
                : isHover
                    ? baseThickness * 1.2f
                    : baseThickness;

            uint color = isHover ? hoverColors[i] : axisColors[i];

            // Add viewport offset
            Vector2 start = originSS + viewportPosition;
            Vector2 end = endSS[i] + viewportPosition;

            // Clip the line to the viewport rectangle
            if (ClipLineToRect(ref start, ref end, vpMin, vpMax))
            {
                drawList.AddLine(
                    Maths.ToNumericsVector2(start),
                    Maths.ToNumericsVector2(end),
                    color,
                    thickness
                );
            }
        }

        // Input handling
        if (Application.Engine.EditorViewport.IsHovered)
        {
            if (!_dragging && io.MouseDown[0])
            {
                _activeAxis = PickAxis(originSS, endSS, mouseSS - viewportPosition);
                if (_activeAxis != Axis.None)
                {
                    _dragging = true;
                    isGizmoActive = true;
                }
            }
            else if (_dragging && io.MouseReleased[0])
            {
                _dragging = false;
                isGizmoActive = false;
                _activeAxis = Axis.None;
            }
            else if (_dragging && io.MouseDown[0])
            {
                Vector2 md = Maths.FromNumericsVector2(io.MouseDelta);
                float delta = ComputeDragDelta(camera, initial.Position, worldDirs[(int)_activeAxis], md, viewportSizeInPoints);
                initial.Position += worldDirs[(int)_activeAxis] * delta;
            }
        }
    }

    private static Vector2 Project(Camera camera, Vector3 worldPos, Vector2 viewportSize)
    {
        // Transform world-space -> view-space
        Vector4 viewPos = new Vector4(worldPos, 1.0f) * camera.ViewMatrix;

        // Clip against near plane (in view space, camera looks down -Z)
        if (viewPos.Z > -camera.ZNear)
        {
            viewPos.Z = -camera.ZNear;
        }

        // View-space -> clip-space
        Vector4 clip = viewPos * camera.ProjectionMatrix;

        // Perspective divide
        clip /= clip.W;

        // NDC [-1,1] -> screen [0, viewport]
        return new Vector2(
            (clip.X * 0.5f + 0.5f) * viewportSize.X,
            (1f - (clip.Y * 0.5f + 0.5f)) * viewportSize.Y
        );
    }

    

    private static Axis PickAxis(Vector2 origin, Vector2[] ends, Vector2 mouse)
    {
        const float pickRadius = 5.0f;
        Axis best = Axis.None;
        float bestD = pickRadius;
        for (int i = 0; i < 3; i++)
        {
            float d = DistancePointToSegment(mouse, origin, ends[i]);
            if (d < bestD)
            {
                bestD = d;
                best = (Axis)i;
            }
        }
        return best;
    }

    private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab);
        t = Math.Clamp(t, 0f, 1f);
        Vector2 proj = a + ab * t;
        return Vector2.Distance(p, proj);
    }

    private static float ComputeDragDelta(Camera camera, Vector3 origin, Vector3 axis, Vector2 mouseDelta, Vector2 vpSize)
    {
        Vector2 originSS = Project(camera, origin, vpSize);
        Vector2 axisEndSS = Project(camera, origin + axis, vpSize);

        Vector2 screenAxis = axisEndSS - originSS;
        float screenLen = screenAxis.Length;
        if (screenLen < 1e-4f) return 0f;

        Vector2 axisDirSS = screenAxis / screenLen;
        float deltaPoints = Vector2.Dot(mouseDelta, axisDirSS);
        return deltaPoints / screenLen;
    }
}