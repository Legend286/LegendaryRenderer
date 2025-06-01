using LegendaryRenderer.LegendaryRuntime.Engine.Engine.EngineTypes;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer;
using LegendaryRenderer.Shaders;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using LegendaryRenderer.LegendaryRuntime.Application;
using System.Linq;
using System.Collections.Generic;
using static LegendaryRenderer.LegendaryRuntime.Engine.Engine.Engine;

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
                Engine.currentShader.SetShaderInt("lightEnableVolumetrics", light.EnableVolumetrics ? 1 : 0);
                Engine.currentShader.SetShaderInt("lightShadowsEnabled", light.EnableShadows ? 1 : 0);
                
                Engine.currentShader.SetShaderInt("enableIESProfile", light.UseIESProfile ? 1 : 0);

                // Set instanced shadow atlas uniforms
                Engine.currentShader.SetShaderInt("useInstancedShadows", Engine.UseInstancedShadows ? 1 : 0);
                
                if (Engine.UseInstancedShadows)
                {
                    // Bind shadow atlas texture
                    int atlasTextureUnit = textureNames.Length + 2;
                    GL.ActiveTexture((TextureUnit)atlasTextureUnit + 33984);
                    GL.BindTexture(TextureTarget.Texture2D, Engine.GetShadowAtlasTexture());
                    Engine.currentShader.SetShaderInt("shadowAtlas", atlasTextureUnit);
                    
                    // Use the same light filtering logic as in BuildInstancedShadowData()
                    if (Engine.TryGetLightShadowInfo(light, out var lightShadowInfo, out int visibleLightIndex))
                    {
                        // Use the same tile size that was calculated during shadow generation
                        int currentTileSize = Engine.GetCurrentAtlasTileSize();
                        
                        // Calculate base atlas index for this light (same logic as BuildInstancesForLight)
                        int baseAtlasIndex = lightShadowInfo.BaseAtlasIndex;
                        
                        // For point lights, we need to pass the base tile info for face 0
                        // The fragment shader will calculate the correct face tile offset
                        AtlasTileInfo atlasInfo;
                        if (light.Type == Light.LightType.Point)
                        {
                            // Point lights: Pass base tile info - shader will handle face offset calculation
                            atlasInfo = Engine.CalculateAtlasTile(baseAtlasIndex, Engine.ShadowAtlasResolution, currentTileSize);
                            
                            // Debug output for atlas tile calculation
                            Console.WriteLine($"Point Light '{light.Name}' (Visible Index: {visibleLightIndex}): " +
                                            $"BaseAtlasIndex={baseAtlasIndex} (6 consecutive tiles), TileSize={currentTileSize}, " +
                                            $"BaseAtlasInfo=Scale({atlasInfo.atlasScale.X:F3},{atlasInfo.atlasScale.Y:F3}) " +
                                            $"Offset({atlasInfo.atlasOffset.X:F3},{atlasInfo.atlasOffset.Y:F3})");
                        }
                        else
                        {
                            // Spot/Directional lights: Use single tile calculation
                            atlasInfo = Engine.CalculateAtlasTile(baseAtlasIndex, Engine.ShadowAtlasResolution, currentTileSize);
                            
                            // Debug output for atlas tile calculation
                            Console.WriteLine($"Light '{light.Name}' (Type: {light.Type}, Visible Index: {visibleLightIndex}): " +
                                            $"BaseAtlasIndex={baseAtlasIndex}, TileSize={currentTileSize}, " +
                                            $"AtlasInfo=Scale({atlasInfo.atlasScale.X:F3},{atlasInfo.atlasScale.Y:F3}) " +
                                            $"Offset({atlasInfo.atlasOffset.X:F3},{atlasInfo.atlasOffset.Y:F3})");
                        }
                        
                        Engine.currentShader.SetShaderVector4("lightAtlasInfo", 
                            new Vector4(atlasInfo.atlasScale.X, atlasInfo.atlasScale.Y, atlasInfo.atlasOffset.X, atlasInfo.atlasOffset.Y));
                    }
                    else
                    {
                        // Light not found in visible lights - this light isn't casting shadows
                        Engine.currentShader.SetShaderVector4("lightAtlasInfo", new Vector4(1.0f, 1.0f, 0.0f, 0.0f));
                    }
                    
                    // Now handle view projection matrices based on light type
                    if (light.Type == Light.LightType.Spot || light.Type == Light.LightType.Projector)
                    {
                        if (Engine.TryGetLightShadowInfo(light, out _, out int visLightIdx_matrices)) // Re-check or use earlier result
                        {
                            // Use the exact matrix from shadow generation via engine method
                            Matrix4 shadowMatrix = Engine.GetStoredShadowMatrix(visLightIdx_matrices, 0);
                            Engine.currentShader.SetShaderMatrix4x4("shadowViewProjection", shadowMatrix);
                        }
                    }
                    else if (light.Type == Light.LightType.Point)
                    {
                        if (Engine.TryGetLightShadowInfo(light, out _, out int visLightIdx_matrices)) // Re-check or use earlier result
                        {
                            // Use exact matrices from shadow generation
                            for (int i = 0; i < 6; i++)
                            {
                                Matrix4 shadowMatrix = Engine.GetStoredShadowMatrix(visLightIdx_matrices, i);
                                Engine.currentShader.SetShaderMatrix4x4($"shadowViewProjection{i}", shadowMatrix);
                            }
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
                else
                {
                    // Non-instanced path - use original logic
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

                if (light.LightIESProfile != null)
                {
                    int target = textureNames.Length + 1;
                    GL.ActiveTexture((TextureUnit)target + 33984); // don't forget the offset!
                    GL.BindTexture(TextureTarget.Texture2D, light.LightIESProfile.TextureID);
                    Engine.currentShader.SetShaderInt("IESProfileTexture", target);
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