using ImGuiNET;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
using LegendaryRenderer.LegendaryRuntime.Engine.Utilities;
using OpenTK.Mathematics;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor.UserInterface;

public class EditorInspector
{

    public static float WrapAngle(float angle)
    {
        // Wrap angle to [-180, 180)
        angle = ((angle + 180f) % 360f + 360f) % 360f - 180f;
        return angle;
    }

    static bool rotationInputActive = false;

    static Vector3 currentPosition = Vector3.Zero;
    static Vector3 currentScale = Vector3.One; // Usually scale default 1
    static Vector3 displayedEuler = Vector3.Zero; // angles shown in UI (degrees)
    static Quaternion currentRotation = Quaternion.Identity;

// Keep track of last committed Euler angles (degrees) for delta calculation during typing
    static Vector3 lastCommittedEuler = Vector3.Zero;

    static (bool dragChanged, bool typedCommitted) DrawRotationUI(ref Vector3 eulerDegrees)
    {
        bool dragChanged = false;
        bool typedCommitted = false;

        ImGui.Text("Rotation:");
        ImGui.SameLine();

        var axisLabels = new[] { 'X', 'Y', 'Z' };
        var axisColors = new[]
        {
            new System.Numerics.Vector4(1f, 0.2f, 0.2f, 1f),
            new System.Numerics.Vector4(0.2f, 1f, 0.2f, 1f),
            new System.Numerics.Vector4(0.2f, 0.2f, 1f, 1f)
        };

        var labels = new[] { "##Pitch", "##Yaw", "##Roll" };
        float columnWidth = ImGui.GetContentRegionAvail().X / 6.6f;

        float[] components = { eulerDegrees.X, eulerDegrees.Y, eulerDegrees.Z };

        for (int i = 0; i < 3; i++)
        {
            ImGui.PushID(i);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, axisColors[i] * 0.4f);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, axisColors[i] * 0.6f);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, axisColors[i] * 0.8f);
            ImGui.PushStyleColor(ImGuiCol.Text, System.Numerics.Vector4.One);

            ImGui.TextColored(axisColors[i], axisLabels[i].ToString());
            ImGui.SameLine();

            ImGui.SetNextItemWidth(columnWidth);
// Flag to detect user committing text input (pressing Enter or defocusing the widget)
            bool committedThisFrame = false;

            bool changed = ImGui.DragFloat(
                labels[i], ref components[i], 0.5f, -180.0f, 180.0f, "%.1f",
                 ImGuiSliderFlags.NoInput);

            if (changed)
            {
                // If user pressed Enter while typing, changed will be true and
                // ImGui.IsItemDeactivatedAfterEdit() should also be true.
                if (ImGui.IsItemDeactivatedAfterEdit())
                    committedThisFrame = true;
            }

            dragChanged |= changed;
            typedCommitted |= committedThisFrame;
            ImGui.PopStyleColor(4);
            ImGui.PopID();

            if (i < 2) ImGui.SameLine();
        }

        eulerDegrees = new Vector3(components[0], components[1], components[2]);
        return (dragChanged, typedCommitted);
    }

    private static bool isDraggingRotation, isTypingRotation;

    public static void Draw()
    {
        ImGui.Begin("Inspector");

        if (Engine.Engine.SelectedRenderableObjects.Count > 0)
        {
            GameObject gameObject = Engine.Engine.SelectedRenderableObjects[0];
            if (gameObject != null && !rotationInputActive)
            {
                currentPosition = gameObject.Transform.Position;
                currentScale = gameObject.Transform.Scale;
                currentRotation = gameObject.Transform.Rotation;

                // Sync displayed Euler angles from currentRotation only if not currently editing (no drag, no typing)
                if (!isDraggingRotation && !isTypingRotation)
                {
                    Vector3 eulerRad = currentRotation.ToEulerAngles();
                    displayedEuler = eulerRad * (180f / MathF.PI);
                    lastCommittedEuler = displayedEuler;
                }
            }

            if (gameObject != null)
            {
                var pos = Maths.ToNumericsVector3(currentPosition);
                if (ImGui.DragFloat3("Position", ref pos))
                {
                    gameObject.Transform.Position = Maths.FromNumericsVector3(pos);
                    currentPosition = gameObject.Transform.Position;
                }

                ImGui.Text("Rotation (drag to update, type to commit)");

                // Draw rotation UI, get drag/commit flags
                (bool dragChanged, bool typedCommitted) = DrawRotationUI(ref displayedEuler);

                float degToRad = MathF.PI / 180f;

                if (dragChanged)
                {
                    isDraggingRotation = true;
                    isTypingRotation = false;

                    // Incremental delta between displayedEuler and lastCommittedEuler
                    Vector3 deltaEuler = new Vector3(
                        WrapAngle(displayedEuler.X - lastCommittedEuler.X),
                        WrapAngle(displayedEuler.Y - lastCommittedEuler.Y),
                        WrapAngle(displayedEuler.Z - lastCommittedEuler.Z)
                    );

                    Quaternion deltaQuat = Quaternion.FromEulerAngles(
                        deltaEuler.X * degToRad,
                        deltaEuler.Y * degToRad,
                        deltaEuler.Z * degToRad
                    );

                    currentRotation = deltaQuat * currentRotation;
                    gameObject.Transform.Rotation = currentRotation;

                    // Update lastCommittedEuler and sync UI
                    Vector3 newEulerRad = currentRotation.ToEulerAngles();
                    displayedEuler = newEulerRad * (180f / MathF.PI);
                    lastCommittedEuler = displayedEuler;
                }
                else if (typedCommitted)
                {
                    isTypingRotation = true;
                    isDraggingRotation = false;

                    // Apply absolute rotation from typed input after commit
                    Vector3 eulerRadAbsolute = displayedEuler * degToRad;
                    currentRotation =
                        Quaternion.FromEulerAngles(eulerRadAbsolute.X, eulerRadAbsolute.Y, eulerRadAbsolute.Z);
                    gameObject.Transform.Rotation = currentRotation;

                    // Sync UI and last committed
                    Vector3 newEulerRad = currentRotation.ToEulerAngles();
                    displayedEuler = newEulerRad * (180f / MathF.PI);
                    lastCommittedEuler = displayedEuler;

                    // Typing ended after commit
                    isTypingRotation = false;
                }
                else
                {
                    // Neither dragging nor committed typing — user might be typing but not committed.
                    // Don't update rotation or revert UI, just let user continue typing.
                    // Mark flags accordingly:
                    if (ImGui.IsItemActive())
                    {
                        isTypingRotation = true;
                        isDraggingRotation = false;
                    }
                    else
                    {
                        isTypingRotation = false;
                        isDraggingRotation = false;
                    }
                }

                // Scale UI
                if (gameObject is not Camera && gameObject is not Light)
                {
                    var scale = Maths.ToNumericsVector3(currentScale);
                    if (ImGui.DragFloat3("Scale", ref scale))
                    {
                        gameObject.Transform.Scale = Maths.FromNumericsVector3(scale);
                        currentScale = gameObject.Transform.Scale;
                    }
                }
            }

// config
            float minRange = 0.1f;
            float maxRange = 100.0f;
            if (gameObject is Light light)
            {
                
                
                var colour = Maths.ToNumericsVector3(Maths.Color4ToVector3(light.Colour));

                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 3.3f);
                if (ImGui.ColorPicker3("Light Colour", ref colour))
                {
                    light.Colour = Maths.Vector3ToColor4(Maths.FromNumericsVector3(colour));
                }

                var range = light.Range;
                if (ImGui.DragFloat("Light Range", ref range, 1.0f, minRange, maxRange))
                {
                    light.Range = Math.Clamp(range, minRange, maxRange);
                }

                var intensity = light.Intensity;
                if (ImGui.DragFloat("Intensity", ref intensity, 1.0f, 0.5f, 10000.0f))
                {
                    light.Intensity = intensity;
                }

                var bias = light.Bias * 1000000.0f;
                if (ImGui.DragFloat("Bias", ref bias, 1.0f, 0))
                {
                    light.Bias = bias / 1000000.0f;
                }

                var normalBias = light.NormalBias * 1000000.0f;
                if (ImGui.DragFloat("Normal Bias", ref normalBias, 1.0f, 0))
                {
                    light.NormalBias = normalBias / 1000000.0f;
                }

                if (light.Type == Light.LightType.Directional)
                {
                    var cascadeCount = light.CascadeCount;
                    if (ImGui.DragInt("Cascade Count", ref cascadeCount, 1, 1, 4))
                    {
                        light.CascadeCount = Math.Clamp(cascadeCount, 1, 4);
                    }

                    var cascadeSplitFactor = light.CascadeSplitFactor;

                    if (ImGui.DragFloat("Cascade Split Factor", ref cascadeSplitFactor, 0.05f, 0.05f, 1.0f))
                    {
                        light.CascadeSplitFactor = Math.Clamp(cascadeSplitFactor, 0.05f, 1.0f);
                    }
                }

                if (light.Type == Light.LightType.Spot)
                {
                    var minCone = 1.0f;
                    var maxCone = 279.0f;
                    var innerCone = light.InnerCone;
                    var outerCone = light.OuterCone;

                    if (ImGui.DragFloat("Cone Radius", ref innerCone, 0.1f, minCone, maxCone))
                    {
                        innerCone = Math.Clamp(innerCone, minCone, outerCone);
                        light.InnerCone = innerCone;
                    }

                    if (ImGui.DragFloat("Outer Cone Radius", ref outerCone, 0.1f, minCone, maxCone))
                    {
                        outerCone = Math.Clamp(outerCone, innerCone, maxCone);
                        light.OuterCone = outerCone;
                    }
                }
            }

            if (gameObject is Camera camera)
            {
                var fov = camera.FieldOfView;
                if (ImGui.DragFloat("Field of View", ref camera.FieldOfView, 0.25f, 1.0f, 279.0f))
                {
                    camera.FieldOfView = Math.Clamp(fov, 1.0f, 279.0f);
                }

                var znear = camera.ZNear;
                if (ImGui.DragFloat("Near Plane", ref znear, 0.01f, 0.01f, 5.0f))
                {
                    camera.ZNear = Math.Clamp(znear, 0.01f, 5.0f);
                }

                var zfar = camera.ZFar;
                if (ImGui.DragFloat("Far Plane", ref zfar, 10.0f, camera.ZNear + 1, 5000.0f))
                {
                    camera.ZFar = Math.Clamp(zfar, camera.ZNear + 1, 5000.0f);
                }
            }
        }


        ImGui.End();
    }
}