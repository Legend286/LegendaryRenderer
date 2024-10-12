using Engine.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Geometry
{
    public static class MeshFactory
    {
        private static Dictionary<string, Mesh> loadedMeshes = new Dictionary<string, Mesh>();
        private static int numberOfLoadedMeshes => loadedMeshes.Count;

        public static bool ContainsMesh(Mesh mesh)
        {
            return loadedMeshes.ContainsKey(mesh.fileName);
        }

        public static void AddMesh(Mesh mesh)
        {
            if (!ContainsMesh(mesh))
            {
                loadedMeshes.Add(mesh.fileName, mesh);
            }
        }

        public static void RemoveMesh(Mesh mesh)
        {
            loadedMeshes.Remove(mesh.fileName);
        }
    }
}
