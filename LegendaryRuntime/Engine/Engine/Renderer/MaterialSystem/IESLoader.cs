using System.Globalization;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer.MaterialSystem;

// ChatGPT helped me write this IES profile loading code :)

public class IESProfile
{
    public int VerticalAnglesCount { get; private set; }
    public int HorizontalAnglesCount { get; private set; }
    public List<float> VerticalAngles { get; private set; }
    public List<float> HorizontalAngles { get; private set; }
    public float[,] CandelaValues { get; private set; }

    public int TextureID { get; set; }

    public static IESProfile Load(string filePath)
    {
        // Get the absolute base directory of the executable.
        string basePath = AppContext.BaseDirectory;
       
        string path = Path.Combine(basePath, Path.Combine(Path.Combine("LegendaryRuntime"), "Resources"), "IES Profiles");

        var lines = File.ReadAllLines(Path.Combine(path, filePath));
        int dataIndex = 0;

        // Skip headers until we reach photometric data
        while (dataIndex < lines.Length && !char.IsDigit(lines[dataIndex][0]) && lines[dataIndex] != "TILT=NONE")
            dataIndex++;

        // Skip "TILT=NONE" line
        if (lines[dataIndex] == "TILT=NONE")
            dataIndex++;

        // Read photometric data
        var data = new List<float>();
        for (int i = dataIndex; i < lines.Length; i++)
        {
            var numbers = lines[i].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var num in numbers)
            {
                if (float.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                    data.Add(value);
            }
        }

        // Extract key values based on IES standard format
        int index = 0;
        int numLamps = (int)data[index++];
        float lumensPerLamp = data[index++];
        float candelaMultiplier = data[index++];
        int numVerticalAngles = (int)data[index++];
        int numHorizontalAngles = (int)data[index++];
        int photometricType = (int)data[index++]; // 1: Type C, 2: Type B, 3: Type A
        int unitsType = (int)data[index++];       // 1: Feet, 2: Meters
        float width = data[index++];
        float length = data[index++];
        float height = data[index++];

        // Read angles
        var verticalAngles = new List<float>();
        for (int i = 0; i < numVerticalAngles; i++)
            verticalAngles.Add(data[index++]);

        var horizontalAngles = new List<float>();
        for (int i = 0; i < numHorizontalAngles; i++)
            horizontalAngles.Add(data[index++]);

        // Read candela values
        float[,] candelaValues = new float[numHorizontalAngles, numVerticalAngles];
        for (int h = 0; h < numHorizontalAngles; h++)
        {
            for (int v = 0; v < numVerticalAngles; v++)
            {
                candelaValues[h, v] = data[index++] * candelaMultiplier;
            }
        }

        var profile = new IESProfile
        {
            VerticalAnglesCount = numVerticalAngles,
            HorizontalAnglesCount = numHorizontalAngles,
            VerticalAngles = verticalAngles,
            HorizontalAngles = horizontalAngles,
            CandelaValues = candelaValues,
        };
        
        profile.TextureID = IESTextureLoader.LoadIESTexture(profile);
        
        return profile;
    }
}