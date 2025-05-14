using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
using OpenTK.Mathematics;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.Systems.SceneSystem;

public class Scene
{
    public List<GameObject> SceneObjects = new List<GameObject>();
    public GameObject RootNode;

    public Scene(ref List<GameObject> objects)
    {
        // Create a root node for the scene
        RootNode = new GameObject(Vector3.Zero, "Scene Root Node", true);
        SceneObjects = objects;
    }

    public void AddGameObject(GameObject gameObject)
    {
        RootNode.Children.Add(gameObject);
    }
    
    public void RemoveGameObject(GameObject gameObject)
    {
        RootNode.Children.Remove(gameObject);
    }
    
    public void Update(float deltaTime)
    {
        RootNode.Update(deltaTime);
    }
    
    public void Render(GameObject.RenderMode mode)
    {
        RootNode.Render(mode);
    }
    
    public void Clear()
    {
        SceneObjects.Clear();
    }
}