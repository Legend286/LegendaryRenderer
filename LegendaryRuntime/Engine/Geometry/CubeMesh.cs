using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace Engine.Geometry
{
    public class CubeMesh : ProceduralMesh
    {
        Vector3 Size = Vector3.One;
        Vector2 UVScale = Vector2.One;

        public CubeMesh(Vector3 size, Vector2 uvScale, string file = "CubeMeshProcedural") : base(file)
        {
            Init();

            Size = size;
            UVScale = uvScale;
        }

        public void Init()
        {
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
            0, 1, 3,
            1, 2, 3,
            };

        }

    }
}
