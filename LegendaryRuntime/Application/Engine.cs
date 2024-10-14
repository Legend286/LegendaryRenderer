using Geometry;
using LegendaryRenderer.GameObjects;
using LegendaryRenderer.Geometry;
using LegendaryRenderer.Shaders;
using OpenTK.Mathematics;

namespace LegendaryRenderer.Application;

public static class Engine
{
    public static List<GameObject> GameObjects = new List<GameObject>();

    public static Camera ActiveCamera;
    public static GameObject RootObject { get; private set; }

    public static ShaderFile currentShader;

    public static int TriangleCountRendered, TriangleCountCulled, TriangleCountTotal = 0;

    static Engine()
    {
        RootObject = new GameObject(Vector3.Zero);

        ShaderManager.LoadShader("basepass", out ShaderFile loaded);
        currentShader = loaded;
         
    }
    
    public static void Update(float deltaTime)
    {
        currentShader.SetShaderMatrix4x4("viewProjection", ActiveCamera.viewProjectionMatrix);

        foreach (GameObject go in GameObjects)
        {
            go.Update(deltaTime);
        }
        // do update logic here for entire engine
    }

    public static void AddGameObject(GameObject gameObject)
    {
        if (gameObject is Camera camera)
        {
            ActiveCamera = camera;
        }
        
        GameObjects.Add(gameObject);
    }

    public static void RemoveGameObject(GameObject gameObject)
    {
        GameObjects.Remove(gameObject);
    }

    public static void Render()
    {
        foreach (GameObject go in GameObjects)
        {
            go.Render();
        }
    }
}