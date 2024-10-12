using LegendaryRenderer.LegendaryRuntime.Engine.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Engine.Geometry
{
    public class Mesh
    {
        public string fileName { get; private set; }

        protected static float[]? vertices;
        protected static uint[]? indices;

        public Mesh(string file)
        {
            fileName = file;
            MeshFactory.AddMesh(this);
        }

        protected int VertexBufferObject;
        protected int VertexArrayObject;
        protected int ElementBufferObject;

    }
}
