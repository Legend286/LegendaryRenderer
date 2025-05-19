using System.Drawing;
using ImGuiNET;
using LegendaryRenderer.LegendaryRuntime.Engine.Editor.UserInterface;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
using LegendaryRenderer.LegendaryRuntime.Engine.Utilities;
using OpenTK.Mathematics;
using Vector2 = System.Numerics.Vector2;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor.Helpers;


public class EditorWorldIconManager
{
    static int numberOfIcons = 0;
    
    public static void ResetCounter()
    {
        numberOfIcons = 0;
    }
    
    static (Vector2, bool) ProjectWorldToScreen(Vector3 worldPosition, Matrix4 viewMatrix, Matrix4 projectionMatrix, Vector2 viewportSize)
    {
        var clipSpacePos = (new Vector4(worldPosition, 1.0f) * (viewMatrix * projectionMatrix));
        clipSpacePos /= clipSpacePos.W;

        var screenPos = new Vector2(
            (clipSpacePos.X + 1.0f) * 0.5f * viewportSize.X,
            ((-clipSpacePos.Y) + 1.0f) * 0.5f * viewportSize.Y
        );

        bool isVisible = clipSpacePos.Z >= 0.0f && clipSpacePos.Z <= 1.0f;
        
        return (screenPos, isVisible);
    }

    public static void Draw(Camera camera, GameObject gameObject, int textureID, float icon_size = 64.0f)
    {
        Vector2 screenPos;
        bool isVisible = false;

        Color4 Colour = Color.White;

        if (gameObject is Light)
        {
            Colour = ((Light)gameObject).Colour;
        }
        
        (screenPos, isVisible) = ProjectWorldToScreen(gameObject.Transform.Position, camera.ViewMatrix, camera.ProjectionMatrix, Engine.Engine.EditorViewport.ViewportSize);

        if (isVisible)
        {
            ImGui.SetCursorScreenPos((screenPos + Engine.Engine.EditorViewport.ViewportPosition) - (Vector2.One * icon_size) * 0.5f); // Center the icon
            ImGui.PushClipRect(Engine.Engine.EditorViewport.Min, Engine.Engine.EditorViewport.Max, true);

            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 32.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 0.0f);

            ImGui.Image(textureID, new Vector2(icon_size), Vector2.One, Vector2.Zero, new System.Numerics.Vector4(Colour.R, Colour.G, Colour.B, 0.75f));
            
            ImGui.PopStyleVar(2);
            if(ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
                Engine.Engine.EditorSceneHierarchyPanel.OnObjectSelected.Invoke(gameObject);
                Console.WriteLine("Selection");
            }
            
            if (ImGui.IsItemHovered())
            {
                Engine.Engine.CanSelect = false;
            }
            else
            {
                Engine.Engine.CanSelect = true;
            }

            ImGui.PopClipRect();
        }
    }
}