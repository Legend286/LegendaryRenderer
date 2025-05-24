using ImGuiNET;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.Systems.SceneSystem;
using OpenTK.Mathematics;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor.UserInterface;

public class EditorSceneHierarchyPanel
{
    private Scene CurrentScene;
    public GameObject Selection;

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
            foreach (GameObject child in go.Children)
            {
                Engine.Engine.RemoveGameObject(child);
                CurrentScene.RemoveGameObject(child);
                if (child is RenderableMesh mesh)
                {
                    Engine.Engine.RenderableMeshes.Remove(mesh);
                }
            }
            Engine.Engine.GameObjects.Remove(go);
            CurrentScene.RemoveGameObject(go);
        }
        ImGui.End();
    }
    
    List<GameObject> MarkedForDeletion = new List<GameObject>();

    private void DrawGameObjectNode(GameObject gameObject)
    {
        ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (Selection == gameObject)
        {
            flags |= ImGuiTreeNodeFlags.Selected;
        }

        bool opened = ImGui.TreeNodeEx(gameObject.Name, flags);

        if (ImGui.IsItemClicked())
        {
            Selection = gameObject;
            OnObjectSelected?.Invoke(gameObject);
        }
        
        if (ImGui.BeginPopupContextItem())
        {
            if (gameObject is Camera camera)
            {
                if (ImGui.MenuItem("Set Active Camera"))
                {
                    Engine.Engine.ActiveCamera = camera;
                }
            }
            
            if (ImGui.MenuItem("Delete"))
            {
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