using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Windows.Forms;

namespace LegitX_V2
{
    public class MouseHook
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;

            public static implicit operator Point(POINT point)
            {
                return new Point(point.x, point.y);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }

            public static implicit operator Rectangle(RECT rect)
            {
                return new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);
            }
        }

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        public class MouseHookEventArgs : EventArgs
        {
            public int MessageType { get; private set; }
            public Point Position { get; private set; }
            public uint MouseData { get; private set; }
            public uint Flags { get; private set; }
            public uint Time { get; private set; }
            public bool Handled { get; set; }

            public MouseHookEventArgs(int messageType, MSLLHOOKSTRUCT hookStruct)
            {
                MessageType = messageType;
                Position = hookStruct.pt;
                MouseData = hookStruct.mouseData;
                Flags = hookStruct.flags;
                Time = hookStruct.time;
                Handled = false;
            }
        }

        public event EventHandler<MouseHookEventArgs> MouseMoved;
        public event EventHandler<MouseHookEventArgs> MouseButtonDown;
        public event EventHandler<MouseHookEventArgs> MouseButtonUp;

        private IntPtr hookHandle = IntPtr.Zero;
        private HookProc hookProcDelegate;

        private bool processingHook = false;

        private IntPtr currentForegroundWindow = IntPtr.Zero;
        private Rectangle currentForegroundRect;
        private bool isFullScreenApp = false;

        public bool IsActive { get { return hookHandle != IntPtr.Zero; } }

        public MouseHook()
        {
            hookProcDelegate = HookCallback;
        }

        public bool Start()
        {
            if (IsActive)
                return true;

            try
            {
                hookHandle = SetWindowsHookEx(WH_MOUSE_LL, hookProcDelegate, GetModuleHandle(null), 0);
                return IsActive;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting mouse hook: {ex.Message}");
                return false;
            }
        }

        public bool Stop()
        {
            if (!IsActive)
                return true;

            try
            {
                bool result = UnhookWindowsHookEx(hookHandle);
                if (result)
                    hookHandle = IntPtr.Zero;
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping mouse hook: {ex.Message}");
                return false;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && !processingHook)
            {
                try
                {
                    processingHook = true;

                    int messageType = wParam.ToInt32();

                    MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

                    CheckForegroundWindow();

                    MouseHookEventArgs args = new MouseHookEventArgs(messageType, hookStruct);

                    if (messageType == WM_MOUSEMOVE)
                    {
                        OnMouseMoved(args);
                    }
                    else if (messageType == WM_LBUTTONDOWN || messageType == WM_RBUTTONDOWN)
                    {
                        OnMouseButtonDown(args);
                    }
                    else if (messageType == WM_LBUTTONUP || messageType == WM_RBUTTONUP)
                    {
                        OnMouseButtonUp(args);
                    }

                    if (args.Handled)
                        return new IntPtr(1);
                }
                finally
                {
                    processingHook = false;
                }
            }

            return CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        private void CheckForegroundWindow()
        {
            IntPtr foregroundWindow = GetForegroundWindow();

            if (foregroundWindow != currentForegroundWindow)
            {
                currentForegroundWindow = foregroundWindow;

                if (foregroundWindow != IntPtr.Zero)
                {
                    RECT windowRect;
                    if (GetWindowRect(foregroundWindow, out windowRect))
                    {
                        currentForegroundRect = windowRect;

                        isFullScreenApp = IsFullScreenApplication(foregroundWindow, windowRect);
                    }
                }
            }
        }

        private bool IsFullScreenApplication(IntPtr hWnd, RECT windowRect)
        {
            if (hWnd == GetShellWindow() || hWnd == GetDesktopWindow())
                return false;

            if (!IsWindowVisible(hWnd) || IsIconic(hWnd))
                return false;

            Screen primaryScreen = Screen.PrimaryScreen;
            Rectangle screenBounds = primaryScreen.Bounds;

            bool coversEntireScreen = (windowRect.Left <= 0 &&
                                      windowRect.Top <= 0 &&
                                      windowRect.Right >= screenBounds.Width &&
                                      windowRect.Bottom >= screenBounds.Height);

            int processId;
            GetWindowThreadProcessId(hWnd, out processId);

            if (coversEntireScreen)
            {
                try
                {
                    using (Process process = Process.GetProcessById(processId))
                    {
                        string processName = process.ProcessName.ToLower();
                        if (processName == "explorer" || processName.Contains("legitx"))
                            return false;
                    }
                }
                catch { }

                return true;
            }

            return false;
        }

        public static Point GetCursorPosition()
        {
            POINT point;
            if (GetCursorPos(out point))
                return point;
            return Point.Empty;
        }

        public static bool SetCursorPosition(int x, int y)
        {
            return SetCursorPos(x, y);
        }

        protected virtual void OnMouseMoved(MouseHookEventArgs e)
        {
            MouseMoved?.Invoke(this, e);
        }

        protected virtual void OnMouseButtonDown(MouseHookEventArgs e)
        {
            MouseButtonDown?.Invoke(this, e);
        }

        protected virtual void OnMouseButtonUp(MouseHookEventArgs e)
        {
            MouseButtonUp?.Invoke(this, e);
        }

        public bool IsFullScreenAppRunning { get { return isFullScreenApp; } }
        public Rectangle ForegroundWindowRect { get { return currentForegroundRect; } }
    }
}