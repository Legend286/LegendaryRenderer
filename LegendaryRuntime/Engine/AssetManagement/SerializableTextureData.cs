using System.IO;

namespace LegendaryRenderer.LegendaryRuntime.Engine.AssetManagement
{
    // Enum to represent the pixel formats we are caching
    public enum CachedTexturePixelFormat : byte
    {
        Unknown = 0,
        Rgba32 = 1,  // Corresponds to ImageSharp's Rgba32, typically GL_RGBA8, GL_RGBA, GL_UNSIGNED_BYTE
        Rgb48 = 2    // Corresponds to ImageSharp's Rgb48, typically GL_RGB16F, GL_RGB, GL_UNSIGNED_SHORT (for 16-bit integer components)
    }

    public class SerializableTextureData
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public CachedTexturePixelFormat PixelFormat { get; private set; }
        public byte[] PixelData { get; private set; }

        // Private constructor for deserialization
        private SerializableTextureData() { }

        public SerializableTextureData(int width, int height, CachedTexturePixelFormat format, byte[] data)
        {
            Width = width;
            Height = height;
            PixelFormat = format;
            PixelData = data ?? throw new System.ArgumentNullException(nameof(data));
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Width);
            writer.Write(Height);
            writer.Write((byte)PixelFormat);
            writer.Write(PixelData.Length);
            writer.Write(PixelData);
        }

        public static SerializableTextureData Deserialize(BinaryReader reader)
        {
            var texData = new SerializableTextureData();
            texData.Width = reader.ReadInt32();
            texData.Height = reader.ReadInt32();
            texData.PixelFormat = (CachedTexturePixelFormat)reader.ReadByte();
            int dataLength = reader.ReadInt32();
            texData.PixelData = reader.ReadBytes(dataLength);
            return texData;
        }
    }
} 