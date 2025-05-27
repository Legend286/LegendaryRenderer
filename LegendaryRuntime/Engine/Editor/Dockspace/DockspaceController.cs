using System.Numerics;
using ImGuiNET;
using LegendaryRenderer.LegendaryRuntime.Engine.Editor.Dockspace.Utils;
using OpenTK.Windowing.Desktop;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor.Dockspace
{
    public class DockspaceController
    {
        private bool bInitialized = false;
        private uint dockspaceID;
        private bool dockspaceBuilt = false;
        private const string LayoutFile = "layout.ini";

        private NativeWindow _window;

        public DockspaceController(NativeWindow window)
        {
            _window = window;
        }

        public void BeginDockspace()
        {
            UpdateImGuiViewport(); // <-- Very important

            ImGuiWindowFlags windowFlags = ImGuiWindowFlags.MenuBar | ImGuiWindowFlags.NoDocking;
            ImGuiViewportPtr viewport = ImGui.GetMainViewport();

            ImGuiViewportPtr mainViewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(mainViewport.Pos);
            ImGui.SetNextWindowSize(mainViewport.Size);
            ImGui.SetNextWindowViewport(mainViewport.ID);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
            windowFlags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove;
            windowFlags |= ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNavFocus;

            ImGui.Begin("DockspaceWindow", windowFlags);
            ImGui.PopStyleVar(2);

            // --- BEGIN ADDED MENU BAR ---
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    // Placeholder for File menu items
                    ImGui.MenuItem("Open...", "Ctrl+O"); // Example
                    ImGui.MenuItem("Save", "Ctrl+S");    // Example
                    ImGui.Separator();
                    ImGui.MenuItem("Exit");
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Developer"))
                {
                    if (ImGui.MenuItem("Reload All Shaders"))
                    {
                        LegendaryRenderer.Shaders.ShaderManager.ReloadAllShaders();
                    }
                    // Potentially other developer tools can be added here
                    ImGui.EndMenu();
                }
                ImGui.EndMainMenuBar();
            }
            // --- END ADDED MENU BAR ---

            dockspaceID = ImGui.GetID("MyDockspace");

            if (true)
            {
                if (ImGuiDockBinding.igDockBuilderGetNode(dockspaceID) == IntPtr.Zero)
                {
                    SetupDockspaceLayout();
                }

                dockspaceBuilt = true;
            }

            ImGui.DockSpace(dockspaceID, Vector2.Zero, ImGuiDockNodeFlags.None);
        }

        public void EndDockspace()
        {
            ImGui.End();
        }

        private void SetupDockspaceLayout()
        {
            ImGuiDockBinding.igDockBuilderRemoveNode(dockspaceID);
            ImGuiDockBinding.igDockBuilderAddNode(dockspaceID, ImGuiDockNodeFlags.None);

            var viewport = ImGui.GetMainViewport();
            ImGuiDockBinding.igDockBuilderSetNodeSize(dockspaceID, viewport.Size);

            uint dockMain = dockspaceID;
            uint dockLeft = ImGuiDockBinding.igDockBuilderSplitNode(dockMain, ImGuiDir.Left, 0.1f, out dockMain, out dockMain);
            uint dockRight = ImGuiDockBinding.igDockBuilderSplitNode(dockMain, ImGuiDir.Right, 0.1f, out dockMain, out dockMain);
            uint dockBottom = ImGuiDockBinding.igDockBuilderSplitNode(dockMain, ImGuiDir.Down, 0.1f, out dockMain, out dockMain);

            ImGuiDockBinding.DockWindow("Viewport", dockMain);
            ImGuiDockBinding.DockWindow("SceneHierarchy", dockLeft);
            ImGuiDockBinding.DockWindow("Inspector", dockRight);
            ImGuiDockBinding.DockWindow("ContentBrowser", dockBottom);

            ImGuiDockBinding.igDockBuilderFinish(dockspaceID);
        }

        private void UpdateImGuiViewport()
        {
            var io = ImGui.GetIO();

            io.DisplaySize = new System.Numerics.Vector2(_window.Size.X, _window.Size.Y);
            io.DisplayFramebufferScale = new System.Numerics.Vector2(1, 1);
        }
    }
}
