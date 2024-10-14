using LegendaryRenderer.Application;
using LegendaryRenderer.GameObjects;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;


using static LegendaryRenderer.Application.Engine;

namespace Geometry
{
    public class Mesh
    {
        public string fileName { get; private set; }

        protected static float[] vertices;
        protected static uint[] indices;

        public int VertexCount = 0;

        public Mesh(string file)
        {
            fileName = file;
            if (MeshFactory.AddMesh(this))
            {
                Init();
            }
        }

        protected int VertexBufferObject = -1;
        protected int VertexArrayObject = -1;
        protected int ElementBufferObject = -1;

        /*
         * Base Class Init should be called LAST!!!
         */
        public virtual void Init()
        {
            // Debug
            
            VertexBufferObject = GL.GenBuffer();
            VertexArrayObject = GL.GenVertexArray();
            ElementBufferObject = GL.GenBuffer();

            Console.WriteLine($"Initialised VBO, VAO, EBO to {VertexBufferObject}, {VertexArrayObject}, {ElementBufferObject}.");
        }

        public void CreateBuffers()
        {
            GL.BindVertexArray(VertexArrayObject);

            GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);

            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 6 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(2);

            // Debug
            Console.WriteLine($"VAO {VertexArrayObject} was bound, and buffer {VertexBufferObject} was uploaded to the GPU. Triangle Count was {vertices.Length / 8}.");
        }


    }
}
