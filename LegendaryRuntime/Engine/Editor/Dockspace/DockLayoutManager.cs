using ImGuiNET;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor.Dockspace
{
    public static class DockLayoutManager
    {
        private static string layoutFilename = "editor_layout.ini";

        public static void SetLayoutFilename(string filename)
        {
            layoutFilename = filename;
        }

        public static void LoadLayoutFromDisk()
        {
            if (!File.Exists(layoutFilename))
                return;

            string iniData = File.ReadAllText(layoutFilename);
            ImGui.LoadIniSettingsFromMemory(iniData, (uint)iniData.Length * sizeof(char));

            Console.WriteLine($"[DockLayoutManager] Loaded layout from '{layoutFilename}'.");
        }

        public static void SaveLayoutToDisk()
        {

            string data = ImGui.SaveIniSettingsToMemory(out uint size);
            File.WriteAllText(layoutFilename, data);
            
            Console.WriteLine($"[DockLayoutManager] Saved layout to '{layoutFilename}'.");
        }
    }
}