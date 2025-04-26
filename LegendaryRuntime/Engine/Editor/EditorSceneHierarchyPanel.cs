using ImGuiNET;
using LegendaryRenderer.Application.SceneManagement;
using LegendaryRenderer.GameObjects;
using OpenTK.Graphics.ES30;
using TheLabs.LegendaryRuntime.Engine.GameObjects;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor;

public class EditorSceneHierarchyPanel
{
    private Scene CurrentScene;
    private GameObject SelectedObject;

    public Action<GameObject?>? OnObjectSelected;

    public EditorSceneHierarchyPanel(Scene scene)
    {
        CurrentScene = scene;
    }

    public void Draw()
    {
        ImGui.Begin("Scene Hierarchy");

        if (ImGui.Button("Create Light"))
        {
            var newLight = new Light(Application.Engine.ActiveCamera.Transform.Position, "Light");
            CurrentScene.AddGameObject(newLight);
        }

        ImGui.Separator();

        foreach (var gameObject in CurrentScene.SceneObjects)
        {
            DrawGameObjectNode(gameObject);
        }

        ImGui.End();
    }

    private void DrawGameObjectNode(GameObject gameObject)
    {
        ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (SelectedObject == gameObject)
        {
            flags |= ImGuiTreeNodeFlags.Selected;
        }

        bool opened = ImGui.TreeNodeEx(gameObject.Name, flags);

        if (ImGui.IsItemClicked())
        {
            SelectedObject = gameObject;
            OnObjectSelected?.Invoke(gameObject);
        }

        if (ImGui.BeginPopupContextItem())
        {
            if (ImGui.MenuItem("Delete"))
            {
                CurrentScene.RemoveGameObject(gameObject);
                ImGui.EndPopup();
                return;
            }
            ImGui.EndPopup();
        }

        if (opened)
        {
            foreach (var child in gameObject.Children)
            {
                DrawGameObjectNode(child);
            }

            ImGui.TreePop();
        }
    }

    public GameObject? GetSelectedObject() => SelectedObject;
}