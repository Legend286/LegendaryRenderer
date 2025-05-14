using ImGuiNET;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.Systems.SceneSystem;
using OpenTK.Mathematics;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor.UserInterface;

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

        DrawGameObjectNode(Engine.Engine.LoadedScenes[0].RootNode);
        
        foreach (var go in MarkedForDeletion)
        {
            CurrentScene.RemoveGameObject(go);
        }
        ImGui.End();
    }
    
    List<GameObject> MarkedForDeletion = new List<GameObject>();

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

        // 👇👇👇 FIXED: Popup per *this* GameObject, not the whole scene!
        if (ImGui.BeginPopupContextItem())
        {
            if (ImGui.MenuItem("Delete"))
            {
                // 👇 Mark THIS object for deletion
                MarkedForDeletion.Add(gameObject);
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
    
    
}