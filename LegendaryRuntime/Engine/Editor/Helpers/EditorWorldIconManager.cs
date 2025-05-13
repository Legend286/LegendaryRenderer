using ImGuiNET;
using LegendaryRenderer.LegendaryRuntime.Engine.Editor.UserInterface;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
using LegendaryRenderer.LegendaryRuntime.Engine.Utilities;
using OpenTK.Mathematics;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor.Helpers;

public class EditorWorldIconManager
{
    static Vector2 ProjectWorldToScreen(Vector3 worldPosition, Matrix4 viewMatrix, Matrix4 projectionMatrix, Vector2 viewportSize)
    {
        var clipSpacePos = (new Vector4(worldPosition, 1.0f) * ( viewMatrix * projectionMatrix));
        clipSpacePos /= clipSpacePos.W;
        
        var screenPos = new Vector2(
            (clipSpacePos.X + 1.0f) * 0.5f * viewportSize.X,
            ((-clipSpacePos.Y) + 1.0f) * 0.5f * viewportSize.Y
        );

        return screenPos;
    }
    
    public void Draw(Camera camera, GameObject gameObject, int textureID)
    {
        // In your rendering loop, after rendering the 3D scene
// and before ImGuiController.Render()
        var io = ImGui.GetIO();

        ImGui.Begin("EditorIconsOverlay", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav); // A full-screen, transparent, non-interactive window
        ImGui.SetWindowPos(Vector2.Zero);
        ImGui.SetWindowSize(io.DisplaySize);
        
            Vector2 screenPos;
            bool isVisible;

            // Project iconData.WorldPosition to screenPos
            // Set isVisible based on whether it's in front of camera and within frustum

            screenPos = ProjectWorldToScreen(Maths.To, camera.ViewMatrix, camera.ProjectionMatrix, Engine.Engine.EditorViewport.ViewportSize);

            ImGui.SetCursorScreenPos(screenPos - iconData.ScreenSizePixels * 0.5f); // Center the icon
            ImGui.Image(iconData.TextureId, iconData.ScreenSizePixels);
            
            if (ImGui.IsItemHovered())
            { 
                ImGui.SetTooltip($"Icon at {iconData.WorldPosition}");
            }

                // }
            }
        ImGui.End();
    }
}