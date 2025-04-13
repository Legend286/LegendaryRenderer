using LegendaryRenderer.Application;

namespace Geometry
{
    public static class MeshFactory
    {
        private static Dictionary<string, RenderableMesh> loadedMeshes = new Dictionary<string, RenderableMesh>();
        private static int numberOfLoadedMeshes => loadedMeshes.Count;

        public static bool ContainsMesh(string key)
        {
            return loadedMeshes.ContainsKey(key);
        }

        public static bool AddMesh(RenderableMesh renderableMesh, out RenderableMesh loadedMesh, int part = 0)
        {
            string key = $"{renderableMesh.fileName}-{part}";
            if (!ContainsMesh(key))
            {
                loadedMeshes.Add($"{key}", renderableMesh);
                Console.WriteLine($"Added Mesh: '{key}' to the Mesh Factory.");
                loadedMesh = renderableMesh;
                return true;
            }
            else
            {
                Console.WriteLine($"Mesh {renderableMesh.fileName} was already loaded. Copying buffer index to new mesh.");
                loadedMesh = loadedMeshes[key];
                return false;
            }
        }
        public static void RemoveMesh(RenderableMesh renderableMesh)
        {
            loadedMeshes.Remove(renderableMesh.fileName);
        }
    }
}
