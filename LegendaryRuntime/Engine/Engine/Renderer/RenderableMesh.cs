using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MaterialSystem;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MeshInstancing;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using static LegendaryRenderer.LegendaryRuntime.Engine.Engine.Engine;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer
{
    public class RenderableMesh : GameObject
    {
        public SphereBounds LocalBounds;

        public MeshHasher.CombinedMesh mesh;

        public SphereBounds Bounds;

        public static bool OverrideCulling = false;

        public Material Material;

        public BeginMode RenderMode = BeginMode.Triangles;
        public string fileName { get; private set; }

        public bool Loaded = false;

        public bool Spinning = false;

        private int VertexBufferObject = -1;
        private int VertexArrayObject = -1;
        private int ElementBufferObject = -1;
        private int ElementBufferObjectShadows = -1;

        // optimisation here :)))))
        private int VertexArrayObjectShadows = -1;
        private int VertexBufferObjectShadows = -1;

        public int TriangleCount => VertexCount / 3;

        public int VertexCount = 0;


        public static int ReusedCounter = 0;
        public static int LoadedMeshCount = 0;
        public static int TotalSceneMeshes = 0;

        public RenderableMesh(string file, int part = 0) : base(new Vector3(0, 0, 0), $"{file}-{part}")
        {
            Bounds = new SphereBounds(Transform.Position, 10.0f);
            LocalBounds = new SphereBounds(Vector3.Zero, 10.0f);
            fileName = file;
            if (MeshFactory.AddMesh(this, out RenderableMesh loaded, part))
            {
                Init();
                this.Material = new Material
                {
                };
                LoadedMeshCount++;
            }
            else
            {
                ReusedCounter++;
                this.VertexArrayObject = loaded.VertexArrayObject;
                if (loaded.LocalBounds != null)
                {
                    this.LocalBounds = loaded.LocalBounds;
                }
                // If loaded.LocalBounds is null, this.LocalBounds retains its initial value.

                if (loaded.Bounds != null)
                {
                    this.Bounds = loaded.Bounds;
                }
                // If loaded.Bounds is null, this.Bounds retains its initial value.

                this.fileName = file;
                this.Material = loaded.Material ?? new Material { };
                this.VertexCount = loaded.VertexCount;
                this.IndexCount = loaded.IndexCount;
                this.VertexArrayObjectShadows = loaded.VertexArrayObjectShadows;
                this.Loaded = true;
            }
            TotalSceneMeshes++;
        }

        private int MeshStride = 12;
        void SetBounds(SphereBounds bounds)
        {
            LocalBounds = bounds;

        }

        private int IndexCount;

        public void SetMeshData(MeshHasher.CombinedMesh mesh)
        {
            VertexArrayObject = mesh.RenderMesh.Vao;
            VertexArrayObjectShadows = mesh.ShadowMesh.Vao;
            IndexCount = mesh.RenderMesh.IndexCount;
            VertexCount = mesh.VertexCount;

            LocalBounds = mesh.LocalBounds ?? new SphereBounds(Vector3.Zero, 10.0f);
            Loaded = true;
        }
        public void SetMeshData(int shadowVao = -1, int vao = -1, bool instanced = false, int indexCount = 0, int vertexCount = 0, SphereBounds? localBounds = null)
        {
            if (shadowVao == -1 || vao == -1)
            {
                throw new ArgumentException("Shadow Vao and Vao are required.");
            }

            VertexArrayObject = vao;
            VertexArrayObjectShadows = shadowVao;
            IndexCount = indexCount;
            VertexCount = vertexCount;
            if (localBounds != null)
            {
                // If SphereBounds is a class, assignment is direct.
                // If SphereBounds is a struct, localBounds is Nullable<SphereBounds>,
                // and localBounds.Value would be used.
                // Given the error, SphereBounds is likely a class, or NRTs are used such that localBounds
                // is treated as SphereBounds (non-null) after the check.
                LocalBounds = localBounds;
            }
            else
            {
                LocalBounds = new SphereBounds(Transform.Position, 10);
            }

            Loaded = true;
        }

        public override void Update(float deltaTime)
        {
            // Assuming LocalBounds is guaranteed to be non-null by constructor and SetMeshData methods
            Vector4 origin = new Vector4(LocalBounds.Centre, 1.0f) * Transform.GetWorldMatrix();
            Bounds = new SphereBounds(origin.Xyz, LocalBounds.Radius);

            if (Spinning)
            {
                GetRoot().Transform.LocalRotation *= Quaternion.FromEulerAngles(0.0f, 3.5f * deltaTime, 0.0f);
            }
        }

        /*
         * Base Class Init should be called LAST!!!
         */
        public virtual void Init()
        {
            VertexBufferObject = GL.GenBuffer();
            VertexBufferObjectShadows = GL.GenBuffer();
            VertexArrayObject = GL.GenVertexArray();
            VertexArrayObjectShadows = GL.GenVertexArray();
            ElementBufferObject = GL.GenBuffer();
            ElementBufferObjectShadows = GL.GenBuffer();
        }

        private Matrix4 previousModelMatrix;

        private int tex = -1;

        public override void Render(RenderMode mode = GameObject.RenderMode.Default)
        {
            if (Loaded)
            {
                DrawCalls++;
                GL.Enable(EnableCap.Texture2D);
                currentShader.SetShaderMatrix4x4("model", Transform.GetWorldMatrix(), true);

                if (Material.DiffuseTexture != -1)
                {
                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2D, Material.DiffuseTexture);
                    currentShader.SetShaderInt("diffuseTexture", 0);
                }
                currentShader.SetShaderFloat("hasDiffuse", Material.DiffuseTexture != -1 ? 1.0f : 0.0f);

                if (mode != GameObject.RenderMode.ShadowPass)
                {
                    if (Material.NormalTexture != -1)
                    {
                        GL.ActiveTexture(TextureUnit.Texture1);
                        GL.BindTexture(TextureTarget.Texture2D, Material.NormalTexture);
                        currentShader.SetShaderInt("normalTexture", 1);
                    }

                    if (Material.RoughnessTexture != -1)
                    {
                        GL.ActiveTexture(TextureUnit.Texture2);
                        GL.BindTexture(TextureTarget.Texture2D, Material.RoughnessTexture);
                        currentShader.SetShaderInt("roughnessTexture", 2);
                    }

                    currentShader.SetShaderFloat("hasNormal", Material.NormalTexture != -1 ? 1.0f : 0.0f);
                    currentShader.SetShaderFloat("hasRoughness", Material.RoughnessTexture != -1 ? 1.0f : 0.0f);

                    currentShader.SetShaderVector3("albedoColour", Material.GetMaterialColourAsVector());
                    currentShader.SetShaderVector3("materialParameters", new Vector3(Material.Roughness, 0, 0));

                    uint[] bits = GuidToUIntArray(GUID);
                    currentShader.SetShaderUint("guid0", bits[0]);
                    currentShader.SetShaderUint("guid1", bits[1]);
                    currentShader.SetShaderUint("guid2", bits[2]);
                    currentShader.SetShaderUint("guid3", bits[3]);
                    currentShader.SetShaderMatrix4x4("prev", previousModelMatrix, true);
                    // Engine.currentShader.SetShaderInt("normalMap", 0);

                    BindVAOCached(VertexArrayObject);
                    GL.DrawElements(this.RenderMode, IndexCount, DrawElementsType.UnsignedInt, 0);
                }
                else
                {
                    BindVAOCached(VertexArrayObjectShadows);
                    GL.DrawElements(this.RenderMode, IndexCount, DrawElementsType.UnsignedInt, 0);
                }
                previousModelMatrix = Transform.GetPreviousWorldMatrix();
            }
        }

        public void RenderInstancedShadows(Light? light)
        {
            if (light != null)
            {
                if (Loaded)
                {
                    DrawCalls++;
                    GL.Enable(EnableCap.Texture2D);
                    currentShader.SetShaderMatrix4x4("model", Transform.GetWorldMatrix(), true);

                    if (Material.DiffuseTexture != -1)
                    {
                        GL.ActiveTexture(TextureUnit.Texture0);
                        GL.BindTexture(TextureTarget.Texture2D, Material.DiffuseTexture);
                        currentShader.SetShaderInt("diffuseTexture", 0);
                    }
                    currentShader.SetShaderFloat("hasDiffuse", Material.DiffuseTexture != -1 ? 1.0f : 0.0f);

                    for (int i = 0; i < light.CascadeCount; i++)
                    {
                        currentShader.SetShaderMatrix4x4("shadowInstanceMatrices[" + i + "]", CSMMatrices[i]);
                    }

                    BindVAOCached(VertexArrayObjectShadows);
                    GL.DrawElementsInstanced(PrimitiveType.Triangles, IndexCount, DrawElementsType.UnsignedInt, 0, light.CascadeCount);
                }
            }
        }

        static int lastBoundVAO = -1;

        public static bool invalidated = false;
        public static void BindVAOCached(int vao)
        {
            if (vao != lastBoundVAO || invalidated)
            {
                lastBoundVAO = vao;
                GL.BindVertexArray(vao);
                invalidated = false;
            }
        }
    }
}