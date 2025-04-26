using System.Numerics;
using ImGuiNET;
using System.IO;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor
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

            ImGui.SetNextWindowPos(viewport.Pos);
            ImGui.SetNextWindowSize(new Vector2(_window.Size.X, _window.Size.Y));
            ImGui.SetNextWindowViewport(viewport.ID);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
            windowFlags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove;
            windowFlags |= ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNavFocus;

            ImGui.Begin("DockspaceWindow", windowFlags);
            ImGui.PopStyleVar(2);

            dockspaceID = ImGui.GetID("MyDockspace");

            if (true)
            {
                if (ImGuiDockHelper.igDockBuilderGetNode(dockspaceID) == IntPtr.Zero)
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
            ImGuiDockHelper.igDockBuilderRemoveNode(dockspaceID);
            ImGuiDockHelper.igDockBuilderAddNode(dockspaceID, ImGuiDockNodeFlags.None);

            var viewport = ImGui.GetMainViewport();
            ImGuiDockHelper.igDockBuilderSetNodeSize(dockspaceID, viewport.Size);

            uint dockMain = dockspaceID;
            uint dockLeft = ImGuiDockHelper.igDockBuilderSplitNode(dockMain, ImGuiDir.Left, 0.2f, out _, out dockMain);
            uint dockRight = ImGuiDockHelper.igDockBuilderSplitNode(dockMain, ImGuiDir.Right, 0.25f, out dockMain, out _);
            uint dockBottom = ImGuiDockHelper.igDockBuilderSplitNode(dockMain, ImGuiDir.Down, 0.25f, out dockMain, out _);

            ImGuiDockHelper.DockWindow("Viewport", dockMain);
            ImGuiDockHelper.DockWindow("Scene Hierarchy", dockLeft);

            ImGuiDockHelper.igDockBuilderFinish(dockspaceID);
        }

        private void UpdateImGuiViewport()
        {
            var io = ImGui.GetIO();

            io.DisplaySize = new System.Numerics.Vector2(_window.Size.X, _window.Size.Y);
            io.DisplayFramebufferScale = new System.Numerics.Vector2(1, 1);
        }
    }
}
