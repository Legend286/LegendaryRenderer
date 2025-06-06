using ImGuiNET;
using LegendaryRenderer.Application;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.EngineTypes;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
using LegendaryRenderer.LegendaryRuntime.Engine.Utilities;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;


namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor.Gizmos;

public static class Gizmos
{
    // add up here in Gizmos:
    private static Vector2 _rotateStartDir;         // screen‐space unit vector at drag start
    private static Quaternion _rotationOnDragStart; // the object's rotation at drag start
    public static bool DrawGizmos = true;
    
    private enum Axis
    {
        None = -1,
        X = 0,
        Y = 1,
        Z = 2
    }
    
    public enum GizmoMode
    {
        Translate,
        Rotate,
        Scale,
    }
    

    private static Axis _activeAxis = Axis.None;
    private static bool _dragging = false;
    public static bool isGizmoActive = false;

    private static bool ClipLineToRect(ref Vector2 a, ref Vector2 b, Vector2 min, Vector2 max)
    {
        return true;
    }

    public static void DrawPointLightGizmo(Camera camera, Light light, Vector2 viewportSizeInPoints, Vector2 viewportPosition)
    {
        if (!DrawGizmos)
        {
            return;
        }
        var drawList = ImGui.GetForegroundDrawList();

        // 1) Project the light’s center into screen space
        Vector2 centerSS = Project(camera, light.Transform.Position, viewportSizeInPoints);

        // 2) Prepare viewport clamp
        Vector2 vpMin = viewportPosition;
        Vector2 vpMax = viewportPosition + viewportSizeInPoints;

        // 3) Circle parameters
        const int numSegments = 16;
        float radius = light.Range;
        uint colour = Maths.Color4ToUint(light.Colour);
        float thickness = 1.5f;

        // 4) Define the three circle‐planes
        var planes = new (Vector3 axisA, Vector3 axisB)[]
        {
            // XY plane
            (Vector3.UnitX, Vector3.UnitY),
            // XZ plane
            (Vector3.UnitX, Vector3.UnitZ),
            // YZ plane
            (Vector3.UnitY, Vector3.UnitZ),
        };

        // clip & draw
        drawList.PushClipRect(
            new System.Numerics.Vector2(vpMin.X, vpMin.Y),
            new System.Numerics.Vector2(vpMax.X, vpMax.Y),
            true
        );
        // 5) For each plane, draw a circle around the light origin
        foreach (var (axisA, axisB) in planes)
        {
            for (int i = 0; i < numSegments; i++)
            {
                float t0 = MathF.PI * 2f * (i / (float)numSegments);
                float t1 = MathF.PI * 2f * ((i + 1) / (float)numSegments);

                // compute offsets in world‐space
                Vector3 offset0 = (axisA * MathF.Cos(t0) + axisB * MathF.Sin(t0)) * radius;
                Vector3 offset1 = (axisA * MathF.Cos(t1) + axisB * MathF.Sin(t1)) * radius;

                // world‐space positions along the circle
                Vector3 world0 = light.Transform.Position + offset0;
                Vector3 world1 = light.Transform.Position + offset1;

                // project into screen‐space
                Vector2 screen0 = Vector2.Zero;
                Vector2 screen1 = Vector2.Zero;

                if (ClipLineAgainstNearPlane(camera, ref world0, ref world1))
                {
                    screen0 = Project(camera, world0, viewportSizeInPoints) + viewportPosition;
                    screen1 = Project(camera, world1, viewportSizeInPoints) + viewportPosition;

                    drawList.AddLine(
                        new System.Numerics.Vector2(screen0.X, screen0.Y),
                        new System.Numerics.Vector2(screen1.X, screen1.Y),
                        colour,
                        thickness
                    );
                }
            }
        }
        drawList.PopClipRect();
    }

    public static void DrawSpotLightCone(Camera camera, Light light, Vector2 viewportSizeInPoints, Vector2 viewportPosition)
    {
        if (!DrawGizmos)
        {
            return;
        }
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

        // clip & draw
        drawList.PushClipRect(
            new System.Numerics.Vector2(vpMin.X, vpMin.Y),
            new System.Numerics.Vector2(vpMax.X, vpMax.Y),
            true
        );
        for (int i = 0; i < numSegments; i++)
        {
            float angle0 = MathF.PI * 2f * (i / (float)numSegments);
            float angle1 = MathF.PI * 2f * ((i + 1) / (float)numSegments);

            Vector3 offset0 = (right * MathF.Cos(angle0) + up * MathF.Sin(angle0)) * radius;
            Vector3 offset1 = (right * MathF.Cos(angle1) + up * MathF.Sin(angle1)) * radius;

            Vector3 worldPos0 = light.Transform.Position + light.Transform.Forward * light.Range + offset0;
            Vector3 worldPos1 = light.Transform.Position + light.Transform.Forward * light.Range + offset1;

            if (ClipLineAgainstNearPlane(camera, ref worldPos0, ref worldPos1))
            {

                Vector2 screen0 = Project(camera, worldPos0, viewportSizeInPoints);
                Vector2 screen1 = Project(camera, worldPos1, viewportSizeInPoints);

                // Offset by viewport top-left position
                screen0 += viewportPosition;
                screen1 += viewportPosition;

                uint colour = Maths.Color4ToUint(light.Colour);

                // draw base circle
                drawList.AddLine(
                    new System.Numerics.Vector2(screen0.X, screen0.Y),
                    new System.Numerics.Vector2(screen1.X, screen1.Y),
                    colour, // semi-transparent white
                    1.5f
                );


            }

            // draw lines from tip to base

            worldPos0 = light.Transform.Position + light.Transform.Forward * light.Range + offset0;

            var lightOrigin = light.Transform.Position;
            if (ClipLineAgainstNearPlane(camera, ref lightOrigin, ref worldPos0))
            {

                Vector2 screen0 = Project(camera, worldPos0, viewportSizeInPoints);
                Vector2 screen1 = Project(camera, worldPos1, viewportSizeInPoints);

                // Offset by viewport top-left position
                screen0 += viewportPosition;
                screen1 += viewportPosition;

                originSS = Project(camera, lightOrigin, viewportSizeInPoints);
                screen0 = Project(camera, worldPos0, viewportSizeInPoints);

                Vector2 screenOrigin = new Vector2(originSS.X + viewportPosition.X, originSS.Y + viewportPosition.Y);

                // Offset by viewport top-left position
                screen0 += viewportPosition;


                uint colour = Maths.Color4ToUint(light.Colour);

                drawList.AddLine(
                    new System.Numerics.Vector2(screen0.X, screen0.Y),
                    new System.Numerics.Vector2(screenOrigin.X, screenOrigin.Y),
                    colour,
                    1.0f
                );
            }

        }
        drawList.PopClipRect();
    }



    public static void DrawAndHandle(ref Transform initial, ref readonly Camera camera, ref readonly Vector2 rawMousePos, ref readonly Vector2 viewportSizeInPoints, ref readonly Vector2 viewportPosition, GizmoMode mode)
    {
        var drawList = ImGui.GetForegroundDrawList();
        var io = ImGui.GetIO();

        Vector3 last = initial.Position;
        // Common data
        Vector2 originSS = Project(camera, initial.Position, viewportSizeInPoints);
        Vector2 vpMin = viewportPosition;
        Vector2 vpMax = viewportPosition + viewportSizeInPoints;
        Vector2 mouseSS = new Vector2(rawMousePos.X, rawMousePos.Y);

        // Compute a world‐space scale so the gizmo stays ~250px tall
        const float desiredPix = 250f;
        float fovY = MathHelper.DegreesToRadians(camera.FieldOfView);
        float dist = (camera.Transform.Position - initial.Position).Length;
        dist = MathF.Max(dist, camera.ZNear + 0.01f);
        float worldUnitsPerPx = 2f * dist * MathF.Tan(fovY * 0.5f) / viewportSizeInPoints.Y;
        float worldScale = worldUnitsPerPx * desiredPix;
        float gizmoLen = worldScale;

        Vector3[] worldDirs = { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
        uint[] axisCols = { 0xFF0000FF, 0xFF00FF00, 0xFFFF0000 };
        uint[] hoverCols = { 0xFF8888FF, 0xFF88FF88, 0xFFFF8888 };

        // clip & draw
        drawList.PushClipRect(
            new System.Numerics.Vector2(vpMin.X, vpMin.Y),
            new System.Numerics.Vector2(vpMax.X, vpMax.Y),
            true
        );
        switch (mode)
        {
            case GizmoMode.Translate:
            {
                const float baseTh = 6f;
                Vector2[] endSS = new Vector2[3];
                var initialPos = initial.Position;
          

                Axis hoverAxis = Axis.None;
                if (!_dragging && Engine.Engine.EditorViewport.IsHovered)
                    hoverAxis = PickAxis(originSS, endSS, mouseSS - viewportPosition);

                for (int i = 0; i < 3; i++)
                {
                    bool isActive = (_dragging && _activeAxis == (Axis)i);
                    bool isHover = (!_dragging && hoverAxis == (Axis)i);
                    float th = isActive ? baseTh * 1.5f : isHover ? baseTh * 1.2f : baseTh;
                    uint col = isHover ? hoverCols[i] : axisCols[i];

                    var endPos = initial.Position + worldDirs[i] * gizmoLen;
                    if (ClipLineAgainstNearPlane(camera, ref initialPos, ref endPos))
                    {
                        originSS = Project(camera, initialPos, viewportSizeInPoints);
                        endSS[i] = Project(camera, endPos, viewportSizeInPoints);

                        Vector2 p0 = originSS + viewportPosition;
                        Vector2 p1 = endSS[i] + viewportPosition;
                        drawList.AddLine(
                            Maths.ToNumericsVector2(p0),
                            Maths.ToNumericsVector2(p1),
                            col, th
                        );
                    }
                }
                if (Engine.Engine.EditorViewport.IsHovered)
                {
                    if (!_dragging && io.MouseDown[0])
                    {
                        _activeAxis = PickAxis(originSS, endSS, mouseSS - viewportPosition);
                        if (_activeAxis != Axis.None)
                        {
                            (_dragging, isGizmoActive) = (true, true);
                            Engine.Engine.CanSelect = false;
                        }
                    }
                    else if (_dragging && io.MouseReleased[0])
                    {
                        (_dragging, isGizmoActive, _activeAxis) = (false, false, Axis.None);
                        Engine.Engine.CanSelect = true;
                    }
                    else if (_dragging && io.MouseDown[0])
                    {
                        Vector2 md = Maths.FromNumericsVector2(io.MouseDelta);
                        float delta = ComputeDragDelta(
                            camera, initial.Position,
                            worldDirs[(int)_activeAxis],
                            md, viewportSizeInPoints
                        );
                        initial.Position += worldDirs[(int)_activeAxis] * delta;
                    }
                }
            }
                break;

            case GizmoMode.Rotate:
            {
                const int numSegs = 64;
                float radius = gizmoLen;
                Axis hoverA = Axis.None;

                Vector3[][] circleAxes =
                {
                    new[] { Vector3.UnitY, Vector3.UnitZ },
                    new[] { Vector3.UnitZ, Vector3.UnitX },
                    new[] { Vector3.UnitX, Vector3.UnitY },
                };

                // 1) Draw the three base rings
                for (int axis = 0; axis < 3; axis++)
                {
                    uint col = axisCols[axis];
                    for (int s = 0; s < numSegs; s++)
                    {
                        float t0 = (MathF.PI * 2) * (s / (float)numSegs);
                        float t1 = (MathF.PI * 2) * ((s + 1) / (float)numSegs);
                        var a0 = circleAxes[axis][0] * MathF.Cos(t0) + circleAxes[axis][1] * MathF.Sin(t0);
                        var a1 = circleAxes[axis][0] * MathF.Cos(t1) + circleAxes[axis][1] * MathF.Sin(t1);

                        Vector3 w0 = initial.Position + a0 * radius;
                        Vector3 w1 = initial.Position + a1 * radius;

                        if (ClipLineAgainstNearPlane(camera, ref w0, ref w1))
                        {
                            Vector2 p0 = Project(camera, w0, viewportSizeInPoints) + viewportPosition;
                            Vector2 p1 = Project(camera, w1, viewportSizeInPoints) + viewportPosition;

                            drawList.AddLine(
                                Maths.ToNumericsVector2(p0),
                                Maths.ToNumericsVector2(p1),
                                col, 3f
                            );
                        }
                    }
                }

                // 2) Hover detect on those rings
                if (!_dragging && Engine.Engine.EditorViewport.IsHovered)
                    hoverA = PickRingAxis(
                        originSS, mouseSS,
                        camera, initial.Position,
                        radius,
                        viewportSizeInPoints,
                        viewportPosition
                    );

                // 3) Highlight hovered ring
                if (hoverA != Axis.None)
                {
                    uint hc = hoverCols[(int)hoverA];
                    for (int s = 0; s < numSegs; s++)
                    {
                        float t0 = (MathF.PI * 2) * (s / (float)numSegs);
                        float t1 = (MathF.PI * 2) * ((s + 1) / (float)numSegs);
                        var a0 = circleAxes[(int)hoverA][0] * MathF.Cos(t0)
                                 + circleAxes[(int)hoverA][1] * MathF.Sin(t0);
                        var a1 = circleAxes[(int)hoverA][0] * MathF.Cos(t1)
                                 + circleAxes[(int)hoverA][1] * MathF.Sin(t1);

                        Vector3 w0 = initial.Position + a0 * radius;
                        Vector3 w1 = initial.Position + a1 * radius;
                        if (ClipLineAgainstNearPlane(camera, ref w0, ref w1))
                        {

                            Vector2 p0 = Project(camera, w0, viewportSizeInPoints) + viewportPosition;
                            Vector2 p1 = Project(camera, w1, viewportSizeInPoints) + viewportPosition;


                            drawList.AddLine(
                                Maths.ToNumericsVector2(p0),
                                Maths.ToNumericsVector2(p1),
                                hc, 3f * 1.2f
                            );
                        }
                    }
                }

                // 4) Handle input + draw pie-slice
                Vector2 centerAbs = originSS + viewportPosition;
                if (Engine.Engine.EditorViewport.IsHovered)
                {
                    if (!_dragging && io.MouseDown[0] && hoverA != Axis.None)
                    {
                        _dragging = true;
                        isGizmoActive = true;
                        _activeAxis = hoverA;
                        _rotateStartDir = Vector2.Normalize(mouseSS - centerAbs);
                        _rotationOnDragStart = initial.Rotation;
                    }
                    else if (_dragging && io.MouseReleased[0])
                    {
                        _dragging = false;
                        isGizmoActive = false;
                        _activeAxis = Axis.None;
                    }
                    else if (_dragging && io.MouseDown[0])
                    {
                        var axis = circleAxes[(int)_activeAxis];

                        var planeAxis = Vector3.Normalize(Vector3.Cross(axis[0], axis[1]));

                        var dir = Vector3.Normalize(camera.Transform.Position  - initial.Position);

                        var sign = Vector3.Dot(dir, planeAxis) > 0 ? -1.0f : 1.0f;
                        
                        
                        Vector2 curDir = Vector2.Normalize(mouseSS - centerAbs);
                        float cross = _rotateStartDir.X * curDir.Y
                                      - _rotateStartDir.Y * curDir.X;
                        float dot = Vector2.Dot(_rotateStartDir, curDir);
                        float dAng = MathF.Atan2(cross * sign, dot);

                        // apply rotation
                        var q = Quaternion.FromAxisAngle(
                            new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ }[(int)_activeAxis],
                            dAng
                        );
                        initial.Rotation = q * _rotationOnDragStart;

                        // Normalize sweep angle to [0, 2π)
                        float sweep = dAng < 0 ? dAng + ((float)Math.PI * 2) : dAng;

                        float startAngle = MathF.Atan2(_rotateStartDir.Y, _rotateStartDir.X);
                        float endAngle = startAngle + sweep;
                        
// Dynamically calculate segment count, but base arc strictly from start to end
                        float segmentDensity = 512f; // segments for full circle
                        int segs = Math.Clamp((int)(sweep / (2 * MathF.PI) * segmentDensity), 3, 512);

                        var plane = circleAxes[(int)_activeAxis];
                        
                        var poly = new List<System.Numerics.Vector2>();
                        poly.Add(new System.Numerics.Vector2(centerAbs.X, centerAbs.Y));

// Build arc from startAngle to endAngle with stable sampling
                        for (int i = 0; i <= segs; i++)
                        {
                            float t = MathHelper.Lerp(startAngle, endAngle, i / (float)segs);

                            Vector3 w = initial.Position + (plane[0] * MathF.Cos(t) + plane[1] * MathF.Sin(t)) * worldScale;
                            if (ClipLineAgainstNearPlane(camera, ref w, ref last))
                            {
                                Vector2 ss = Project(camera, w, viewportSizeInPoints) + viewportPosition;
                                poly.Add(new System.Numerics.Vector2(ss.X, ss.Y));
                            }
                        }
                        
                        uint baseC = hoverCols[(int)_activeAxis];
                        uint fillC = (baseC & 0x00FFFFFF) | 0x44000000;
                        var arr = poly.ToArray();

                      
                            drawList.AddConvexPolyFilled(ref arr[0], arr.Length, fillC);
                            drawList.AddPolyline(ref arr[0], arr.Length, baseC, ImDrawFlags.Closed,2f);
                            

                        // angle label
                        string txt = $"{MathHelper.RadiansToDegrees(MathF.Abs(dAng)):0.0}°";
                        var pos = new System.Numerics.Vector2(
                            centerAbs.X + (Project(camera, initial.Position + plane[1] * worldScale, viewportSizeInPoints) + viewportPosition - centerAbs).Length + 8f,
                            centerAbs.Y
                        );
                        drawList.AddText(pos, baseC, txt);
                    }
                }
                
            }
                break;

            case GizmoMode.Scale:
            {
                // …your scale code…
            }
                break;
        }
        drawList.PopClipRect();
    }

    private static Axis PickRingAxis(
        Vector2 originSS,
        Vector2 mouseSS,
        Camera camera,
        Vector3 origin,
        float radius,
        Vector2 vpSize,
        Vector2 vpPos
    )
    {
        const int   numSegments   = 64;
        const float pickThreshold = 6f;
        Axis        bestAxis      = Axis.None;
        float       bestDistance  = pickThreshold;

        // absolute centre in screen‐space (including viewport offset)
        Vector2 centerAbs = originSS + vpPos;

        // Each axis has its own “in‐plane” basis vectors
        Vector3[][] planeAxes = {
            new[]{ Vector3.UnitY, Vector3.UnitZ }, // circle around X
            new[]{ Vector3.UnitZ, Vector3.UnitX }, // circle around Y
            new[]{ Vector3.UnitX, Vector3.UnitY }, // circle around Z
        };

        // For each axis, walk the circle in small segments
        for (int axis = 0; axis < 3; axis++)
        {
            var aA = planeAxes[axis][0];
            var aB = planeAxes[axis][1];

            // Build and test each segment
            for (int s = 0; s < numSegments; s++)
            {
                float t0 = (MathF.PI*2) * (s       / (float)numSegments);
                float t1 = (MathF.PI*2) * ((s + 1) / (float)numSegments);

                Vector3 w0 = origin + (aA * MathF.Cos(t0) + aB * MathF.Sin(t0)) * radius;
                Vector3 w1 = origin + (aA * MathF.Cos(t1) + aB * MathF.Sin(t1)) * radius;

                Vector2 p0 = Project(camera, w0, vpSize) + vpPos;
                Vector2 p1 = Project(camera, w1, vpSize) + vpPos;

                // how close is the mouse to this segment?
                float d = DistancePointToSegment(mouseSS, p0, p1);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    bestAxis     = (Axis)axis;
                }
            }
        }

        return bestAxis;
    }
    /// <summary>
    /// Estimate how many radians the mouse moved around the circle tangent.
    /// </summary>
    private static float ComputeRotationDelta(
        Camera camera,
        Vector3 origin,
        Vector3 axis,
        Vector2 mouseDelta,
        Vector2 vpSize
    )
    {
        // project origin and one axis endpoint
        Vector2 cs  = Project(camera, origin,      vpSize);
        Vector2 es  = Project(camera, origin+axis, vpSize);
        Vector2 dir = (es - cs);
        float   len = dir.Length;
        if (len < 1e-5f) return 0f;
        Vector2 norm      = dir / len;
        Vector2 tangent   = new Vector2(-norm.Y, norm.X);
        float   deltaProj = Vector2.Dot(mouseDelta, tangent);
        // scale so that moving one circle‐radius in screen‐space == 1 radian
        return deltaProj / len;
    }

    public static void DrawInfiniteGrid(Vector2 viewportMin, Vector2 viewportMax, Camera camera, float baseGridSize)
    {
        var drawList = ImGui.GetWindowDrawList();
        var viewportSize = viewportMax - viewportMin;

        if (camera.Transform == null) return;

        // Use System.Numerics for matrix calculations
        // Assuming Maths.ToNumericsMatrix4x4 correctly converts OpenTK.Mathematics.Matrix4 to System.Numerics.Matrix4x4
        System.Numerics.Matrix4x4 viewMatrixNumerics = Maths.ToNumericsMatrix4x4(camera.ViewMatrix);
        // Use GetProjectionMatrix to ensure it's for the current viewport aspect ratio
        System.Numerics.Matrix4x4 projectionMatrixNumerics = Maths.ToNumericsMatrix4x4(camera.ProjectionMatrix);
        
        uint gridColorU = ImGui.GetColorU32(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 0.4f));
        uint thickerLineColorU = ImGui.GetColorU32(new System.Numerics.Vector4(0.6f, 0.6f, 0.6f, 0.6f));
        uint xAxisColorU = ImGui.GetColorU32(new System.Numerics.Vector4(0.8f, 0.2f, 0.2f, 0.7f)); // Red for X
        uint zAxisColorU = ImGui.GetColorU32(new System.Numerics.Vector4(0.2f, 0.2f, 0.8f, 0.7f)); // Blue for Z

        System.Numerics.Vector3 cameraPositionNumerics = Maths.ToNumericsVector3(camera.Transform.Position);
        float cameraHeight = cameraPositionNumerics.Y;

        // Dynamic grid size based on camera height.
        float dynamicGridSpacing = baseGridSize;
       
        dynamicGridSpacing = MathF.Max(0.05f, dynamicGridSpacing); // Ensure a minimum spacing

        System.Numerics.Vector3 gridPatchCenter = new System.Numerics.Vector3(cameraPositionNumerics.X, 0, cameraPositionNumerics.Z);

        const int linesPerSide = 250; // Draw 50 lines from the center in each direction.
        float drawExtent = linesPerSide;
        var lines = new List<(System.Numerics.Vector3 start, System.Numerics.Vector3 end, uint color)>();

        // Lines parallel to Z-axis (X lines)
        float startX = (float)Math.Floor((gridPatchCenter.X - drawExtent) / dynamicGridSpacing) * dynamicGridSpacing;
        float endX = (float)Math.Ceiling((gridPatchCenter.X + drawExtent) / dynamicGridSpacing) * dynamicGridSpacing;

        for (float x = startX; x <= endX; x += dynamicGridSpacing)
        {
            if (dynamicGridSpacing < 0.001f) break; // Safety break if spacing becomes too small
            uint color = gridColorU;
            float xRelative = x / dynamicGridSpacing;
            if (Math.Abs(xRelative - MathF.Round(xRelative / 10.0f) * 10.0f) < 0.1f)
                color = thickerLineColorU;
            if (Math.Abs(x) < dynamicGridSpacing * 0.5f) // Check if 'x' is close to 0 for Z-axis
                color = zAxisColorU;

            lines.Add((new System.Numerics.Vector3(x, 0, gridPatchCenter.Z - drawExtent),
                new System.Numerics.Vector3(x, 0, gridPatchCenter.Z + drawExtent),
                color));
        }

        // Lines parallel to X-axis (Z lines)
        float startZ = (float)Math.Floor((gridPatchCenter.Z - drawExtent) / dynamicGridSpacing) * dynamicGridSpacing;
        float endZ = (float)Math.Ceiling((gridPatchCenter.Z + drawExtent) / dynamicGridSpacing) * dynamicGridSpacing;

        for (float z = startZ; z <= endZ; z += dynamicGridSpacing)
        {
            if (dynamicGridSpacing < 0.001f) break; // Safety break
            uint color = gridColorU;
            float zRelative = z / dynamicGridSpacing;
            if (Math.Abs(zRelative - MathF.Round(zRelative / 10.0f) * 10.0f) < 0.1f)
                color = thickerLineColorU;
            if (Math.Abs(z) < dynamicGridSpacing * 0.5f) // Check if 'z' is close to 0 for X-axis
                color = xAxisColorU;

            lines.Add((new System.Numerics.Vector3(gridPatchCenter.X - drawExtent, 0, z),
                new System.Numerics.Vector3(gridPatchCenter.X + drawExtent, 0, z),
                color));
        }

        foreach (var line in lines)
        {
            var start = Maths.FromNumericsVector3(line.start);
            var end = Maths.FromNumericsVector3(line.end);
            
            if (ClipLineAgainstNearPlane(camera, ref start, ref end))
            {
                var screenP1 = Maths.ToNumericsVector2(Project(camera, start, viewportSize)) + Engine.Engine.EditorViewport.ViewportPosition;
                var screenP2 = Maths.ToNumericsVector2(Project(camera, end, viewportSize)) + Engine.Engine.EditorViewport.ViewportPosition;
                drawList.AddLine(screenP1, screenP2, line.color, 1.0f);
            }
        }
    }

    public static bool ClipLineAgainstNearPlane(Camera camera, ref Vector3 a, ref Vector3 b)
    {
        float nearZ = -camera.ZNear; // view space near plane is at -ZNear

        // Transform to view space
        Vector3 viewA = (new Vector4(a, 1.0f) * camera.ViewMatrix).Xyz;
        Vector3 viewB = (new Vector4(b, 1.0f) * camera.ViewMatrix).Xyz;

        bool aInside = viewA.Z <= nearZ;
        bool bInside = viewB.Z <= nearZ;

        // Fully outside (both behind)
        if (!aInside && !bInside)
            return false;

        // If only one point is outside, clip it
        if (aInside && !bInside)
        {
            float t = (nearZ - viewA.Z) / (viewB.Z - viewA.Z);
            Vector3 viewClip = Vector3.Lerp(viewA, viewB, t);
            viewB = viewClip;
            b = (new Vector4(viewB, 1.0f) * camera.ViewMatrix.Inverted()).Xyz;
        }
        else if (!aInside && bInside)
        {
            float t = (nearZ - viewB.Z) / (viewA.Z - viewB.Z);
            Vector3 viewClip = Vector3.Lerp(viewB, viewA, t);
            viewA = viewClip;
            a = (new Vector4(viewA, 1.0f) * camera.ViewMatrix.Inverted()).Xyz;
        }

        return true;
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