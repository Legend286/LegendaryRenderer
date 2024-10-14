using LegendaryRenderer.Application;

namespace Geometry
{
    public static class MeshFactory
    {
        private static Dictionary<string, Mesh> loadedMeshes = new Dictionary<string, Mesh>();
        private static int numberOfLoadedMeshes => loadedMeshes.Count;

        public static bool ContainsMesh(Mesh mesh)
        {
            return loadedMeshes.ContainsKey(mesh.fileName);
        }

        public static bool AddMesh(Mesh mesh)
        {
            if (!ContainsMesh(mesh))
            {
                loadedMeshes.Add(mesh.fileName, mesh);
                Console.WriteLine($"Added mesh {mesh.fileName} to the Mesh Factory.");
                return true;
            }
            Console.WriteLine($"Mesh {mesh.fileName} was already loaded.");
            return false;
        }

        public static void RemoveMesh(Mesh mesh)
        {
            loadedMeshes.Remove(mesh.fileName);
        }
    }
}
