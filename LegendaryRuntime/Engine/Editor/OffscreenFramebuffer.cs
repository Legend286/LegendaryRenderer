using OpenTK.Graphics.OpenGL;
using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor
{
    public class OffscreenFramebuffer : IDisposable
    {
        private int fboHandle;
        private int colorTextureHandle;
        private int depthRenderbufferHandle;
        private int width;
        private int height;

        public int ColorTexture => colorTextureHandle; // Expose for ImGui or direct use

        public OffscreenFramebuffer(int width, int height)
        {
            this.width = width;
            this.height = height;

            // Create FBO
            fboHandle = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fboHandle);

            // Create Color Texture
            colorTextureHandle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, colorTextureHandle);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                            width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                    TextureTarget.Texture2D, colorTextureHandle, 0);

            // Create Depth Renderbuffer
            depthRenderbufferHandle = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depthRenderbufferHandle);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, OpenTK.Graphics.OpenGL.RenderbufferStorage.DepthComponent24,
                                   width, height); // Or DepthComponent16 if preferred
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                                     RenderbufferTarget.Renderbuffer, depthRenderbufferHandle);

            // Check FBO status
            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                throw new Exception($"Framebuffer not complete: {status}");
            }

          //  GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0); // Unbind
        }

        public void Bind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fboHandle);
            GL.Viewport(0, 0, width, height); // Set viewport to FBO size
        }

        public void Unbind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            // Consider restoring original viewport if needed, or ensure caller manages it.
        }
        
        public byte[] GetPixelData()
        {
            Bind(); // Ensure FBO is bound before reading
            byte[] pixels = new byte[width * height * 4]; // RGBA
            GL.ReadPixels(0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            Unbind();
            // ImageSharp expects images typically with origin at top-left, OpenGL bottom-left.
            // This raw data will be bottom-left origin. Saving to PNG needs to handle this.
            return pixels;
        }

        public void SaveAsPng(string path, bool flipVertical = true) // Added flipVertical
        {
            byte[] pixelData = GetPixelData();
            
            // Create ImageSharp image from raw pixel data
            // ImageSharp expects pixel data in top-to-bottom order.
            // OpenGL's ReadPixels provides bottom-to-top. So, we might need to flip it.
            using (Image<Rgba32> image = Image.LoadPixelData<Rgba32>(pixelData, width, height))
            {
                if (flipVertical)
                {
                    image.Mutate(x => x.Flip(FlipMode.Vertical));
                }
                image.SaveAsPng(path);
            }
        }

        public void Dispose()
        {
            GL.DeleteFramebuffer(fboHandle);
            GL.DeleteTexture(colorTextureHandle);
            GL.DeleteRenderbuffer(depthRenderbufferHandle);
            GC.SuppressFinalize(this);
        }

        ~OffscreenFramebuffer()
        {
            // Note: OpenGL calls should not be made in a finalizer thread.
            // This is a fallback. Proper disposal is via Dispose().
            // Consider logging if finalizer is reached, indicating a Dispose() call was missed.
            // For simplicity in this context, we'll leave it, but in robust code, manage GL resources on main thread.
            // If GL context is not current on this thread, these GL.Delete calls would fail or crash.
        }
    }
} 