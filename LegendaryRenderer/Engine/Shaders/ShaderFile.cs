using System.Net;
using OpenTK.Graphics.OpenGL;

namespace LegendaryRenderer.Engine.Shaders;

public class ShaderFile : IDisposable
{
    private int ShaderHandle;
    public bool IsCompiled = false;
    private int VertexShader;
    private int FragmentShader;
    
    /*
     * Specify the shader path either with .vert/.frag extension or without. 
     */
    public ShaderFile(string vertex, string fragment)
    {
        if (!vertex.Contains(".vert"))
        {
            if (vertex.Contains('.'))
            {
                throw new ArgumentException("Vertex shader file path has a typo...");
            }
            else
            {
                vertex = vertex + ".vert";
            }
        }

        if (!fragment.Contains(".frag"))
        {
            if (fragment.Contains('.'))
            {
                throw new ArgumentException("Fragment shader file path has a typo...");
            }
            else
            {
                fragment = fragment + ".frag";
            }
        }

        if (File.Exists(vertex) && File.Exists(fragment))
        {
            string VertexShaderSource = File.ReadAllText(vertex);
            string FragmentShaderSource = File.ReadAllText(fragment);

            VertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(VertexShader, VertexShaderSource);

            FragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(FragmentShader, FragmentShaderSource);

            if (CompileShader(VertexShader) && CompileShader(FragmentShader))
            {
                ShaderHandle = GL.CreateProgram();

                // Attach both shaders...
                GL.AttachShader(ShaderHandle, VertexShader);
                GL.AttachShader(ShaderHandle, FragmentShader);
                if (LinkProgram(ShaderHandle))
                {
                    Console.WriteLine(
                        $"Loaded and compiled vertex shader '{vertex}' and fragment shader '{fragment}' successfully.");
                    IsCompiled = true;
                }
            }

        }
        else
        {
            throw new ArgumentException("One or more shader files do not exist.");
        }

    }

    private static bool LinkProgram(int program)
    {
        // We link the program
        GL.LinkProgram(program);

        // Check for linking errors
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out var code);
        if (code != (int)All.True)
        {
            // We can use `GL.GetProgramInfoLog(program)` to get information about the error.
            throw new Exception($"Error occurred whilst linking Program({program})");
            return false;
        }

        return true;
    }
    
    private static bool CompileShader(int shader)
    {
        // Try to compile the shader
        GL.CompileShader(shader);

        // Check for compilation errors
        GL.GetShader(shader, ShaderParameter.CompileStatus, out var code);
        if (code != (int)All.True)
        {
            // We can use `GL.GetShaderInfoLog(shader)` to get information about the error.
            var infoLog = GL.GetShaderInfoLog(shader);
            throw new Exception($"Error occurred whilst compiling Shader({shader}).\n\n{infoLog}");
            return false;
        }

        return true;
    }
    
    public void UseShader()
    {
        GL.UseProgram(ShaderHandle);
    }

    public ShaderFile(string path): this(path, path)
    {
        
    }

    private bool IsDisposed = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            GL.DeleteProgram(ShaderHandle);
            IsDisposed = true;
        }
    }

    ~ShaderFile()
    {
        if (IsDisposed == false)
        {
            Console.WriteLine("GPU Resource Leak! Did you forget to call Dispose()?");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}