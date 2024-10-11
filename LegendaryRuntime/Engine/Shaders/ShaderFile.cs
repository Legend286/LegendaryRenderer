using System.Net;
using System.Numerics;
using OpenTK.Graphics.OpenGL;

namespace LegendaryRenderer.Shaders;

public class ShaderFile : IDisposable
{
    private int ShaderHandle;
    public bool IsCompiled = false;
    private int VertexShader;
    private int FragmentShader;
    
    /*
     * Specify the shader path either with .vert/.frag extension or without. 
     */
    public ShaderFile(string vertex, string fragment, out ShaderManager.ShaderLoadStatus compileStatus,
        bool ErrorShader = false)
    {
        string VertexShaderSource =
            "#version 330 core\n"
            + "layout (location = 0) in vec3 aPosition;\n\n"

            + "uniform mat4 model;\n"
            + "uniform mat4 viewProjection;\n"
            + "void main()\n"
            + "{\n"
            + "   gl_Position = vec4(aPosition, 1.0f) * model * viewProjection;\n"
            + "}\n";

        // error fragment
        string FragmentShaderSource =
            "#version 330 core\n" +
            "out vec4 FragColor;\n\n" +

            "void main()\n"
            + "{\n"
            + "    FragColor = vec4(1.0f, 0.0f, 1.0f, 1.0f);\n"
            + "}\n";

        vertex = "LegendaryRuntime/Engine/Shaders/glsl/" + vertex;
        fragment = "LegendaryRuntime/Engine/Shaders/glsl/" + fragment;
        
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

        


        if (File.Exists(vertex) && File.Exists(fragment) && !ErrorShader)
        {
            VertexShaderSource = File.ReadAllText(vertex);
            FragmentShaderSource = File.ReadAllText(fragment);
        }
        else
        {
            Console.WriteLine($"Could not load {vertex} and {fragment} files from disk, are they valid? Defaulting to Error Shader.");
            compileStatus = ShaderManager.ShaderLoadStatus.ERROR_LOADING_FROM_DISK;
            vertex = "ErrorVertex";
            fragment = "ErrorFragment";
        }

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

                GL.DetachShader(ShaderHandle, VertexShader);
                GL.DetachShader(ShaderHandle, FragmentShader);
                GL.DeleteShader(VertexShader);
                GL.DeleteShader(FragmentShader);
                compileStatus = ShaderManager.ShaderLoadStatus.SUCCESS;
            }
            else
            {
                compileStatus = ShaderManager.ShaderLoadStatus.LINK_ERROR;
            }
        }
        else
        {
            compileStatus = ShaderManager.ShaderLoadStatus.COMPILE_ERROR;
        }
    }

    private static bool LinkProgram(int program)
    {
        // We link the program
        GL.LinkProgram(program);

        // Check for linking errors
        GL.GetProgrami(program, ProgramProperty.LinkStatus, out var code);
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
        GL.GetShaderi(shader, ShaderParameterName.CompileStatus, out var code);
        if (code != (int)All.True)
        {
            // We can use `GL.GetShaderInfoLog(shader)` to get information about the error.
            GL.GetShaderInfoLog(shader, out string infoLog);
            Console.WriteLine($"Error occurred whilst compiling Shader({shader}).\n\n{infoLog}");
            return false;
        }

        return true;
    }

    public static ShaderFile GetErrorShader()
    {
        return new ShaderFile("", "", out ShaderManager.ShaderLoadStatus compileStatus,true);
    }
    
    public void UseShader()
    {
        GL.UseProgram(ShaderHandle);
    }

    public int GetAttributeLocation(string attributeName)
    {
        int location = GL.GetAttribLocation(ShaderHandle, attributeName);
       // Console.WriteLine($"Attribute location {location}.");
        return location;
    }

    public void SetShaderFloat(string parameterName, float value)
    {
        int location = GetAttributeLocation(parameterName);
        GL.Uniform1d(location, value);
    }

    public void SetShaderVector3(string parameterName, Vector3 value)
    {
        int location = GetAttributeLocation(parameterName);
        GL.Uniform3f(location, value.X, value.Y, value.Z);
    }

    public ShaderFile(string path): this(path, path, out ShaderManager.ShaderLoadStatus compileStatus)
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

    public int GetUniform(string name)
    {
        int location = GL.GetUniformLocation(ShaderHandle, name);
        return location;
    }
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}