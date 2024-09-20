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
                if (LinkShaders())
                {
                    Console.WriteLine($"Loaded and compiled vertex shader '{vertex}' and fragment shader '{fragment}' successfully.");
                    IsCompiled = true;
                }
            }
        }
        else
        {
            throw new ArgumentException("One or more shader files do not exist.");
        }

    }

    private bool LinkShaders()
    {
        ShaderHandle = GL.CreateProgram();

        GL.AttachShader(ShaderHandle, VertexShader);
        GL.AttachShader(ShaderHandle, FragmentShader);

        GL.LinkProgram(ShaderHandle);

        GL.GetProgram(ShaderHandle, ProgramParameter.LinkStatus, out int success);

        if (success == 0)
        {
            GL.GetProgramInfoLog(ShaderHandle, out string info);
            Console.WriteLine(info);

            
            GL.DetachShader(ShaderHandle, VertexShader);
            GL.DetachShader(ShaderHandle, FragmentShader);

            GL.DeleteShader(VertexShader);
            GL.DeleteShader(FragmentShader);
            
            return true;
        }
        else
        {
            GL.GetProgramInfoLog(ShaderHandle, out string info);
            Console.WriteLine($"Program had errors! See log \n {info}");
            return false;
        }
    }
    private bool CompileShader(int shader)
    {
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
        GL.GetShaderInfoLog(shader, out string info);

        if (success == 0)
        {
            Console.WriteLine(info);
            return true;
        }
        
        Console.WriteLine($"Shader had errors! See log: \n {info}");
        return false;
        
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