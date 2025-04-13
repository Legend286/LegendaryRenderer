using System.Xml.Resolvers;
using OpenTK.Graphics.OpenGL;

namespace Geometry;

public class AutoExposureCompute
{
    static int computeID = -1;

    public static float RenderAutoExposureCompute(int textureWidth, int textureHeight, int textureID,
        float prevExposure, float lerpFactor)
    {


        if (computeID == -1)
        {
            computeID = LoadReductionProgram("LegendaryRuntime/Engine/Shaders/glsl/AutoExposureCompute.comp");
        }

        float exposure = ComputeAutoExposure(textureID, textureWidth, textureHeight, computeID, prevExposure,
            lerpFactor);
        
        return exposure;
    }


    public static int LoadReductionProgram(string computeShaderFile)
    {
        // Load the compute shader source from file.
        string computeShaderSource = File.ReadAllText(computeShaderFile);

        // Create the compute shader.
        int computeShader = GL.CreateShader(ShaderType.ComputeShader);
        GL.ShaderSource(computeShader, computeShaderSource);
        GL.CompileShader(computeShader);

        // Check for compilation errors.
        GL.GetShader(computeShader, ShaderParameter.CompileStatus, out int compileStatus);
        if (compileStatus != (int)All.True)
        {
            string log = GL.GetShaderInfoLog(computeShader);
            throw new Exception("Compute shader compilation failed: " + log);
        }

        // Create a program and attach the compute shader.
        int programID = GL.CreateProgram();
        GL.AttachShader(programID, computeShader);
        GL.LinkProgram(programID);

        // Check for linking errors.
        GL.GetProgram(programID, GetProgramParameterName.LinkStatus, out int linkStatus);
        if (linkStatus != (int)All.True)
        {
            string log = GL.GetProgramInfoLog(programID);
            throw new Exception("Shader program linking failed: " + log);
        }

        Console.WriteLine("Loaded and compiled Compute Shader 'AutoExposureCompute.comp' successfully.");


        // Detach and delete the shader after linking.
        GL.DetachShader(programID, computeShader);
        GL.DeleteShader(computeShader);

        return programID;
    }

    private static int intermediateTexture = -1;

    public static float ComputeAutoExposure(
        int hdrTextureID, // Your HDR texture containing the final lighting
        int hdrWidth,
        int hdrHeight,
        int reductionProgramID, // The compute shader program ID for reduction
        float previousExposure, // Exposure value from the previous frame
        float exposureLerpFactor // Lerp factor for smoothing (e.g. 0.05f)
    )
    {
        int currentTexture = hdrTextureID;
        int currentWidth = hdrWidth;
        int currentHeight = hdrHeight;
        
        int pingTexture = GL.GenTexture();
        int pongTexture = GL.GenTexture();
        
        int outputTexture = pingTexture;

        while (currentWidth > 1 || currentHeight > 1)
        {
            int groupsX = (currentWidth + 16 - 1) / 16;
            int groupsY = (currentHeight + 16 - 1) / 16;
            
            GL.BindTexture(TextureTarget.Texture2D, outputTexture);
            GL.TexImage2D(TextureTarget.Texture2D,
                0,
                PixelInternalFormat.Rgba32f,
                groupsX,
                groupsY,
                0,
                PixelFormat.Rgba,
                PixelType.Float,
                IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Nearest);

            GL.UseProgram(reductionProgramID);

            int inputSizeLocation = GL.GetUniformLocation(reductionProgramID, "inputSize");
            GL.Uniform2(inputSizeLocation, currentWidth, currentHeight);

            GL.BindImageTexture(0, currentTexture, 0, false, 0, TextureAccess.ReadOnly, SizedInternalFormat.Rgba32f);
            GL.BindImageTexture(1, outputTexture, 0, false, 0, TextureAccess.WriteOnly, SizedInternalFormat.Rgba32f);
          
          
            GL.DispatchCompute(groupsX, groupsY, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.TextureFetchBarrierBit |
                             MemoryBarrierFlags.ShaderImageAccessBarrierBit);
            
            currentTexture = outputTexture;
            currentWidth = groupsX;
            currentHeight = groupsY;
            
            outputTexture = (outputTexture == pingTexture) ? pongTexture : pingTexture;
        }
        
        float[] pixelData = new float[4];
        GL.BindTexture(TextureTarget.Texture2D, currentTexture);
        GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.Float, pixelData);
        float totalLuminance = pixelData[0]; 
        float averageLuminance = totalLuminance / (hdrWidth * hdrHeight);

        
        float computedExposure = 0.18f / Math.Max(averageLuminance, 0.001f);
        
        float finalExposure = previousExposure + exposureLerpFactor * (computedExposure - previousExposure);
        
        GL.DeleteTexture(pingTexture);
        GL.DeleteTexture(pongTexture);
       
        /*
        Console.WriteLine("Total Luminance: " + totalLuminance);
        Console.WriteLine("Average Luminance: " + averageLuminance);
        Console.WriteLine("Computed Exposure (before smoothing): " + computedExposure);
        Console.WriteLine("Final Exposure: " + finalExposure);
*/
        return finalExposure;
    }
}