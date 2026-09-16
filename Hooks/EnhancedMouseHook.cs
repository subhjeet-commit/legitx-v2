using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;

namespace LegitX_V2
{
    public class EnhancedMouseHook
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

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
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

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        public event EventHandler<MouseEventArgs> MouseMoved;
        public event EventHandler<MouseEventArgs> MouseDown;
        public event EventHandler<MouseEventArgs> MouseUp;

        public class MouseEventArgs : EventArgs
        {
            public Point Position { get; }
            public int MessageType { get; }
            public bool Handled { get; set; }

            public MouseEventArgs(Point position, int messageType)
            {
                Position = position;
                MessageType = messageType;
                Handled = false;
            }
        }

        private IntPtr hookHandle = IntPtr.Zero;
        private HookProc hookProcDelegate;
        private System.Windows.Forms.Timer recoveryTimer;
        private System.Windows.Forms.Timer activityMonitorTimer;
        private int recoveryAttempts = 0;
        private readonly int maxRecoveryAttempts = 5;
        private volatile bool isProcessingHook = false;
        private Point lastMousePosition = Point.Empty;
        private DateTime lastMouseActivity = DateTime.Now;
        private readonly object hookLock = new object();

        public bool IsActive { get { return hookHandle != IntPtr.Zero; } }

        public EnhancedMouseHook()
        {
            hookProcDelegate = HookCallback;

            recoveryTimer = new System.Windows.Forms.Timer();
            recoveryTimer.Interval = 5000;
            recoveryTimer.Tick += RecoveryTimer_Tick;

            activityMonitorTimer = new System.Windows.Forms.Timer();
            activityMonitorTimer.Interval = 10000;
            activityMonitorTimer.Tick += ActivityMonitorTimer_Tick;
        }

        public bool Start()
        {
            if (IsActive)
                return true;

            lock (hookLock)
            {
                try
                {
                    recoveryAttempts = 0;

                    IntPtr moduleHandle = GetModuleHandle(null);

                    hookHandle = SetWindowsHookEx(WH_MOUSE_LL, hookProcDelegate, moduleHandle, 0);

                    if (hookHandle == IntPtr.Zero)
                    {
                        int errorCode = Marshal.GetLastWin32Error();
                        Debug.WriteLine($"Failed to set mouse hook. Error code: {errorCode}");

                        if (!recoveryTimer.Enabled)
                            recoveryTimer.Start();

                        return false;
                    }

                    recoveryTimer.Start();
                    activityMonitorTimer.Start();

                    Debug.WriteLine("Mouse hook successfully started");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Exception while starting mouse hook: {ex.Message}");

                    if (!recoveryTimer.Enabled)
                        recoveryTimer.Start();

                    return false;
                }
            }
        }

        public bool Stop()
        {
            if (!IsActive)
                return true;

            lock (hookLock)
            {
                try
                {
                    recoveryTimer.Stop();
                    activityMonitorTimer.Stop();

                    bool result = UnhookWindowsHookEx(hookHandle);

                    if (result)
                    {
                        hookHandle = IntPtr.Zero;
                        Debug.WriteLine("Mouse hook successfully stopped");
                    }
                    else
                    {
                        int errorCode = Marshal.GetLastWin32Error();
                        Debug.WriteLine($"Failed to unhook mouse hook. Error code: {errorCode}");
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Exception while stopping mouse hook: {ex.Message}");

                    hookHandle = IntPtr.Zero;

                    return false;
                }
            }
        }

        private void RecoveryTimer_Tick(object sender, EventArgs e)
        {
            if (!IsActive && recoveryAttempts < maxRecoveryAttempts)
            {
                recoveryAttempts++;
                Debug.WriteLine($"Attempting to recover mouse hook (attempt {recoveryAttempts} of {maxRecoveryAttempts})");
                Start();
            }
        }

        private void ActivityMonitorTimer_Tick(object sender, EventArgs e)
        {
            TimeSpan inactiveTime = DateTime.Now - lastMouseActivity;

            if (inactiveTime.TotalSeconds > 30 && IsActive)
            {
                Debug.WriteLine("Mouse hook appears to be unresponsive. Restarting hook...");

                Stop();
                Thread.Sleep(100);
                Start();

                lastMouseActivity = DateTime.Now;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
                return CallNextHookEx(hookHandle, nCode, wParam, lParam);

            lastMouseActivity = DateTime.Now;

            try
            {
                if (isProcessingHook)
                    return CallNextHookEx(hookHandle, nCode, wParam, lParam);

                isProcessingHook = true;

                int messageType = wParam.ToInt32();

                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                Point mousePosition = new Point(hookStruct.pt.x, hookStruct.pt.y);

                MouseEventArgs args = new MouseEventArgs(mousePosition, messageType);

                switch (messageType)
                {
                    case WM_MOUSEMOVE:
                        OnMouseMoved(args);
                        break;

                    case WM_LBUTTONDOWN:
                    case WM_RBUTTONDOWN:
                        OnMouseDown(args);
                        break;

                    case WM_LBUTTONUP:
                    case WM_RBUTTONUP:
                        OnMouseUp(args);
                        break;
                }

                if (args.Handled)
                    return new IntPtr(1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in hook callback: {ex.Message}");
            }
            finally
            {
                isProcessingHook = false;
            }

            return CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        public static Point GetCursorPosition()
        {
            POINT point;
            if (GetCursorPos(out point))
                return new Point(point.x, point.y);
            return Point.Empty;
        }

        public static bool SetCursorPosition(int x, int y)
        {
            return SetCursorPos(x, y);
        }

        protected virtual void OnMouseMoved(MouseEventArgs e)
        {
            MouseMoved?.Invoke(this, e);
        }

        protected virtual void OnMouseDown(MouseEventArgs e)
        {
            MouseDown?.Invoke(this, e);
        }

        protected virtual void OnMouseUp(MouseEventArgs e)
        {
            MouseUp?.Invoke(this, e);
        }

        public void Dispose()
        {
            Stop();

            if (recoveryTimer != null)
            {
                recoveryTimer.Stop();
                recoveryTimer.Dispose();
                recoveryTimer = null;
            }

            if (activityMonitorTimer != null)
            {
                activityMonitorTimer.Stop();
                activityMonitorTimer.Dispose();
                activityMonitorTimer = null;
            }
        }
    }
}