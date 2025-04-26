using System.Runtime.InteropServices;


public static class ImGuizmoBinding
{

    [DllImport("cimguizmo", CallingConvention = CallingConvention.Cdecl)]
    public static extern void ImGuizmo_BeginFrame();

    [DllImport("cimguizmo", CallingConvention = CallingConvention.Cdecl)]
    public static extern void ImGuizmo_Enable([MarshalAs(UnmanagedType.I1)] bool enable);

    [DllImport("cimguizmo", CallingConvention = CallingConvention.Cdecl)]
    public static extern void ImGuizmo_SetRect(float x, float y, float width, float height);
    
    [DllImport("cimguizmo", CallingConvention = CallingConvention.Cdecl)]
    public static extern void ImGuizmo_SetImGuiContext(IntPtr ctx);
    
    [DllImport("cimguizmo", CallingConvention = CallingConvention.Cdecl)]
    public static extern void ImGuizmo_Manipulate(
        float[] view,
        float[] projection,
        int operation,
        int mode,
        float[] matrix,
        float[] deltaMatrix,
        float[] snap,
        float[] localBounds,
        float[] boundsSnap
    );
}