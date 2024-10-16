using LegendaryRenderer.Application;
using LegendaryRenderer.GameObjects;
using LegendaryRenderer.Geometry;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;


using static LegendaryRenderer.Application.Engine;

namespace Geometry
{
    public class Mesh
    {
        public Transform localTransform { get; private set; } = new Transform();
        public string fileName { get; private set; }

        protected float[] vertices;
        protected uint[] indices;

        public int VertexCount => vertices.Length / 8;

        public Mesh(string file)
        {
            fileName = file;
            if (MeshFactory.AddMesh(this))
            {
                Init();
            }
        }

        public Mesh()
        {

        }

        public void SetVerticesAndIndices(float[] vertices, uint[] indices)
        {
            this.vertices = vertices;
            this.indices = indices;

            CreateBuffers();
        }



        public Mesh(string fileName, Vector3[] vertices, Vector3[] normals, Vector2[] uvs, Tuple<int,int,int> indices)
        {
            this.fileName = fileName;

            if (vertices.Length != normals.Length && vertices.Length != uvs.Length && uvs.Length != normals.Length)
            {
                Console.WriteLine($"Malformed vertex data in mesh '{fileName}'");
            }
            
            float[] temp = new float[vertices.Length * 8];

            for (int i = 0; i < vertices.Length; i++)
            {
                temp[i * 8]     = vertices[i].X;
                temp[i * 8 + 1] = vertices[i].Y;
                temp[i * 8 + 2] = vertices[i].Z;
                temp[i * 8 + 3] = normals[i].X;
                temp[i * 8 + 4] = normals[i].Y;
                temp[i * 8 + 5] = normals[i].Z;
                temp[i * 8 + 6] = uvs[i].X;
                temp[i * 8 + 7] = uvs[i].X;
            }

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
            VertexBufferObject = GL.GenBuffer();
            VertexArrayObject = GL.GenVertexArray();
            ElementBufferObject = GL.GenBuffer();

            Console.WriteLine($"Initialised VBO, VAO, EBO to {VertexBufferObject}, {VertexArrayObject}, {ElementBufferObject}.");

        }

        public void CopyMesh(Mesh source)
        {
            this.VertexBufferObject = source.VertexBufferObject;
            this.VertexArrayObject = source.VertexArrayObject;
            this.ElementBufferObject = source.ElementBufferObject;
            this.vertices = source.vertices;
            this.indices = source.indices;
        }

        public void CreateBuffers()
        {
            GL.BindVertexArray(VertexArrayObject);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ElementBufferObject);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);

            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 6 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(2);

            // Debug
            Console.WriteLine($"VAO {VertexArrayObject} was bound, and buffer {VertexBufferObject} was uploaded to the GPU. Triangle Count was {(vertices.Length / 8) / 3}.");
        }
        public void Render()
        {
            Engine.TriangleCountTotal += VertexCount / 3;


            Engine.currentShader.UseShader();

            Engine.currentShader.SetShaderMatrix4x4("model", localTransform.GetWorldMatrix(), true);

            //   Console.WriteLine($"LOCAL TRANSFORM: {localTransform.GetWorldMatrix().ToString()}.");
            BindVAOCached(VertexArrayObject);

            GL.DrawElements(BeginMode.Triangles, indices.Length, DrawElementsType.UnsignedInt, 0);
            //GL.DrawArrays(PrimitiveType.Triangles, 0, vertices.Length);
        }

        static int lastBoundVAO = -1;

        public static void BindVAOCached(int vao)
        {
            if (vao != lastBoundVAO)
            {
                lastBoundVAO = vao;
                GL.BindVertexArray(vao);

                Console.WriteLine($"Bound VAO {vao}.");
            }
        }
    }
}
