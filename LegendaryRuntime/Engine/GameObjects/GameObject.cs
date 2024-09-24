using LegendaryRenderer.Geometry;
using OpenTK.Mathematics;
using LegendaryRenderer.Engine;

namespace LegendaryRenderer.GameObjects;

public class GameObject
{
    public Transform Transform;

    public Guid GUID;
    
    public GameObject Parent;
    
    public List<GameObject> Children;

    public GameObject(Vector3 position)
    {
        GUID = new Guid();

        Parent = Application.Engine.RootObject;
        
        Children = new List<GameObject>();
        
        Transform = new Transform(position);
        
        Application.Engine.AddGameObject(this);
    }

    public virtual void Update(float deltaTime)
    {
        // do update for game object here.
    }

    public virtual void Render()
    {
        // Do Render logic here
    }

    public virtual void Delete()
    {
        Application.Engine.RemoveGameObject(this);
    }

    public void AddChild(GameObject child)
    {
        if (child.Parent == this)
        {
            Console.WriteLine("Cannot add GameObject as a child of itself.");
            return;
        }
        
        Children.Add(child);
        
        child.Parent = this;
    }
}