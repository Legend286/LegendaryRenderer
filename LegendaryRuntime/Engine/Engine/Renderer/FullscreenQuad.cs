using LegendaryRenderer.LegendaryRuntime.Engine.Engine.EngineTypes;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
using LegendaryRenderer.Shaders;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using LegendaryRenderer.LegendaryRuntime.Application;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer;

public static class FullscreenQuad
{

    static int vertexArrayObject;
    static int vertexBufferObject;
    static int elementBufferObject;
    static ShaderFile shader;

    static float[] vertices = new float[]
    {
        -1.0f, 1.0f, 0.0f, 0.0f, 1.0f,
        1.0f, 1.0f, 0.0f, 1.0f, 1.0f,
        -1.0f, -1.0f, 0.0f, 0.0f, 0.0f,
        1.0f, -1.0f, 0.0f, 1.0f, 0.0f
    };

    static uint[] indices = new uint[]
    {
        3,1,2,2,1,0
    };

    private static bool initialised = false;

    public static void BuildQuadData(string shaderFileName)
    {
        // Setup buffers
        if (initialised == false)
        {
            initialised = true;
            GL.GenVertexArrays(1, out vertexArrayObject);
            GL.GenBuffers(1, out vertexBufferObject);
            GL.GenBuffers(1, out elementBufferObject);

            GL.BindVertexArray(vertexArrayObject);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObject);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(vertices.Length * sizeof(float)), vertices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, elementBufferObject);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(indices.Length * sizeof(uint)), indices, BufferUsageHint.StaticDraw);

            // Vertex attributes
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
        }
        ShaderManager.LoadShader(shaderFileName, out shader);
    }



    public static void RenderQuad(string shaderFileName, int[] textures, string[] textureNames, Transform? Transform = null, Light? light = null)
    {

        BuildQuadData(shaderFileName);
        shader.UseShader();

        for (int i = 0; i < textureNames.Length; i++)
        {
            GL.ActiveTexture((TextureUnit)i + 33984); // don't forget the offset!
            GL.BindTexture(TextureTarget.Texture2D, textures[i]);
            Engine.currentShader.SetShaderInt(textureNames[i], i);
        }
        Engine.currentShader.SetShaderFloat("exposureValue", Engine.currentExposure);
        Engine.currentShader.SetShaderVector4("screenDimensions", new Vector4(Application.Application.Width, Application.Application.Height, 1/(float)Application.Application.Width, 1/(float)Application.Application.Height));
        float far = Engine.ActiveCamera.ZFar;
        float near = Engine.ActiveCamera.ZNear;
        Engine.currentShader.SetShaderVector4("projectionParameters", new Vector4(near, far, 1 / near, 1 / far));
        Engine.currentShader.SetShaderMatrix4x4("view", Engine.ActiveCamera.ViewMatrix);
        Engine.currentShader.SetShaderMatrix4x4("projection", Engine.ActiveCamera.ProjectionMatrix);
        
        //uniform vec4 SSAOParams;
        // // X = Radius, Y = Bias, Z = Number of Samples
        // 
        Engine.currentShader.SetShaderVector4("SSAOParams", new Vector4(Engine.SSAOSettings.Radius, Engine.SSAOSettings.Bias, (float)Engine.SSAOSettings.NumberOfSamples, 0.0f));
        
        // move to light renderer
        if (Transform != null)
        {
            Engine.currentShader.SetShaderVector3("lightPosition", Transform.Position);
            Engine.currentShader.SetShaderVector3("spotLightDir", Transform.Forward.Normalized());
            
            if (light != null)
            {
                float DegToRad = 0.01745329251f;
                float innerCos = MathF.Cos(DegToRad * 0.5f * light.InnerCone);
                float outerCos = MathF.Cos(DegToRad * 0.5f * light.OuterCone);
                float angleRangeInv = 1.0f / MathF.Max(innerCos - outerCos, 0.00000001f);
            
                Engine.currentShader.SetShaderFloat("shadowResolution", Engine.ShadowResolution);
                Engine.currentShader.SetShaderVector3("spotLightCones", new Vector3(angleRangeInv, -outerCos * angleRangeInv, 0));
                Engine.currentShader.SetShaderVector3("lightColour", new Vector3(light.Colour.R, light.Colour.G, light.Colour.B));
                Engine.currentShader.SetShaderInt("lightType", (int)light.Type);
                Engine.currentShader.SetShaderInt("enableCookie", light.EnableCookie ? 1 : 0);
                Engine.currentShader.SetShaderFloat("lightShadowBiasNormal", light.NormalBias);
                Engine.currentShader.SetShaderFloat("lightRadius", 1.0f / MathF.Max(light.Range * light.Range, 0.0000001f));
                Engine.currentShader.SetShaderFloat("lightIntensity", light.Intensity);
                Engine.currentShader.SetShaderFloat("lightShadowBias", light.Bias);
                Engine.currentShader.SetShaderInt("lightShadowsEnabled", light.EnableShadows ? 1 : 0);
                
                Engine.currentShader.SetShaderInt("enableIESProfile", light.UseIESProfile ? 1 : 0);

                if (light.LightIESProfile != null)
                {
                    int target = textureNames.Length + 1;
                    GL.ActiveTexture((TextureUnit)target + 33984); // don't forget the offset!
                    GL.BindTexture(TextureTarget.Texture2D, light.LightIESProfile.TextureID);
                    Engine.currentShader.SetShaderInt("IESProfileTexture", target);
                }

                if (light.Type == Light.LightType.Spot || light.Type == Light.LightType.Projector)
                {
                    Engine.currentShader.SetShaderMatrix4x4("shadowViewProjection", light.ViewProjectionMatrix);
                }
                else if (light.Type == Light.LightType.Point)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        Engine.currentShader.SetShaderMatrix4x4($"shadowViewProjection{i}", light.PointLightViewProjections[i]);
                    }
                }
                else if (light.Type == Light.LightType.Directional)
                {
                    Matrix4[] cascadeViewProjections = Light.GenerateCascadedShadowMatrices(Engine.ActiveCamera, light, Engine.ShadowResolution);
                    
                    for (int i = 0; i < light.CascadeCount; i++)
                    {
                        Engine.currentShader.SetShaderMatrix4x4($"shadowViewProjection{i}", cascadeViewProjections[i]);
                    }
                    Engine.currentShader.SetShaderInt("cascadeCount", light.CascadeCount);
                }
            }
        }
        
        RenderableMesh.invalidated = true;
        RenderableMesh.BindVAOCached(vertexArrayObject);
        GL.Disable(EnableCap.DepthTest);
        GL.DrawElements(PrimitiveType.Triangles, indices.Length, DrawElementsType.UnsignedInt, 0);
        GL.Enable(EnableCap.DepthTest);


    }
}