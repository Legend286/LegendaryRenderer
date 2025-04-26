using LegendaryRenderer.Application;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.IO;

namespace LegendaryRenderer.Shaders;

public class ShaderFile : IDisposable
{
    private int ShaderHandle;
    public bool IsCompiled = false;
    private int VertexShader;
    private int FragmentShader;

    // New fields for hot reloading
    private string vertexShaderPath;
    private string fragmentShaderPath;
    private FileSystemWatcher? vertexWatcher;
    private FileSystemWatcher? fragmentWatcher;
    private DateTime lastReloadTime = DateTime.MinValue;
    private readonly TimeSpan reloadDelay = TimeSpan.FromMilliseconds(1000);

    private bool IsErrorShader = false;
    /*
     * Specify the shader path either with .vert/.frag extension or without. 
     */
    public ShaderFile(string vertex, string fragment, out ShaderManager.ShaderLoadStatus compileStatus, bool ErrorShader = false)
    {
        LoadShader(vertex, fragment, out ShaderManager.ShaderLoadStatus status, ErrorShader);
        IsErrorShader = ErrorShader;
        compileStatus = status;
    }

    void LoadShader(string vertex, string fragment, out ShaderManager.ShaderLoadStatus status, bool ErrorShader = false)
    {
        if (!ErrorShader)
        {
            // Get the absolute base directory of the executable.
            string basePath = AppContext.BaseDirectory;
            // Build your shader folder path using Path.Combine for proper platform independence.
            string shaderFolder = Path.Combine(basePath, "LegendaryRuntime", "Engine", "Engine", "Renderer", "Shaders", "glsl");

            // Combine the shader folder with the given file names.
            vertex = Path.Combine(shaderFolder, vertex);
            fragment = Path.Combine(shaderFolder, fragment);

            // Ensure correct file extensions.
            if (!vertex.EndsWith(".vert", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(Path.GetExtension(vertex)))
                {
                    throw new ArgumentException("Vertex shader file path has a typo...");
                }
                else
                {
                    vertex += ".vert";
                }
            }
            if (!fragment.EndsWith(".frag", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(Path.GetExtension(fragment)))
                {
                    throw new ArgumentException("Fragment shader file path has a typo...");
                }
                else
                {
                    fragment += ".frag";
                }
            }


            // Convert to absolute paths (in case Path.Combine returns a relative path, which is unlikely here).
            vertexShaderPath = Path.GetFullPath(vertex);
            fragmentShaderPath = Path.GetFullPath(fragment);
        }
        
        // Default shader sources for error cases.
        string ErrorVertex =
            @"#version 400 core
            layout(location = 0) in vec3 aPosition;
            layout(location = 1) in vec2 aTexCoord;
            out vec2 texCoord;
            uniform mat4 viewProjection;
            uniform mat4 model;
            void main()
            {    
                vec4 currentPos = vec4(aPosition, 1.0f) * model * viewProjection;
                currentPos.z *= -1;
                texCoord = aTexCoord;
                gl_Position = currentPos;
            }";

        string ErrorFragment =
            @"#version 400 core
            layout(location = 0) out vec4 FragColour;
            in vec2 texCoord;
            void main()
            {
                FragColour = vec4(1,0,1,1); // White color
            }";

        string VertexShaderSource, FragmentShaderSource;
        // Check if shader files exist; if so, read them.
        if (File.Exists(vertexShaderPath) && File.Exists(fragmentShaderPath) && !ErrorShader)
        {
            VertexShaderSource = File.ReadAllText(vertexShaderPath);
            FragmentShaderSource = File.ReadAllText(fragmentShaderPath);
        }
        else
        {
            Console.WriteLine($"Could not load shader files from disk. Searched for:\n Vertex: {vertexShaderPath}\n Fragment: {fragmentShaderPath}\n Defaulting to Error Shader.");
            status = ShaderManager.ShaderLoadStatus.ERROR_LOADING_FROM_DISK;
            // Use default error names for logging.
            VertexShaderSource = ErrorVertex;
            FragmentShaderSource = ErrorFragment;

            vertexShaderPath = "ErrorVertex";
            fragmentShaderPath = "ErrorFragment";
            
        }

        // Create shader objects for vertex and fragment.
        VertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(VertexShader, VertexShaderSource);

        FragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(FragmentShader, FragmentShaderSource);

        if (CompileShader(VertexShader) && CompileShader(FragmentShader))
        {
            ShaderHandle = GL.CreateProgram();

            // Attach shaders and link the program.
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
                status = ShaderManager.ShaderLoadStatus.SUCCESS;
            }
            else
            {
                status = ShaderManager.ShaderLoadStatus.LINK_ERROR;
            }
        }
        else
        {
            status = ShaderManager.ShaderLoadStatus.COMPILE_ERROR;
        }
    }

    private static bool LinkProgram(int program)
    {
        GL.LinkProgram(program);
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out var code);
        if (code != (int)All.True)
        {
            string error = GL.GetProgramInfoLog(program);
            Console.WriteLine($"Shader {program} link failed: \n {error}");
            return false;
        }
        return true;
    }
    
    private static bool CompileShader(int shader)
    {
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out var code);
        if (code != (int)All.True)
        {
            GL.GetShaderInfoLog(shader, out string infoLog);
            Console.WriteLine($"Error occurred whilst compiling Shader({shader}).\n\n{infoLog}");
            return false;
        }
        return true;
    }

    public static ShaderFile GetErrorShader()
    {
        return new ShaderFile("", "", out ShaderManager.ShaderLoadStatus compileStatus, true);
    }

    private static int lastShader = -1;

    public void UseShader()
    {
        lastShader = ShaderHandle;
        Application.Engine.currentShader = this;
        GL.UseProgram(ShaderHandle);
        SetShaderMatrix4x4("view", Application.Engine.ActiveCamera.ViewMatrix); 
        SetShaderMatrix4x4("proj", Application.Engine.ActiveCamera.ProjectionMatrix);
        SetShaderMatrix4x4("viewProjection", IsErrorShader ? Matrix4.Identity : Application.Engine.ActiveCamera.ViewProjectionMatrix);
        if (IsErrorShader)
        {
            SetShaderMatrix4x4("model", Matrix4.Identity);
        }
        // TODO: Some more robustness...
        
        SetShaderMatrix4x4("prevViewProjection", Application.Engine.ActiveCamera.PreviousViewProjectionMatrix);
        SetShaderVector3("cameraPosWS", Application.Engine.ActiveCamera.Transform.Position);
    }

    public int GetAttributeLocation(string attributeName)
    {
        return GL.GetAttribLocation(ShaderHandle, attributeName);
    }

    public int GetUniformLocation(string uniformName)
    {
        return GL.GetUniformLocation(ShaderHandle, uniformName);
    }

    public void SetShaderFloat(string parameterName, float value)
    {
        GL.Uniform1(GetUniformLocation(parameterName), value);
    }

    public void SetShaderInt(string parameterName, int value)
    {
        GL.Uniform1(GetUniformLocation(parameterName), value);
    }

    public void SetShaderUint(string parameterName, uint value)
    {
        GL.Uniform1(GetUniformLocation(parameterName), value);
    }

    public void SetShaderVector3(string parameterName, Vector3 value)
    {
        GL.Uniform3(GetUniformLocation(parameterName), value.X, value.Y, value.Z);
    }

    public void SetShaderVector4(string parameterName, Vector4 value)
    {
        GL.Uniform4(GetUniformLocation(parameterName), value.X, value.Y, value.Z, value.W);
    }

    public void SetShaderMatrix4x4(string parameterName, Matrix4 value, bool transpose = true)
    {
        GL.UniformMatrix4(GetUniformLocation(parameterName), transpose, ref value);
    }

    // Convenience constructor for single-path shaders.
    public ShaderFile(string path)
        : this(path, path, out ShaderManager.ShaderLoadStatus compileStatus)
    { }

    private void OnShaderFileChanged(object sender, FileSystemEventArgs e)
    {
        Console.WriteLine($"FileSystemWatcher fired for: {e.FullPath}");
        DateTime now = DateTime.Now;
        if (now - lastReloadTime < reloadDelay)
        {
            Console.WriteLine("Event ignored due to debounce."); ;
            return;
        }
        lastReloadTime = now;
        Application.Engine.QueueOnMainThread(() =>
        {
            Console.WriteLine($"Detected change in shader file '{e.FullPath}'. Attempting hot reload...");
            ReloadShader();
        });
    }

    private string ReadFileWithRetry(string path, int maxAttempts = 5, int delayMillis = 100)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException)
            {
                System.Threading.Thread.Sleep(delayMillis);
            }
        }
        throw new IOException($"Unable to read file: {path}");
    }

    public void ReloadShader()
    {
        try
        {
            string newVertexSource = ReadFileWithRetry(vertexShaderPath);
            string newFragmentSource = ReadFileWithRetry(fragmentShaderPath);

            int newVertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(newVertexShader, newVertexSource);

            int newFragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(newFragmentShader, newFragmentSource);

            bool vertexCompiled = CompileShader(newVertexShader);
            bool fragmentCompiled = CompileShader(newFragmentShader);
            if (vertexCompiled && fragmentCompiled)
            {
                int newProgram = GL.CreateProgram();
                GL.AttachShader(newProgram, newVertexShader);
                GL.AttachShader(newProgram, newFragmentShader);
                if (LinkProgram(newProgram))
                {
                    Console.WriteLine("Shader hot-reloaded successfully.");
                    GL.DeleteProgram(ShaderHandle);
                    ShaderHandle = newProgram;
                    IsCompiled = true;

                    GL.DetachShader(newProgram, newVertexShader);
                    GL.DetachShader(newProgram, newFragmentShader);
                    GL.DeleteShader(newVertexShader);
                    GL.DeleteShader(newFragmentShader);
                }
                else
                {
                    Console.WriteLine("Shader hot-reload link failed. Keeping the old shader program.");
                    GL.DeleteProgram(newProgram);
                    GL.DeleteShader(newVertexShader);
                    GL.DeleteShader(newFragmentShader);
                }
            }
            else
            {
                Console.WriteLine("Shader hot-reload compile error. Keeping the old shader program.");
                GL.DeleteShader(newVertexShader);
                GL.DeleteShader(newFragmentShader);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception during shader hot-reload: " + ex.Message);
        }
    }

    private bool IsDisposed = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            GL.DeleteProgram(ShaderHandle);
           // vertexWatcher?.Dispose();
          //  fragmentWatcher?.Dispose();
            IsDisposed = true;
        }
    }

    ~ShaderFile()
    {
        if (!IsDisposed)
        {
            Console.WriteLine("GPU Resource Leak! Did you forget to call Dispose()?");
        }
    }

    public int GetUniform(string name)
    {
        return GL.GetUniformLocation(ShaderHandle, name);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
