using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Security.Principal;
using System.Diagnostics;

namespace LegitX_V2
{
    public class OverlaySystem
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private const uint EVENT_SYSTEM_FOREGROUND = 3;
        private const uint WINEVENT_OUTOFCONTEXT = 0;

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        private Form overlayForm;
        private IntPtr eventHook;
        private WinEventDelegate winEventProc;

        private bool isActive = false;
        private bool isCoveringFullScreen = false;

        private Timer positionUpdateTimer;
        private const int DEFAULT_UPDATE_INTERVAL = 50;
        private int updateInterval;

        public OverlaySystem(int updateInterval = DEFAULT_UPDATE_INTERVAL)
        {
            this.updateInterval = updateInterval;
        }

        public void Initialize()
        {
            if (overlayForm != null)
                return;

            try
            {
                overlayForm = new Form
                {
                    ShowInTaskbar = false,
                    FormBorderStyle = FormBorderStyle.None,
                    TopMost = true,
                    Opacity = 0.01,
                    BackColor = Color.Black,
                    Size = Screen.PrimaryScreen.Bounds.Size,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(0, 0)
                };

                typeof(Form).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .SetValue(overlayForm, true, null);

                overlayForm.Load += (sender, e) =>
                {
                    try
                    {
                        IntPtr handle = overlayForm.Handle;
                        int exStyle = GetWindowLong(handle, GWL_EXSTYLE);
                        SetWindowLong(handle, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
                        SetTopmostAndUpdatePosition();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in overlay form load: {ex.Message}");
                    }
                };

                positionUpdateTimer = new Timer
                {
                    Interval = updateInterval,
                    Enabled = false
                };

                positionUpdateTimer.Tick += (sender, e) => UpdateOverlayPosition();

                winEventProc = new WinEventDelegate(WinEventCallback);
                eventHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                    IntPtr.Zero, winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);

                overlayForm.Show();
                isActive = true;
                positionUpdateTimer.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing overlay: {ex.Message}");
                Dispose();
            }
        }

        public void Activate()
        {
            if (!isActive && overlayForm != null)
            {
                try
                {
                    overlayForm.Show();
                    SetTopmostAndUpdatePosition();
                    isActive = true;
                    positionUpdateTimer.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error activating overlay: {ex.Message}");
                }
            }
            else if (overlayForm == null)
            {
                Initialize();
            }
        }

        public void Deactivate()
        {
            if (isActive && overlayForm != null)
            {
                try
                {
                    positionUpdateTimer.Stop();
                    overlayForm.Hide();
                    isActive = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deactivating overlay: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            try
            {
                if (positionUpdateTimer != null)
                {
                    positionUpdateTimer.Stop();
                    positionUpdateTimer.Dispose();
                    positionUpdateTimer = null;
                }

                if (eventHook != IntPtr.Zero)
                {
                    UnhookWinEvent(eventHook);
                    eventHook = IntPtr.Zero;
                }

                if (overlayForm != null)
                {
                    overlayForm.Close();
                    overlayForm.Dispose();
                    overlayForm = null;
                }

                isActive = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing overlay: {ex.Message}");
            }
        }

        private bool IsRunningAsAdmin()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        public void SetPerformanceLevel(int level)
        {
            level = Math.Max(0, Math.Min(10, level));

            updateInterval = 200 - (level * 19);

            if (positionUpdateTimer != null)
            {
                positionUpdateTimer.Interval = updateInterval;
            }
        }

        private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType == EVENT_SYSTEM_FOREGROUND && isActive)
            {
                UpdateOverlayPosition();
            }
        }

        private void UpdateOverlayPosition()
        {
            if (!isActive || overlayForm == null || !overlayForm.IsHandleCreated)
                return;

            try
            {
                IntPtr foregroundWnd = GetForegroundWindow();
                if (foregroundWnd == IntPtr.Zero || foregroundWnd == overlayForm.Handle)
                    return;

                RECT windowRect;
                if (!GetWindowRect(foregroundWnd, out windowRect))
                    return;

                Screen currentScreen = Screen.FromHandle(foregroundWnd);
                bool isFullScreen = IsFullScreenWindow(foregroundWnd, windowRect, currentScreen);

                if (isFullScreen)
                {
                    overlayForm.Invoke(new Action(() => {
                        try
                        {
                            overlayForm.Bounds = new Rectangle(
                                windowRect.Left,
                                windowRect.Top,
                                windowRect.Right - windowRect.Left,
                                windowRect.Bottom - windowRect.Top
                            );
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error updating bounds: {ex.Message}");
                        }
                    }));

                    if (!isCoveringFullScreen)
                    {
                        SetTopmostAndUpdatePosition();
                        isCoveringFullScreen = true;
                    }
                }
                else if (isCoveringFullScreen)
                {
                    overlayForm.Invoke(new Action(() => {
                        try
                        {
                            overlayForm.Bounds = Screen.PrimaryScreen.Bounds;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error updating bounds: {ex.Message}");
                        }
                    }));
                    isCoveringFullScreen = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating overlay position: {ex.Message}");
            }
        }

        private void SetTopmostAndUpdatePosition()
        {
            if (overlayForm == null || !overlayForm.IsHandleCreated)
                return;

            try
            {
                SetWindowPos(
                    overlayForm.Handle,
                    HWND_TOPMOST,
                    0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW
                );

                overlayForm.Invoke(new Action(() => {
                    try
                    {
                        overlayForm.Bounds = Screen.PrimaryScreen.Bounds;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error updating bounds: {ex.Message}");
                    }
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting topmost: {ex.Message}");
            }
        }

        private bool IsFullScreenWindow(IntPtr hWnd, RECT windowRect, Screen screen)
        {
            if (hWnd == IntPtr.Zero)
                return false;

            try
            {
                return (windowRect.Left <= screen.Bounds.Left &&
                        windowRect.Top <= screen.Bounds.Top &&
                        windowRect.Right >= screen.Bounds.Right &&
                        windowRect.Bottom >= screen.Bounds.Bottom);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking fullscreen: {ex.Message}");
                return false;
            }
        }

        public bool IsActive => isActive;

        public bool IsCoveringFullScreenApp => isCoveringFullScreen;

        public void ApplySettings(bool persistAcrossFullscreen)
        {
            try
            {
                if (persistAcrossFullscreen)
                {
                    Activate();
                }
                else
                {
                    Deactivate();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying settings: {ex.Message}");
            }
        }
    }
}