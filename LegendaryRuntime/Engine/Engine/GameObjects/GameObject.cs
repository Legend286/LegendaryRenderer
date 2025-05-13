using LegendaryRenderer.LegendaryRuntime.Engine.Components;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.EngineTypes;
using OpenTK.Mathematics;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;

public class GameObject
{
    public Transform Transform;

    public static int NumGameObjects = -1;

    public Guid GUID;

    public string Name;

    public bool IsVisible = true;

    public GameObject GetRoot()
    {
        return Parent != null ? Parent.GetRoot() : this;
    }
    
    public GameObject? Parent
    {
        get
        {
            if (parent != null)
            {
                return parent;
            }
            else
            {
                return null;
            }
        }
        set
        {
            parent = value;
        }
    }

    private GameObject? parent;

    public List<Component?> Components;
    
    public List<GameObject> Children;

    public GameObject(Vector3 position, string name = "")
    {
        GUID = Guid.NewGuid();

        if (name == "")
        {
            Name = $"Game Object (Entity ID: {++NumGameObjects})";
        }
        else
        {
            Name = name;
        }

//        Parent = Application.Engine.RootObject;
        
        Children = new List<GameObject>();
        
        Transform = new Transform(position, this);
        Transform.LocalPosition = position;
        
        LegendaryRuntime.Engine.Engine.Engine.AddGameObject(this);
    }

    public Component? AddComponent<T>() where T : Component
    {
        var comp = (T)Activator.CreateInstance(typeof(T), new object[] { this, "" })!; 
        Components.Add(comp);

        return comp;
    }

    public Component? GetOrAddComponent<T>(T component) where T : Component
    {
        if (Components.Find(comp => comp?.GetType() == typeof(Component)) == null)
        {
            AddComponent<T>();
        }
        
        return Components.Find(comp => comp?.GetType() == typeof(T));
    }

    public Component? GetComponent<T>() where T : Component
    {
        return Components.Find(comp => comp?.GetType() == typeof(T));
    }
    
    
    public virtual void Update(float deltaTime)
    {
        Transform.Update();
        
        foreach (var child in Children)
        {
            child.Transform.Update();
        }
    }

    public enum RenderMode
    {
        GBuffer,
        SelectionMask,
        Wireframe,
        Default,
        ShadowPass,
        ShadowPassInstanced,
    }
    public virtual void Render(RenderMode mode = RenderMode.Default)
    {
    }

    public virtual void Delete()
    {
        LegendaryRuntime.Engine.Engine.Engine.RemoveGameObject(this);
    }

    public void AddChild(GameObject child)
    {
        if (child.Parent == this)
        {
            Console.WriteLine("Cannot add GameObject as a child of a child of itself.");
            return;
        }
        
        Children.Add(child);
        
        child.Transform.Parent = Transform;
        child.Transform.HasChanged = true;
        child.Parent = this;
        
      //  Console.WriteLine($"Added child {child.Name} to parent {child.Parent?.Name}.");
    }

    public void RemoveChild(GameObject child)
    {
        child.Parent = null;
        child.Transform.Parent = null;
        Children.Remove(child);
    }
}