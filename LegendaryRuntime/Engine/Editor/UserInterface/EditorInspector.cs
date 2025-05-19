using ImGuiNET;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
using LegendaryRenderer.LegendaryRuntime.Engine.Utilities;
using OpenTK.Mathematics;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor.UserInterface;

public class EditorInspector
{
    public static float WrapAngle(float angle)
    { 
        angle = ((angle + 180f) % 360f + 360f) % 360f - 180f;

        return angle;
    }
    public static void Draw()
    {
        var currentPosition = Maths.ToNumericsVector3(Vector3.Zero);
        var currentEulerRotation = Maths.ToNumericsVector3(Vector3.Zero);
        var currentScale = Maths.ToNumericsVector3(Vector3.Zero);
        
        ImGui.Begin("Inspector");
        if (Engine.Engine.SelectedRenderableObjects.Count > 0)
        {

            GameObject gameObject = Engine.Engine.SelectedRenderableObjects[0];
            if (gameObject != null)
            {
                currentPosition = Maths.ToNumericsVector3(gameObject.Transform.Position);
                currentEulerRotation = Maths.ToNumericsVector3(gameObject.Transform.EulerAngles);
                currentScale = Maths.ToNumericsVector3(gameObject.Transform.Scale);

            }


            if (gameObject != null)
            {
                if (ImGui.DragFloat3("Position", ref currentPosition))
                {
                    gameObject.Transform.Position = Maths.FromNumericsVector3(currentPosition);

                }
                if (ImGui.DragFloat3("Rotation", ref currentEulerRotation))
                {
                    currentEulerRotation.X = WrapAngle(currentEulerRotation.X);
                    currentEulerRotation.Y = WrapAngle(currentEulerRotation.Y);
                    currentEulerRotation.Z = WrapAngle(currentEulerRotation.Z);

                    gameObject.Transform.EulerAngles = Maths.FromNumericsVector3(currentEulerRotation);

                }

                if (gameObject is not Camera && gameObject is not Light)
                {
                    if (ImGui.DragFloat3("Scale", ref currentScale))
                    {
                        gameObject.Transform.Scale = Maths.FromNumericsVector3(currentScale);

                    }
                }

                // config
                float minRange = 0.1f;
                float maxRange = 100.0f;
                if (gameObject is Light light)
                {
                    var colour = Maths.ToNumericsVector3(Maths.Color4ToVector3(light.Colour));
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
        }
        ImGui.End();
    }
}