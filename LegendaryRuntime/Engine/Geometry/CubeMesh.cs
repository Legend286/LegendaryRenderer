using LegendaryRenderer.Application;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;

namespace Geometry
{
    public class CubeMesh : ProceduralMesh, IEquatable<CubeMesh>
    {
        Vector3 Size = Vector3.One;
        Vector2 UVScale = Vector2.One;

        public CubeMesh(Vector3 size, Vector2 uvScale, string file = $"CubeMeshProcedural") : base(file)
        {
            Size = size;
            UVScale = uvScale;
            file = $"{file}{UVScale.ToString()}{Size.ToString()}";
        }

        public override void Init()
        {
            base.Init();
            SetupMeshData();
            Console.WriteLine($"Building mesh {fileName}");
            VertexCount = vertices.Length / 8;
            Engine.TriangleCountTotal += VertexCount;
        }

        public override void SetupMeshData()
        {
            Console.WriteLine($"Creating Buffer objects for mesh {fileName}.");

            vertices = new[] {
            /*                 position                            normal                        coord             */
            -0.5f * Size.X, -0.5f * Size.Y, -0.5f * Size.Z,  0.0f,  0.0f, -1.0f,  0.0f * UVScale.X, 0.0f * UVScale.Y,
             0.5f * Size.X, -0.5f * Size.Y, -0.5f * Size.Z,  0.0f,  0.0f, -1.0f,  1.0f * UVScale.X, 0.0f * UVScale.Y,
             0.5f * Size.X,  0.5f * Size.Y, -0.5f * Size.Z,  0.0f,  0.0f, -1.0f,  1.0f * UVScale.X, 1.0f * UVScale.Y,
             0.5f * Size.X,  0.5f * Size.Y, -0.5f * Size.Z,  0.0f,  0.0f, -1.0f,  1.0f * UVScale.X, 1.0f * UVScale.Y,
            -0.5f * Size.X,  0.5f * Size.Y, -0.5f * Size.Z,  0.0f,  0.0f, -1.0f,  0.0f * UVScale.X, 1.0f * UVScale.Y,
            -0.5f * Size.X, -0.5f * Size.Y, -0.5f * Size.Z,  0.0f,  0.0f, -1.0f,  0.0f * UVScale.X, 0.0f * UVScale.Y,

            -0.5f * Size.X, -0.5f * Size.Y,  0.5f * Size.Z,  0.0f,  0.0f,  1.0f,  0.0f * UVScale.X, 0.0f * UVScale.Y,
             0.5f * Size.X, -0.5f * Size.Y,  0.5f * Size.Z,  0.0f,  0.0f,  1.0f,  1.0f * UVScale.X, 0.0f * UVScale.Y,
             0.5f * Size.X,  0.5f * Size.Y,  0.5f * Size.Z,  0.0f,  0.0f,  1.0f,  1.0f * UVScale.X, 1.0f * UVScale.Y,
             0.5f * Size.X,  0.5f * Size.Y,  0.5f * Size.Z,  0.0f,  0.0f,  1.0f,  1.0f * UVScale.X, 1.0f * UVScale.Y,
            -0.5f * Size.X,  0.5f * Size.Y,  0.5f * Size.Z,  0.0f,  0.0f,  1.0f,  0.0f * UVScale.X, 1.0f * UVScale.Y,
            -0.5f * Size.X, -0.5f * Size.Y,  0.5f * Size.Z,  0.0f,  0.0f,  1.0f,  0.0f * UVScale.X, 0.0f * UVScale.Y,

            -0.5f * Size.X,  0.5f * Size.Y,  0.5f * Size.Z, -1.0f,  0.0f,  0.0f,  1.0f * UVScale.X, 0.0f * UVScale.Y,
            -0.5f * Size.X,  0.5f * Size.Y, -0.5f * Size.Z, -1.0f,  0.0f,  0.0f,  1.0f * UVScale.X, 1.0f * UVScale.Y,
            -0.5f * Size.X, -0.5f * Size.Y, -0.5f * Size.Z, -1.0f,  0.0f,  0.0f,  0.0f * UVScale.X, 1.0f * UVScale.Y,
            -0.5f * Size.X, -0.5f * Size.Y, -0.5f * Size.Z, -1.0f,  0.0f,  0.0f,  0.0f * UVScale.X, 1.0f * UVScale.Y,
            -0.5f * Size.X, -0.5f * Size.Y,  0.5f * Size.Z, -1.0f,  0.0f,  0.0f,  0.0f * UVScale.X, 0.0f * UVScale.Y,
            -0.5f * Size.X,  0.5f * Size.Y,  0.5f * Size.Z, -1.0f,  0.0f,  0.0f,  1.0f * UVScale.X, 0.0f * UVScale.Y,

             0.5f * Size.X,  0.5f * Size.Y,  0.5f * Size.Z,  1.0f,  0.0f,  0.0f,  1.0f * UVScale.X, 0.0f * UVScale.Y,
             0.5f * Size.X,  0.5f * Size.Y, -0.5f * Size.Z,  1.0f,  0.0f,  0.0f,  1.0f * UVScale.X, 1.0f * UVScale.Y,
             0.5f * Size.X, -0.5f * Size.Y, -0.5f * Size.Z,  1.0f,  0.0f,  0.0f,  0.0f * UVScale.X, 1.0f * UVScale.Y,
             0.5f * Size.X, -0.5f * Size.Y, -0.5f * Size.Z,  1.0f,  0.0f,  0.0f,  0.0f * UVScale.X, 1.0f * UVScale.Y,
             0.5f * Size.X, -0.5f * Size.Y,  0.5f * Size.Z,  1.0f,  0.0f,  0.0f,  0.0f * UVScale.X, 0.0f * UVScale.Y,
             0.5f * Size.X,  0.5f * Size.Y,  0.5f * Size.Z,  1.0f,  0.0f,  0.0f,  1.0f * UVScale.X, 0.0f * UVScale.Y,

            -0.5f * Size.X, -0.5f * Size.Y, -0.5f * Size.Z,  0.0f, -1.0f,  0.0f,  0.0f * UVScale.X, 1.0f * UVScale.Y,
             0.5f * Size.X, -0.5f * Size.Y, -0.5f * Size.Z,  0.0f, -1.0f,  0.0f,  1.0f * UVScale.X, 1.0f * UVScale.Y,
             0.5f * Size.X, -0.5f * Size.Y,  0.5f * Size.Z,  0.0f, -1.0f,  0.0f,  1.0f * UVScale.X, 0.0f * UVScale.Y,
             0.5f * Size.X, -0.5f * Size.Y,  0.5f * Size.Z,  0.0f, -1.0f,  0.0f,  1.0f * UVScale.X, 0.0f * UVScale.Y,
            -0.5f * Size.X, -0.5f * Size.Y,  0.5f * Size.Z,  0.0f, -1.0f,  0.0f,  0.0f * UVScale.X, 0.0f * UVScale.Y,
            -0.5f * Size.X, -0.5f * Size.Y, -0.5f * Size.Z,  0.0f, -1.0f,  0.0f,  0.0f * UVScale.X, 1.0f * UVScale.Y,

            -0.5f * Size.X,  0.5f * Size.Y, -0.5f * Size.Z,  0.0f,  1.0f,  0.0f,  0.0f * UVScale.X, 1.0f * UVScale.Y,
             0.5f * Size.X,  0.5f * Size.Y, -0.5f * Size.Z,  0.0f,  1.0f,  0.0f,  1.0f * UVScale.X, 1.0f * UVScale.Y,
             0.5f * Size.X,  0.5f * Size.Y,  0.5f * Size.Z,  0.0f,  1.0f,  0.0f,  1.0f * UVScale.X, 0.0f * UVScale.Y,
             0.5f * Size.X,  0.5f * Size.Y,  0.5f * Size.Z,  0.0f,  1.0f,  0.0f,  1.0f * UVScale.X, 0.0f * UVScale.Y,
            -0.5f * Size.X,  0.5f * Size.Y,  0.5f * Size.Z,  0.0f,  1.0f,  0.0f,  0.0f * UVScale.X, 0.0f * UVScale.Y,
            -0.5f * Size.X,  0.5f * Size.Y, -0.5f * Size.Z,  0.0f,  1.0f,  0.0f,  0.0f * UVScale.X, 1.0f * UVScale.Y,
            };


            indices = new uint[]
            {
                // front
                0,1,2,
                2,3,0,
                // right
                1,5,6,
                6,2,1,
                // back
                7,6,5,
                5,4,7,
                // left
                4,0,3,
                3,7,4,
                // bottom
                4,5,1,
                1,0,4,
                // top
                3,2,6, 
                6,7,3,
            };


            base.CreateBuffers();
        }
        public void Render()
        {
            Engine.currentShader.UseShader();
            GL.BindVertexArray(VertexArrayObject);

            GL.DrawArrays(PrimitiveType.Triangles, 0, vertices.Length / 8);

        }

        public bool Equals(CubeMesh? other)
        {
            if (other.UVScale == UVScale && other.Size == Size)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
