using Geometry.MaterialSystem;
using Geometry.MaterialSystem.IESProfiles;
using LegendaryRenderer.Application;
using LegendaryRenderer.GameObjects;
using LegendaryRenderer.LegendaryRuntime.Engine.Renderer.MaterialSystem;
using LegendaryRenderer.Shaders;
using Microsoft.VisualBasic;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TheLabs.LegendaryRuntime.Engine.GameObjects;
using TheLabs.LegendaryRuntime.Engine.Renderer;
using static LegendaryRenderer.Application.Engine;

namespace Geometry
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

        private bool Loaded = false;

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
            fileName = file;
            if (MeshFactory.AddMesh(this, out RenderableMesh loaded, part))
            {
                Init();
                Console.WriteLine($"Mesh loaded: {Name}");
                this.Material = new Material
                {
                };
                LoadedMeshCount++;
            }
            else
            {
                ReusedCounter++;
                Console.WriteLine($"Already Loaded: {Name}. Mesh Cache has saved {ReusedCounter} duplicate GPU buffers.");
                this.VertexArrayObject = loaded.VertexArrayObject;
                this.LocalBounds = loaded.LocalBounds;
                this.Bounds = loaded.Bounds;
                this.fileName = file;
                this.Material = loaded.Material;
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

            Console.WriteLine(
                $"Initialised VBO, VAO, EBO to {VertexBufferObject}, {VertexArrayObject}, {ElementBufferObject}. Shadow Buffers EBO: {ElementBufferObjectShadows}, VAO: {VertexArrayObjectShadows} VBO: {VertexBufferObjectShadows}");

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
                    GL.DrawElements(RenderMode, IndexCount, DrawElementsType.UnsignedInt, 0);
                }
                else
                {
                    BindVAOCached(VertexArrayObjectShadows);
                    GL.DrawElements(RenderMode, IndexCount, DrawElementsType.UnsignedInt, 0);
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

           //     Console.WriteLine($"Bound VAO {vao}.");
            }
        }
    }
}
