using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor;

public static class ImGuiDockHelper
{
    private const string ImGuiNativeLib = "cimgui"; // Standard cimgui backend library

    [DllImport(ImGuiNativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void igDockBuilderRemoveNode(uint node_id);

    [DllImport(ImGuiNativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void igDockBuilderAddNode(uint node_id, ImGuiDockNodeFlags flags);

    [DllImport(ImGuiNativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void igDockBuilderSetNodeSize(uint node_id, Vector2 size);

    [DllImport(ImGuiNativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint igDockBuilderSplitNode(uint node_id, ImGuiDir split_direction, float size_ratio_for_node_at_dir, out uint out_id_at_dir, out uint out_id_at_opposite_dir);

    [DllImport(ImGuiNativeLib, CallingConvention = CallingConvention.Cdecl)]
    public unsafe static extern void igDockBuilderDockWindow(byte* window_name, uint node_id);

    [DllImport(ImGuiNativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void igDockBuilderFinish(uint node_id);

    [DllImport(ImGuiNativeLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr igDockBuilderGetNode(uint node_id);
    
    public static unsafe void DockWindow(string windowName, uint node_id)
    {
        fixed (byte* ptr = System.Text.Encoding.UTF8.GetBytes(windowName + '\0'))
        {
            igDockBuilderDockWindow(ptr, node_id);
        }
    }
}
// This is a C# wrapper for the ImGui docking API. It provides methods to create and manage docking nodes,