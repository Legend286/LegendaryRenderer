using LegendaryRenderer.GameObjects;

namespace LegendaryRenderer.Components;


public class Component
{
    public static int ComponentID = -1;
    public string Name { get; set; }
    public GameObject Parent { get; private set; }

    private Component(GameObject parent)
    {
        Initialize(parent);
    }
    
    public virtual void Initialize(GameObject parent, string name = "")
    {
        Parent = parent;
        ComponentID++;

        if (name == String.Empty)
        {
            Name = $"{nameof(Component)} - (Component ID: {ComponentID})";
        }
        else
        {
            Name = $"{name} - (Component ID: {ComponentID})";
        }
    }
    
    public void Update(double deltaTime)
    {
        // do component update logic
    }
    
    public void Render()
    {
        // do component render logic
    }
    
    public void RemoveFromParent(Component? component)
    {
        Parent.Components.Remove(component);
    }
}


