using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.Systems.SceneSystem;

public class Scene
{
    public List<GameObject> SceneObjects = new List<GameObject>();

    public Scene(List<GameObject> objects)
    {
        SceneObjects = objects;
    }

    public Scene()
    {
        SceneObjects = new List<GameObject>();
    }

    public void AddGameObject(GameObject gameObject)
    {
        SceneObjects.Add(gameObject);
    }
    
    public void RemoveGameObject(GameObject gameObject)
    {
        SceneObjects.Remove(gameObject);
    }
    
    public void Update(float deltaTime)
    {
        foreach (var gameObject in SceneObjects)
        {
            gameObject.Update(deltaTime);
        }
    }
    
    public void Render()
    {
        foreach (var gameObject in SceneObjects)
        {
            gameObject.Render();
        }
    }
    
    public void Clear()
    {
        SceneObjects.Clear();
    }
}