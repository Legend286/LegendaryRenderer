using OpenTK.Graphics.OpenGL;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Utilities;

public static class GLHelpers
{
    public static void CheckGLError(string context)
    {
        ErrorCode error;
        while ((error = GL.GetError()) != ErrorCode.NoError)
        {
            Console.WriteLine($"OpenGL Error in {context}: {error}");
        }
    }
    
    public enum TextureHandle
    {
        ALBEDO,
        PRIMARY_DEPTH,
        WORLD_NORMALS,
        VELOCITY,
        SELECTION_BUFFER_MASK,
        SELECTION_BUFFER_DEPTH,
        LIGHTING_RESULT,
        COPY,
    }
}