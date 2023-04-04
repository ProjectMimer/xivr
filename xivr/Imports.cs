using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using xivr.Structures;

namespace xivr
{
    public static unsafe class Imports
    {
        [DllImport("kernel32.dll")]
        public static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll")]
        public static extern bool FlushInstructionCache(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string lclassName, string windowTitle);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int ProcessId);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point lpPoint);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(Point lpPoint);

        [DllImport("user32.dll")]
        public static extern bool ScreenToClient(IntPtr hwnd, out Point lpPoint);

        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(IntPtr hwnd, out Point lpPoint);

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hwnd, out Rectangle lpRect);



        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void UpdateConfiguration(Configuration.cfgData config);

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetDX11(IntPtr Device, IntPtr rtManager);

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void UnsetDX11();

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern Structures.Texture* GetUIRenderTexture(int eye);

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Recenter();

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetFramePose();

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void WaitGetPoses();

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern Matrix4x4 GetFramePose(poseType posetype, int eye);

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetThreadedEye(int eye);

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RenderVR();

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RenderUI(bool enableVR, bool enableFloatingHUD, Matrix4x4 curViewMatrixWithoutHMD, Point virtualMouse, bool dalamudMode);

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RenderFloatingScreen(Point virtualMouse, bool dalamudMode);

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetTexture();

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern Point GetBufferSize();

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void ResizeWindow(IntPtr hwnd, int width, int height);

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetActiveJSON([In, MarshalAs(UnmanagedType.LPUTF8Str)] string filePath, int size);

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void UpdateController(UpdateControllerInput controllerCallback);

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void HapticFeedback(ActionButtonLayout side, float time, float freq, float amp);

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetLogFunction(InternalLogging internalLogging);

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRayCoordinate(float* posFrom, float* posTo);
    }
}
