using System.Diagnostics;
using Geometry;
using LegendaryRenderer;
using LegendaryRenderer.Application;
using LegendaryRenderer.Shaders;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

public class Frustum
{
    private Vector4[] planes = new Vector4[6];

    private Camera ParentCamera;

    private Matrix4 viewProjMat;
    
    public Frustum(Camera parent)
    {
        ParentCamera = parent;
    }

    public Frustum(Matrix4 viewProjection, Camera? parent = null)
    {
        if (parent != null)
        {
            ParentCamera = parent;
        }
        UpdateFrustumPlanes(viewProjection);
    }

    public void UpdateFrustum(Matrix4 viewProjection)
    {
        UpdateFrustumPlanes(viewProjection);
    }
    
    // Update the frustum planes from the view-projection matrix
    public void UpdateFrustumPlanes(Matrix4 viewProjectionMatrix)
    { 
        viewProjectionMatrix.Transpose();
       
        // Left plane
        planes[0] = new Vector4(
            viewProjectionMatrix.Row3 + viewProjectionMatrix.Row0
        );
        // Right plane
        planes[1] = new Vector4(
            viewProjectionMatrix.Row3 - viewProjectionMatrix.Row0
        );
        // Bottom plane
        planes[2] = new Vector4(
            viewProjectionMatrix.Row3 + viewProjectionMatrix.Row1
        );
        // Top plane
        planes[3] = new Vector4(
            viewProjectionMatrix.Row3 - viewProjectionMatrix.Row1
        );
        // Near plane
        planes[4] = new Vector4(
            viewProjectionMatrix.Row3 + viewProjectionMatrix.Row2
        );
        // Far plane
        planes[5] = new Vector4(
            viewProjectionMatrix.Row3 - viewProjectionMatrix.Row2
        );

        // Normalize the planes
        for (int i = 0; i < 6; i++)
        {
            planes[i] = NormalizePlane(planes[i]);
        }

        viewProjMat = viewProjectionMatrix;
    }
    
    Vector3[] ExtractFrustumCorners()
    {
        // Invert the view-projection matrix
        Matrix4 inverseVP = Matrix4.Invert(viewProjMat);
       // inverseVP.Transpose(); // this broke shadow view culling :D

        // Define frustum corners in NDC
        Vector4[] ndcCorners = new Vector4[]
        {
            new Vector4(-1, -1, -1, 1), // Near bottom-left
            new Vector4( 1, -1, -1, 1), // Near bottom-right
            new Vector4(-1,  1, -1, 1), // Near top-left
            new Vector4( 1,  1, -1, 1), // Near top-right
            new Vector4(-1, -1,  1, 1), // Far bottom-left
            new Vector4( 1, -1,  1, 1), // Far bottom-right
            new Vector4(-1,  1,  1, 1), // Far top-left
            new Vector4( 1,  1,  1, 1), // Far top-right
        };

        // Transform corners to world space
        Vector3[] worldCorners = new Vector3[ndcCorners.Length];
        for (int i = 0; i < ndcCorners.Length; i++)
        {
            Vector4 corner = inverseVP * ndcCorners[i];
            worldCorners[i] = new Vector3(corner.X, corner.Y, corner.Z) / corner.W; // Perspective divide
        }

        return worldCorners;
    }
    
    protected int VertexBufferObject = -1;
    protected int VertexArrayObject = -1;
    protected int ElementBufferObject = -1;

    private Vector3[] vertices;

    private Color4 FrustumColour;

    public bool first = true;
    public bool firstRun = true;

    public static int Count = 0;
    
    // Helper method to visualize the frustum as a list of triangles
    public enum FrustumDrawMode
    {
        Debug,
        Selection,
    }
    public void DrawFrustum(FrustumDrawMode mode)
    {
        using(new ScopedProfiler($"Render {ParentCamera.Name} View Frustum {Count++}"))
        {
            GL.Disable(EnableCap.DepthTest);
            
            if (first)
            {
                vertices = ExtractFrustumCorners();
            }

            uint[] indices =
            {
                3, 2, // near top edge
                2, 0, // near left edge
                0, 1, // near bottom edge
                1, 3, // near right edge

                2, 6, // top left edge
                0, 4, // bottom left edge
                3, 7, // top right edge
                1, 5, // bottom right edge

                6, 7, // far top edge
                4, 6, // far left edge
                4, 5, // far bottom edge
                5, 7, // far right edge
            };

            float[] vertexArray = new float[vertices.Length * 3];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertexArray[i * 3] = vertices[i].X;
                vertexArray[i * 3 + 1] = vertices[i].Y;
                vertexArray[i * 3 + 2] = vertices[i].Z;
            }

            if (firstRun)
            {
                VertexBufferObject = GL.GenBuffer();
                VertexArrayObject = GL.GenVertexArray();
                ElementBufferObject = GL.GenBuffer();

                Console.WriteLine(
                    $"Initialised VBO, VAO, EBO to {VertexBufferObject}, {VertexArrayObject}, {ElementBufferObject}.");

                Random random = new Random();
                FrustumColour = new Color4(random.Next(127, 200), random.Next(127, 200), random.Next(127, 200), 1);

                firstRun = false;
            }

            RenderableMesh.invalidated = true;
            RenderableMesh.BindVAOCached(VertexArrayObject);

            if (first)
            {
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, ElementBufferObject);
                GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices,
                    BufferUsageHint.DynamicDraw);


                GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);

                GL.BufferData(BufferTarget.ArrayBuffer, vertexArray.Length * sizeof(float), vertexArray,
                    BufferUsageHint.DynamicDraw);

                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
                GL.EnableVertexAttribArray(0);
                first = false;
            }

            ShaderFile shader;
            
            ShaderManager.LoadShader("debug", out shader);
            if (mode == FrustumDrawMode.Selection)
            {
                ShaderManager.LoadShader("SelectionBits", out shader);
                shader.UseShader();
                uint[] bits = Engine.GuidToUIntArray(ParentCamera.GUID);
                
                Console.WriteLine($"Frustum Guid {ParentCamera.GUID}");
                Engine.currentShader.SetShaderUint("guid0", bits[0]);
                Engine.currentShader.SetShaderUint("guid1", bits[1]);
                Engine.currentShader.SetShaderUint("guid2", bits[2]);
                Engine.currentShader.SetShaderUint("guid3", bits[3]);

            }

            shader.UseShader();
            shader.SetShaderMatrix4x4("model", Matrix4.Identity);
            shader.SetShaderVector3("Colour",
                new Vector3(FrustumColour.R / 255.0f, FrustumColour.G / 255.0f, FrustumColour.B / 255.0f));

            GL.LineWidth(8);
            GL.Enable(EnableCap.LineSmooth);

            GL.DrawElements(BeginMode.Lines, indices.Length, DrawElementsType.UnsignedInt, 0);
            GL.Enable(EnableCap.DepthTest);
        }
    }

    private Vector4 NormalizePlane(Vector4 plane)
    {
        float magnitude = plane.Xyz.Length;
        return new Vector4(plane.X / magnitude, plane.Y / magnitude, plane.Z / magnitude, plane.W / magnitude);
    }

    public bool ContainsSphere(SphereBounds sphereBounds)
    {
        return ContainsSphere(sphereBounds.Centre, sphereBounds.Radius);
    }
    // Frustum-Sphere intersection test
    public bool ContainsSphere(Vector3 center, float radius)
    {
        for (int i = 0; i < 6; i++)
        {
            // Calculate the signed distance from the sphere center to the plane
            float distance = Vector3.Dot(planes[i].Xyz, center) + planes[i].W;

            // If the distance is less than the negative radius, the sphere is outside
            if (distance < -(radius * 2))
            {
                return false;
            }
        }
        return true; // The sphere is either intersecting or inside the frustum
    }
    
    public bool ContainsFrustum(Frustum other)
    {
        Vector3[] otherCorners = other.ExtractFrustumCorners();

        foreach (Vector4 plane in planes)
        {
            int outsideCount = 0;

            foreach (Vector3 corner in otherCorners)
            {
                float distance = Vector3.Dot(plane.Xyz, corner) + plane.W;

                if (distance < 0)
                {
                    outsideCount++;
                }
            }
           
            if (outsideCount == otherCorners.Length)
            {
                return false;
            }
        }

        return true;
    }

}