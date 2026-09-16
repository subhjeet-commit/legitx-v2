using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.Win32;
using LegitX_V2.Properties;
using Guna.UI2.WinForms;
using System.Reflection;
using System.Security.Cryptography;
using System.Net;
using System.Security.Principal;
using System.Speech.Synthesis;
using System.IO;
using Microsoft.VisualBasic;
using System.Net.Http;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using System.Management;
using System.Threading;
using System.Timers;
using System.Security;
using Svg;

namespace LegitX_V2
{

    public partial class LegitX : Form
    {
        private Point lastMousePosition = Point.Empty;
        private bool processingMouseMove = false;

        private Dictionary<string, MouseSettings> perAppSettings = new Dictionary<string, MouseSettings>();
        private string currentAppName = string.Empty;
        private bool persistAcrossFullscreen = true;
        private bool syncingPersistToggles;

        private System.Windows.Forms.Timer settingsMonitorTimer;
        private bool isFullScreenAppRunning = false;
        private Dictionary<string, Point> appPositions = new Dictionary<string, Point>();
        private Dictionary<string, DateTime> lastPositionUpdate = new Dictionary<string, DateTime>();

        private const int WM_HOTKEY = 0x0312;
        private const int MOD_NOREPEAT = 0x4000;
        private const int INSERT_KEY = 0x2D;

        private const int HOTKEY_ID = 9000;

        private const string REGISTRY_KEY = @"SOFTWARE\LegitX V2";

        private const int ShellContentMarginX = 9;
        private const int ShellContentMarginTop = 12;
        private const int ShellGapAboveTabRow = 8;
        private const int ShellContentBottomPadding = 6;

        private Dictionary<string, int> defaultValues = new Dictionary<string, int>();

        private bool isActive = false;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private bool isFormHidden = false;

        private bool mouseHookActive = false;
        private IntPtr mouseHookHandle = IntPtr.Zero;
        private NativeMethods.MouseHookCallback mouseHookProcDelegate;
        private System.Windows.Forms.Timer hookMaintainTimer;

        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;

        private Rectangle displayResolution;
        private Rectangle emulatorResolution;
        private bool resolutionScalingEnabled = false;

        private const int DisplayWidthMin = 800;
        private const int DisplayWidthMax = 3840;
        private const int DisplayHeightMin = 600;
        private const int DisplayHeightMax = 2160;
        private const int EmulatorWidthMin = 800;
        private const int EmulatorWidthMax = 2560;
        private const int EmulatorHeightMin = 600;
        private const int EmulatorHeightMax = 1440;

        private ToolTip _uiToolTip;

        private const string CreditsDiscordInviteUrl = "https://discord.gg/Vs9sM8cZw5";
        private const string CreditsDiscordSvgResource = "LegitX_V2.discord_credits.svg";

        public LegitX()
        {
            InitializeComponent();
            try
            {
                typeof(Control).InvokeMember("DoubleBuffered",
                    BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                    null, this, new object[] { true });
            }
            catch { }

            this.Load += new EventHandler(LegitX_Load);
            this.TopMost = true;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            MinimumSize = MaximumSize = Size;

            InitializeTrackBars();
            InitializeResolutions();
            LoadSettingsFromRegistry();

            WireResolutionValueLabels();
            this.Resize += LegitX_ResizeLayout;
            LayoutBottomChrome();
        }

        private void LegitX_ResizeLayout(object sender, EventArgs e)
        {
            LayoutBottomChrome();
        }

        private void LayoutBottomChrome()
        {
            if (ClientSize.Width < 200 || ClientSize.Height < 200)
                return;

            int w = ClientSize.Width;
            int h = ClientSize.Height;
            const int padX = 14;
            const int padBottom = 10;
            int tabY = h - padBottom - mainpanel.Height;
            mainpanel.Location = new Point(padX, tabY);
            optimizationpanel.Location = new Point(padX + mainpanel.Width + 10, tabY);
            extrapanel.Location = new Point(padX + mainpanel.Width + 10 + optimizationpanel.Width + 10, tabY);

            int contentW = Math.Max(400, w - 2 * ShellContentMarginX);
            int contentH = Math.Max(480, tabY - ShellGapAboveTabRow - ShellContentMarginTop);
            Rectangle shell = new Rectangle(ShellContentMarginX, ShellContentMarginTop, contentW, contentH);
            mainpanel1.Bounds = shell;
            optpanel1.Bounds = shell;
            extrapanel1.Bounds = shell;
            mainpanel2.Bounds = shell;

            LayoutMainTabStretchedContent();
            LayoutOptimizationTabStretchedContent();
            LayoutExtraTabStretchedContent();
            LayoutAiTabStretchedContent();

            creditsDevLabel.PerformLayout();
            creditsDiscordIcon.PerformLayout();

            const int gapTextIcon = 10;
            int rowHeight = Math.Max(creditsDevLabel.Height, creditsDiscordIcon.Height);
            int yRow = h - padBottom - rowHeight;

            int discordLeft = w - padX - creditsDiscordIcon.Width;
            int discordY = yRow + (rowHeight - creditsDiscordIcon.Height) / 2;
            creditsDiscordIcon.Location = new Point(discordLeft, discordY);

            int textY = yRow + (rowHeight - creditsDevLabel.Height) / 2;
            creditsDevLabel.Location = new Point(discordLeft - gapTextIcon - creditsDevLabel.Width, textY);

            creditsDevLabel.BringToFront();
            creditsDiscordIcon.BringToFront();
        }

        private void LayoutMainTabStretchedContent()
        {
            if (mainpanel1.ClientSize.Height < 80)
                return;

            int bottom = mainpanel1.ClientSize.Height - ShellContentBottomPadding;
            int innerRight = main3.Left - main2.Left - 6;
            if (innerRight >= 400)
                main2.Width = innerRight;

            int main2Height = bottom - main2.Top;
            if (main2Height >= 215)
                main2.Height = main2Height;

            main4.Top = bottom - main4.Height;
            main4.Left = main3.Left;

            int main3Height = bottom - main3.Top;
            if (main3Height >= 200)
                main3.Height = main3Height;

            int minLeftMain3 = main2.Right + 6;
            int preferredMain3Left = mainpanel1.ClientSize.Width - main3.Width - 3;
            main3.Left = Math.Max(minLeftMain3, preferredMain3Left);
        }

        private void LayoutOptimizationTabStretchedContent()
        {
            if (optpanel1.ClientSize.Height < 80)
                return;

            int bottom = optpanel1.ClientSize.Height - ShellContentBottomPadding;
            int h4 = bottom - guna2Panel4.Top;
            if (h4 >= 120)
                guna2Panel4.Height = h4;
        }

        private void LayoutExtraTabStretchedContent()
        {
            if (extrapanel1.ClientSize.Height < 120)
                return;

            const int hMargin = 6;
            const int gap = 8;
            int bottom = extrapanel1.ClientSize.Height - ShellContentBottomPadding;
            int fullW = Math.Max(200, extrapanel1.ClientSize.Width - 2 * hMargin);

            int aimH = guna2Panel7.Height;
            int aimTop = bottom - aimH;
            guna2Panel7.SetBounds(hMargin, aimTop, fullW, aimH);

            if (guna2Separator14 != null)
                guna2Separator14.Width = Math.Max(160, guna2Panel7.ClientSize.Width - 36);
            if (label49 != null)
                label49.Left = Math.Max(6, (guna2Panel7.ClientSize.Width - label49.Width) / 2);

            int panel8Bottom = aimTop - gap;
            int h8 = panel8Bottom - guna2Panel8.Top;
            if (h8 >= 100)
                guna2Panel8.Height = h8;
            guna2Panel8.SetBounds(hMargin, guna2Panel8.Top, fullW, guna2Panel8.Height);

            if (guna2Separator12 != null)
                guna2Separator12.Width = Math.Max(160, guna2Panel8.ClientSize.Width - 36);
            if (guna2Separator15 != null)
                guna2Separator15.Width = Math.Max(160, guna2Panel8.ClientSize.Width - 36);

            guna2Panel8.SendToBack();
            guna2Panel7.BringToFront();
        }

        private void LayoutAiTabStretchedContent()
        {
            if (mainpanel2.ClientSize.Height < 80 || guna2Panel6 == null || guna2Panel9 == null)
                return;

            const int hMargin = 10;
            const int stackGap = 16;
            const int padVT = 20;
            const int padVB = 22;
            const int gapLabelToField = 16;
            const int rowGap = 9;
            const int fieldWFixed = 204;
            const int toolbarMinPanelW = 208;
            const int panelSideBudget = 56;
            const double minPanelWidthFractionOfTab = 0.72;
            const int fieldWMax = 252;
            const int tbBottomPad = 3;

            (Label lbl, Guna2TextBox fld)[] rows =
            {
                (label38, generalsensi),
                (label39, sensilimit),
                (label42, offsetsensivity),
                (label45, baselinescalex),
                (label44, baselinescaley),
                (label43, subsequentscalx),
                (label50, subsequentscaly),
                (label47, acceleration),
            };

            int maxLabelW = 0;
            foreach ((Label lbl, _) in rows)
            {
                if (lbl == null)
                    continue;
                Size sz = TextRenderer.MeasureText(lbl.Text, lbl.Font, new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.SingleLine);
                maxLabelW = Math.Max(maxLabelW, sz.Width);
            }

            int innerContentW = maxLabelW + gapLabelToField + fieldWFixed;
            int contentFloor = innerContentW + panelSideBudget;
            int availMaxW = mainpanel2.ClientSize.Width - 2 * hMargin;
            int minFromTab = (int)Math.Round(availMaxW * minPanelWidthFractionOfTab);
            int panelW = Math.Max(Math.Max(toolbarMinPanelW, contentFloor), minFromTab);
            panelW = Math.Min(panelW, availMaxW);

            int fieldW = panelW - panelSideBudget - maxLabelW - gapLabelToField;
            fieldW = Math.Max(120, Math.Min(fieldWMax, fieldW));

            int centerX = hMargin + Math.Max(0, (availMaxW - panelW) / 2);
            int innerBottom = mainpanel2.ClientSize.Height - ShellContentBottomPadding;

            guna2Panel6.SuspendLayout();
            guna2Panel6.SetBounds(centerX, hMargin, panelW, 400);
            int sensH = LayoutAiSensitivityRows(rows, padVT, padVB, rowGap, panelW, maxLabelW, gapLabelToField,
                fieldW);
            guna2Panel6.Height = sensH;
            guna2Panel6.ResumeLayout(false);

            guna2Panel9.SuspendLayout();
            guna2Panel9.SetBounds(centerX, hMargin, panelW, 150);
            guna2Panel9.ResumeLayout(false);
            LayoutAiToolbarInPanel();

            int tbH = ToolbarPanelContentBottom(guna2Panel9) + tbBottomPad;
            tbH = Math.Max(82, Math.Min(150, tbH));

            int groupH = sensH + stackGap + tbH;
            int availTop = hMargin;
            int groupTop = availTop + Math.Max(0, (innerBottom - availTop - groupH) / 2);
            int sensY = groupTop;
            int toolbarY = groupTop + sensH + stackGap;

            if (toolbarY + tbH > innerBottom)
            {
                toolbarY = innerBottom - tbH;
                sensY = Math.Max(hMargin, toolbarY - stackGap - sensH);
            }

            guna2Panel6.SetBounds(centerX, sensY, panelW, sensH);
            guna2Panel9.SetBounds(centerX, toolbarY, panelW, tbH);

            LayoutAiSensitivityRows(rows, padVT, padVB, rowGap, panelW, maxLabelW, gapLabelToField, fieldW);
            LayoutAiToolbarInPanel();

            int tbH2 = ToolbarPanelContentBottom(guna2Panel9) + tbBottomPad;
            tbH2 = Math.Max(82, Math.Min(150, tbH2));
            if (tbH2 != tbH)
            {
                tbH = tbH2;
                groupH = sensH + stackGap + tbH;
                groupTop = availTop + Math.Max(0, (innerBottom - availTop - groupH) / 2);
                sensY = groupTop;
                toolbarY = groupTop + sensH + stackGap;
                if (toolbarY + tbH > innerBottom)
                {
                    toolbarY = innerBottom - tbH;
                    sensY = Math.Max(hMargin, toolbarY - stackGap - sensH);
                }

                guna2Panel6.SetBounds(centerX, sensY, panelW, sensH);
                guna2Panel9.SetBounds(centerX, toolbarY, panelW, tbH);
                LayoutAiToolbarInPanel();
            }
        }

        private static int ToolbarPanelContentBottom(Guna2Panel panel)
        {
            if (panel == null || panel.Controls.Count == 0)
                return 0;

            int bottom = 0;
            foreach (Control c in panel.Controls)
            {
                if (!c.Visible)
                    continue;

                bottom = Math.Max(bottom, c.Top + c.Height);
            }

            return bottom;
        }

        /// <summary>
        /// Centered sensitivity block: label column (right-aligned) + field, grouped in the middle of the panel.
        /// </summary>
        private int LayoutAiSensitivityRows(
            (Label lbl, Guna2TextBox fld)[] rows,
            int padVT,
            int padVB,
            int rowGap,
            int panelW,
            int maxLabelW,
            int gapLabelToField,
            int fieldW)
        {
            int innerW = maxLabelW + gapLabelToField + fieldW;
            int x0 = Math.Max(6, (panelW - innerW) / 2);
            int y = padVT;

            for (int i = 0; i < rows.Length; i++)
            {
                (Label lbl, Guna2TextBox fld) = rows[i];
                if (lbl == null || fld == null)
                    continue;

                lbl.AutoSize = true;

                Size textM = TextRenderer.MeasureText(lbl.Text, lbl.Font, new Size(panelW, 2000),
                    TextFormatFlags.SingleLine);
                int lineH = Math.Max(Math.Max(textM.Height + 2, fld.Height), 26);

                lbl.Left = x0 + maxLabelW - lbl.Width;
                lbl.Top = y + (lineH - lbl.Height) / 2;

                fld.Width = fieldW;
                fld.Left = x0 + maxLabelW + gapLabelToField;
                fld.Top = y + (lineH - fld.Height) / 2;

                y += lineH + rowGap;
            }

            return y - rowGap + padVB;
        }

        private void LayoutAiToolbarInPanel()
        {
            if (guna2Panel9 == null || guna2Panel9.ClientSize.Width < 80)
                return;

            int cw = guna2Panel9.ClientSize.Width;
            int ch = guna2Panel9.ClientSize.Height;

            const int topBtnW = 36;
            const int topBtnH = 42;
            const int topGap = 7;
            const int imgMain = 22;
            const int imgRefresh = 26;
            const int backW = 62;
            const int backH = 36;
            const int imgBack = 30;

            void SizeTopImageButton(Guna2ImageButton b, int imgDim)
            {
                b.Size = new Size(topBtnW, topBtnH);
                Size s = new Size(imgDim, imgDim);
                b.ImageSize = s;
                b.HoverState.ImageSize = s;
                b.PressedState.ImageSize = s;
                b.CheckedState.ImageSize = new Size(Math.Min(48, imgDim + 22), Math.Min(48, imgDim + 22));
            }

            SizeTopImageButton(playbutton, imgMain);
            SizeTopImageButton(pausebutton2, imgMain);
            SizeTopImageButton(savebutton2, imgMain);
            SizeTopImageButton(refreshbutton2, imgRefresh);

            int topRowW = topBtnW * 3 + topGap * 2;
            int startX = Math.Max(4, (cw - topRowW) / 2);
            const int topRowY = 4;

            playbutton.SetBounds(startX, topRowY, topBtnW, topBtnH);
            pausebutton2.SetBounds(startX, topRowY, topBtnW, topBtnH);
            savebutton2.SetBounds(startX + topBtnW + topGap, topRowY, topBtnW, topBtnH);
            refreshbutton2.SetBounds(startX + 2 * (topBtnW + topGap), topRowY, topBtnW, topBtnH);

            backbutton2.Size = new Size(backW, backH);
            Size backImg = new Size(imgBack, imgBack);
            backbutton2.ImageSize = backImg;
            backbutton2.HoverState.ImageSize = backImg;
            backbutton2.PressedState.ImageSize = backImg;

            int row1Bottom = topRowY + topBtnH;
            backbutton2.Left = Math.Max(0, (cw - backbutton2.Width) / 2);
            backbutton2.Top = row1Bottom + 3;

            if (playbutton1 != null)
            {
                playbutton1.AutoSize = true;
                playbutton1.Left = Math.Max(6, (cw - playbutton1.Width) / 2);
                int wantY = backbutton2.Bottom + 2;
                playbutton1.Top = Math.Max(wantY, ch - playbutton1.Height - 2);
            }
        }

        private static readonly Color ResolutionChipBg = Color.FromArgb(34, 34, 38);
        private static readonly Color ResolutionChipBgHover = Color.FromArgb(48, 48, 54);

        private void WireResolutionValueLabels()
        {
            foreach (Label lbl in new[] { doswidth, dosheight, eoswidth, eosheight })
            {
                lbl.Click += ResolutionValueLabel_Click;
                lbl.MouseEnter += ResolutionValueChip_MouseEnter;
                lbl.MouseLeave += ResolutionValueChip_MouseLeave;
            }
        }

        private void ResolutionValueChip_MouseEnter(object sender, EventArgs e)
        {
            if (sender is Label l)
                l.BackColor = ResolutionChipBgHover;
        }

        private void ResolutionValueChip_MouseLeave(object sender, EventArgs e)
        {
            if (sender is Label l)
                l.BackColor = ResolutionChipBg;
        }

        private void ResolutionValueLabel_Click(object sender, EventArgs e)
        {
            if (!(sender is Label lbl))
                return;

            string title;
            string prompt;
            int min;
            int max;
            Guna2TrackBar track;

            if (lbl == doswidth)
            {
                title = "Display width";
                prompt = "Width (pixels):";
                min = DisplayWidthMin;
                max = DisplayWidthMax;
                track = dossensi1;
            }
            else if (lbl == dosheight)
            {
                title = "Display height";
                prompt = "Height (pixels):";
                min = DisplayHeightMin;
                max = DisplayHeightMax;
                track = dossensi2;
            }
            else if (lbl == eoswidth)
            {
                title = "Emulator width";
                prompt = "Width (pixels):";
                min = EmulatorWidthMin;
                max = EmulatorWidthMax;
                track = eossensi1;
            }
            else if (lbl == eosheight)
            {
                title = "Emulator height";
                prompt = "Height (pixels):";
                min = EmulatorHeightMin;
                max = EmulatorHeightMax;
                track = eossensi2;
            }
            else
                return;

            bool wasTopMost = TopMost;
            try
            {
                TopMost = false;
                Activate();
                BringToFront();

                string input = Interaction.InputBox(prompt, title, lbl.Text.Trim(), -1, -1);
                if (string.IsNullOrWhiteSpace(input))
                    return;
                if (!int.TryParse(input.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                {
                    MessageBox.Show(this, "Enter a whole number.", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                v = Math.Min(max, Math.Max(min, v));
                track.Value = Math.Min(track.Maximum, Math.Max(track.Minimum, v));
                UpdateResolutionLabels();
                ApplySettingsIfActive();
            }
            finally
            {
                TopMost = wasTopMost;
            }
        }

        private bool defaultValuesLoaded = false;
        private void InitializeResolutions()
        {
            Screen primaryScreen = Screen.PrimaryScreen;
            displayResolution = primaryScreen.Bounds;

            if (!defaultValuesLoaded)
            {
                dossensi1.Value = Math.Min(dossensi1.Maximum, Math.Max(dossensi1.Minimum, displayResolution.Width));
                dossensi2.Value = Math.Min(dossensi2.Maximum, Math.Max(dossensi2.Minimum, displayResolution.Height));
            }

            UpdateResolutionLabels();
        }

        private void UpdateResolutionLabels()
        {
            doswidth.Text = dossensi1.Value.ToString();
            dosheight.Text = dossensi2.Value.ToString();
            eoswidth.Text = eossensi1.Value.ToString();
            eosheight.Text = eossensi2.Value.ToString();
        }

        private void InitializeBackgroundProcessing()
        {
            settingsMonitorTimer = new System.Windows.Forms.Timer();
            settingsMonitorTimer.Interval = 1000;
            settingsMonitorTimer.Tick += SettingsMonitorTimer_Tick;

            appPositions = new Dictionary<string, Point>();
            lastPositionUpdate = new Dictionary<string, DateTime>();
        }


        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private void SettingsMonitorTimer_Tick(object sender, EventArgs e)
        {
            if (!isActive)
                return;

            bool wasFullScreen = isFullScreenAppRunning;
            isFullScreenAppRunning = IsFullScreenApplicationRunning();

            if (wasFullScreen != isFullScreenAppRunning)
            {
                Console.WriteLine($"Full-screen state changed: {isFullScreenAppRunning}");

                if (isFullScreenAppRunning)
                {
                    ApplySettings();
                }
            }

            CleanupStaleCursorPositions();

            EnsureMouseHookActive();

            if (enhancedMouseHook != null && enhancedMouseHook.IsActive && IsFullScreenApplicationRunning())
                ApplySettings();

            if (sensitivityControlActive && movementPredictor != null)
                RunAiPeriodicDiagnostics();
        }

        private const int GWL_STYLE = -16;

        private bool IsFullScreenApplicationRunning()
        {
            IntPtr hWnd = NativeMethods.GetForegroundWindow();
            if (hWnd == IntPtr.Zero)
                return false;

            RECT windowRect;
            if (!GetWindowRect(hWnd, out windowRect))
                return false;

            int style = GetWindowLong(hWnd, GWL_STYLE);

            Screen primaryScreen = Screen.PrimaryScreen;
            bool coversScreen = (windowRect.Left <= 0 &&
                                 windowRect.Top <= 0 &&
                                 windowRect.Right >= primaryScreen.Bounds.Width &&
                                 windowRect.Bottom >= primaryScreen.Bounds.Height);

            bool isDesktop = IsDesktopWindow(hWnd);
            bool isOurApp = (hWnd == this.Handle);

            return coversScreen && !isDesktop && !isOurApp;
        }

        private bool IsDesktopWindow(IntPtr hWnd)
        {
            IntPtr desktopWnd = GetDesktopWindow();
            IntPtr shellWnd = GetShellWindow();

            if (hWnd == desktopWnd || hWnd == shellWnd)
                return true;

            StringBuilder className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);

            string classNameStr = className.ToString();
            return (classNameStr == "WorkerW" ||
                    classNameStr == "Progman" ||
                    classNameStr == "Shell_TrayWnd");
        }

        private void CleanupStaleCursorPositions()
        {
            DateTime now = DateTime.Now;

            List<string> keysToRemove = new List<string>();

            foreach (var entry in lastPositionUpdate)
            {
                if ((now - entry.Value).TotalMinutes > 5)
                {
                    keysToRemove.Add(entry.Key);
                }
            }

            foreach (string key in keysToRemove)
            {
                appPositions.Remove(key);
                lastPositionUpdate.Remove(key);
            }

            if (keysToRemove.Count > 0)
            {
                Console.WriteLine($"Cleaned up {keysToRemove.Count} stale position entries");
            }
        }

        private class MouseSettings
        {
            public int GeneralSensitivity { get; set; }
            public int XSensitivity { get; set; }
            public int YSensitivity { get; set; }
            public bool IsActive { get; set; }

            public MouseSettings(int general, int x, int y, bool active)
            {
                GeneralSensitivity = general;
                XSensitivity = x;
                YSensitivity = y;
                IsActive = active;
            }
        }

        private System.Windows.Forms.Timer monitorTimer;

        private void InitializeFullScreenPreservation()
        {
            perAppSettings = new Dictionary<string, MouseSettings>();

            monitorTimer = new System.Windows.Forms.Timer();
            monitorTimer.Interval = 500;
            monitorTimer.Tick += MonitorTimer_Tick;
            monitorTimer.Start();
        }

        private void MonitorTimer_Tick(object sender, EventArgs e)
        {
            if (!isActive || !persistAcrossFullscreen)
                return;

            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero)
                return;

            uint processId;
            GetWindowThreadProcessId(hWnd, out processId);

            string appName = "unknown";
            try
            {
                using (Process process = Process.GetProcessById((int)processId))
                {
                    appName = process.ProcessName;
                }
            }
            catch
            {
                return;
            }

            if (appName != currentAppName)
            {
                if (!string.IsNullOrEmpty(currentAppName) && isActive)
                {
                    SaveSettingsForApp(currentAppName);
                }

                currentAppName = appName;

                if (perAppSettings.ContainsKey(appName))
                {
                    LoadSettingsForApp(appName);
                }
                else
                {
                    Console.WriteLine($"No saved settings for {appName}, using current settings");
                }
            }

            bool isFullScreen = IsFullScreenApplicationRunning();

            if (isFullScreen && !isFullScreenAppRunning)
            {
                isFullScreenAppRunning = true;
                Console.WriteLine($"Entered full-screen mode in {appName}");

                ReapplyCurrentSettings();
            }
            else if (!isFullScreen && isFullScreenAppRunning)
            {
                isFullScreenAppRunning = false;
                Console.WriteLine($"Exited full-screen mode from {appName}");
            }
        }


        private void SaveSettingsForApp(string appName)
        {
            MouseSettings settings = new MouseSettings(
                mousesensi.Value,
                xaxissensi.Value,
                yaxissensi.Value,
                isActive
            );

            perAppSettings[appName] = settings;
            Console.WriteLine($"Saved settings for {appName}: General={settings.GeneralSensitivity}, X={settings.XSensitivity}, Y={settings.YSensitivity}");
        }

        private void LoadSettingsForApp(string appName)
        {
            if (!perAppSettings.ContainsKey(appName))
                return;

            MouseSettings settings = perAppSettings[appName];

            mousesensi.Value = settings.GeneralSensitivity;
            xaxissensi.Value = settings.XSensitivity;
            yaxissensi.Value = settings.YSensitivity;

            UpdateAllLabels();

            ApplySettings();

            Console.WriteLine($"Loaded settings for {appName}: General={settings.GeneralSensitivity}, X={settings.XSensitivity}, Y={settings.YSensitivity}");
        }

        private void ReapplyCurrentSettings()
        {
            lastMousePosition = Point.Empty;

            ApplySettings();

            if (!mouseHookActive)
            {
                StartMouseHook();
            }
        }
        private void EnsureMouseHookActive()
        {
            if (isActive && !mouseHookActive)
            {
                Console.WriteLine("Mouse hook not active. Restarting...");
                StartMouseHook();
            }
        }

        private void InitializeTrackBars()
        {

            mousesensi.Maximum = 100;
            mousesensi.Minimum = 1;
            mousesensi.Value = 20;
            mousesensi.SmallChange = 1;
            mousesensi.LargeChange = 5;
            mousesensi.MouseWheelBarPartitions = 10;
            defaultValues["mousesensi"] = 20;

            xaxissensi.Maximum = 100;
            xaxissensi.Minimum = 1;
            xaxissensi.Value = 40;
            xaxissensi.SmallChange = 1;
            xaxissensi.LargeChange = 5;
            xaxissensi.MouseWheelBarPartitions = 10;
            defaultValues["xaxissensi"] = 40;

            yaxissensi.Maximum = 100;
            yaxissensi.Minimum = 1;
            yaxissensi.Value = 40;
            yaxissensi.SmallChange = 1;
            yaxissensi.LargeChange = 5;
            yaxissensi.MouseWheelBarPartitions = 10;
            defaultValues["yaxissensi"] = 40;

            dossensi1.Maximum = 3840;
            dossensi1.Minimum = 800;
            dossensi1.Value = 1920;
            dossensi1.SmallChange = 10;
            dossensi1.LargeChange = 100;
            dossensi1.MouseWheelBarPartitions = 10;
            defaultValues["dossensi1"] = 1920;

            dossensi2.Maximum = 2160;
            dossensi2.Minimum = 600;
            dossensi2.Value = 1080;
            dossensi2.SmallChange = 10;
            dossensi2.LargeChange = 100;
            dossensi2.MouseWheelBarPartitions = 10;
            defaultValues["dossensi2"] = 1080;

            eossensi1.Maximum = 2560;
            eossensi1.Minimum = 800;
            eossensi1.Value = 1280;
            eossensi1.SmallChange = 10;
            eossensi1.LargeChange = 100;
            eossensi1.MouseWheelBarPartitions = 10;
            defaultValues["eossensi1"] = 1280;

            eossensi2.Maximum = 1440;
            eossensi2.Minimum = 600;
            eossensi2.Value = 720;
            eossensi2.SmallChange = 10;
            eossensi2.LargeChange = 100;
            eossensi2.MouseWheelBarPartitions = 10;
            defaultValues["eossensi2"] = 720;

            UpdateAllLabels();
        }

        private void UpdateAllLabels()
        {
            mousesensi1.Text = mousesensi.Value.ToString();
            xaxissensi1.Text = xaxissensi.Value.ToString();
            yaxissensi1.Text = yaxissensi.Value.ToString();

            doswidth.Text = dossensi1.Value.ToString();
            dosheight.Text = dossensi2.Value.ToString();
            eoswidth.Text = eossensi1.Value.ToString();
            eosheight.Text = eossensi2.Value.ToString();
        }

        private static class NativeMethods
        {
            public delegate IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr SetWindowsHookEx(int idHook, MouseHookCallback lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);

            [DllImport("user32.dll")]
            public static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll")]
            public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

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

            public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

            public const int MOUSEEVENTF_LEFTDOWN = 0x0002;
            public const int MOUSEEVENTF_LEFTUP = 0x0004;

            [StructLayout(LayoutKind.Sequential)]
            public struct INPUT
            {
                public int type;
                public MOUSEINPUT mi;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MOUSEINPUT
            {
                public int dx;
                public int dy;
                public uint mouseData;
                public uint dwFlags;
                public uint time;
                public IntPtr dwExtraInfo;
            }

        }

        private void StartMouseHook()
        {
            if (!mouseHookActive)
            {
                mouseHookProcDelegate = MouseHookProc;
                IntPtr moduleHandle = NativeMethods.GetModuleHandle(null);
                mouseHookHandle = NativeMethods.SetWindowsHookEx(WH_MOUSE_LL, mouseHookProcDelegate, moduleHandle, 0);

                if (mouseHookHandle != IntPtr.Zero)
                {
                    mouseHookActive = true;

                    hookMaintainTimer = new System.Windows.Forms.Timer();
                    hookMaintainTimer.Interval = 5000;
                    hookMaintainTimer.Tick += HookMaintainTimer_Tick;
                    hookMaintainTimer.Start();

                    Console.WriteLine("Mouse hook started successfully");
                }
                else
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    Console.WriteLine($"Failed to set mouse hook. Error code: {errorCode}");
                }
            }
        }

        private void StopMouseHook()
        {
            if (mouseHookActive)
            {
                if (hookMaintainTimer != null)
                {
                    hookMaintainTimer.Stop();
                    hookMaintainTimer.Dispose();
                    hookMaintainTimer = null;
                }

                bool result = NativeMethods.UnhookWindowsHookEx(mouseHookHandle);
                if (result)
                {
                    mouseHookActive = false;
                    mouseHookHandle = IntPtr.Zero;
                    Console.WriteLine("Mouse hook stopped successfully");
                }
                else
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    Console.WriteLine($"Failed to unhook mouse hook. Error code: {errorCode}");
                }
            }
        }

        private void HookMaintainTimer_Tick(object sender, EventArgs e)
        {
            if (mouseHookActive && mouseHookHandle == IntPtr.Zero)
            {
                IntPtr moduleHandle = NativeMethods.GetModuleHandle(null);
                mouseHookHandle = NativeMethods.SetWindowsHookEx(WH_MOUSE_LL, mouseHookProcDelegate, moduleHandle, 0);

                if (mouseHookHandle == IntPtr.Zero)
                {
                    Console.WriteLine("Failed to reinstall mouse hook");
                }
                else
                {
                    Console.WriteLine("Mouse hook reinstalled successfully");
                }
            }
        }

        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam.ToInt32() == WM_MOUSEMOVE && isActive && !processingMouseMove)
            {
                try
                {
                    processingMouseMove = true;

                    NativeMethods.MSLLHOOKSTRUCT hookStruct = (NativeMethods.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.MSLLHOOKSTRUCT));
                    Point currentPosition = new Point(hookStruct.pt.x, hookStruct.pt.y);

                    if (lastMousePosition.IsEmpty)
                    {
                        lastMousePosition = currentPosition;
                        return NativeMethods.CallNextHookEx(mouseHookHandle, nCode, wParam, lParam);
                    }

                    int deltaX = currentPosition.X - lastMousePosition.X;
                    int deltaY = currentPosition.Y - lastMousePosition.Y;

                    if (deltaX == 0 && deltaY == 0)
                    {
                        return NativeMethods.CallNextHookEx(mouseHookHandle, nCode, wParam, lParam);
                    }

                    float generalSensitivity = mousesensi.Value / 50.0f;

                    float xSensitivity = xaxissensi.Value / 50.0f;
                    float ySensitivity = yaxissensi.Value / 50.0f;

                    float adjustedDeltaX = deltaX * generalSensitivity * xSensitivity;
                    float adjustedDeltaY = deltaY * generalSensitivity * ySensitivity;

                    int newX = lastMousePosition.X + (int)Math.Round(adjustedDeltaX);
                    int newY = lastMousePosition.Y + (int)Math.Round(adjustedDeltaY);

                    newX = Math.Max(0, Math.Min(newX, Screen.PrimaryScreen.Bounds.Width - 1));
                    newY = Math.Max(0, Math.Min(newY, Screen.PrimaryScreen.Bounds.Height - 1));

                    SetCursorPos(newX, newY);

                    lastMousePosition = new Point(newX, newY);

                    return new IntPtr(1);
                }
                finally
                {
                    processingMouseMove = false;
                }
            }

            return NativeMethods.CallNextHookEx(mouseHookHandle, nCode, wParam, lParam);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out Point lpPoint);

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentAppName) && isActive)
                SaveSettingsForApp(currentAppName);

            StopMouseHook();
            StopMouseHook69();

            if (settingsMonitorTimer != null)
            {
                settingsMonitorTimer.Stop();
                settingsMonitorTimer.Dispose();
                settingsMonitorTimer = null;
            }

            if (monitorTimer != null)
            {
                monitorTimer.Stop();
                monitorTimer.Dispose();
                monitorTimer = null;
            }

            CleanupMouseHook();

            if (overlaySystem != null)
            {
                overlaySystem.Dispose();
                overlaySystem = null;
            }

            UnregisterHotKey(this.Handle, HOTKEY_ID);

            base.OnFormClosing(e);
        }

        private void mousesensi_Scroll(object sender, ScrollEventArgs e)
        {
            mousesensi1.Text = mousesensi.Value.ToString();
            ApplySettingsIfActive();
        }


        private void releasebutton1_Click(object sender, EventArgs e)
        {
            isActive = true;
            ApplySettings();

            StartMouseHook();

            if (settingsMonitorTimer == null)
            {
                settingsMonitorTimer = new System.Windows.Forms.Timer();
                settingsMonitorTimer.Interval = 1000;
                settingsMonitorTimer.Tick += SettingsMonitorTimer_Tick;
            }

            settingsMonitorTimer.Start();

            releasebutton1.Hide();
            pausebutton1.Show();

            MessageBox.Show("Settings applied and activated!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void pausebutton1_Click(object sender, EventArgs e)
        {
            isActive = false;
            StopMouseHook();

            settingsMonitorTimer.Stop();

            pausebutton1.Hide();
            releasebutton1.Show();

            isActive = false;
            StopMouseHook69();

            if (overlaySystem != null)
            {
                overlaySystem.Deactivate();
            }

            settingsMonitorTimer.Stop();

            MessageBox.Show("Settings paused!", "Paused", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private void refreshbutton1_Click(object sender, EventArgs e)
        {
            ResetToDefaults();
        }

        private void moresensi1_Click(object sender, EventArgs e)
        {
            mainpanel1.Hide();
            extrapanel1.Hide();
            optpanel1.Hide();
            mainpanel2.Show();
        }

        private void xaxissensi_Scroll(object sender, ScrollEventArgs e)
        {
            xaxissensi1.Text = xaxissensi.Value.ToString();
            ApplySettingsIfActive();
        }

        private void yaxissensi_Scroll(object sender, ScrollEventArgs e)
        {
            yaxissensi1.Text = yaxissensi.Value.ToString();
            ApplySettingsIfActive();
        }

        private void dossensi1_Scroll(object sender, ScrollEventArgs e)
        {
            doswidth.Text = dossensi1.Value.ToString();
            ApplySettingsIfActive();
        }

        private void dossensi2_Scroll(object sender, ScrollEventArgs e)
        {
            dosheight.Text = dossensi2.Value.ToString();
            ApplySettingsIfActive();
        }

        private void eossensi1_Scroll(object sender, ScrollEventArgs e)
        {
            eoswidth.Text = eossensi1.Value.ToString();
            ApplySettingsIfActive();
        }

        private void eossensi2_Scroll(object sender, ScrollEventArgs e)
        {
            eosheight.Text = eossensi2.Value.ToString();
            ApplySettingsIfActive();
        }

        private void ApplySettingsIfActive()
        {
            if (isActive)
            {
                ApplySettings();
            }
        }

        private void ApplySettings()
        {
            int mouseSensitivity = mousesensi.Value;
            int xAxisSensitivity = xaxissensi.Value;
            int yAxisSensitivity = yaxissensi.Value;
            int displayWidth = dossensi1.Value;
            int displayHeight = dossensi2.Value;
            int emulatorWidth = eossensi1.Value;
            int emulatorHeight = eossensi2.Value;

            displayResolution = new Rectangle(0, 0, displayWidth, displayHeight);
            emulatorResolution = new Rectangle(0, 0, emulatorWidth, emulatorHeight);

            Screen primaryScreen = Screen.PrimaryScreen;
            bool displayDiffers = (displayResolution.Width != primaryScreen.Bounds.Width ||
                                  displayResolution.Height != primaryScreen.Bounds.Height);

            resolutionScalingEnabled = displayDiffers ||
                                      (emulatorResolution.Width > 0 && emulatorResolution.Height > 0);

            lastMousePosition = Point.Empty;

            if (isActive && !mouseHookActive)
            {
                StartMouseHook();
            }

            if (!string.IsNullOrEmpty(currentAppName) && persistAcrossFullscreen)
            {
                MouseSettings settings = new MouseSettings(
                    mouseSensitivity,
                    xAxisSensitivity,
                    yAxisSensitivity,
                    isActive
                );

                perAppSettings[currentAppName] = settings;
            }

            Console.WriteLine($"Applied settings: Mouse={mouseSensitivity}, X={xAxisSensitivity}, Y={yAxisSensitivity}");
            Console.WriteLine($"Display: {displayWidth}x{displayHeight}, Emulator: {emulatorWidth}x{emulatorHeight}");
            Console.WriteLine($"Resolution scaling: {(resolutionScalingEnabled ? "Enabled" : "Disabled")}");

            if (isActive)
                UpdateSystemMouseSensitivity(mouseSensitivity);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private string GetWindowTitle(IntPtr hWnd)
        {
            const int nChars = 256;
            StringBuilder sb = new StringBuilder(nChars);

            if (GetWindowText(hWnd, sb, nChars) > 0)
            {
                return sb.ToString();
            }

            return string.Empty;
        }

        private bool IsEmulatorWindow(string windowTitle)
        {
            if (string.IsNullOrEmpty(windowTitle))
                return false;

            string[] emulatorIdentifiers = new string[]
            {
        "bluestacks", "msi app player", "nox", "HD-Player", "ldplayer", "memu",
        "gameloop", "android emulator", "emulator"
            };

            windowTitle = windowTitle.ToLower();

            foreach (string identifier in emulatorIdentifiers)
            {
                if (windowTitle.Contains(identifier))
                    return true;
            }

            return false;
        }

        private void UpdateSystemMouseSensitivity(int sensitivityValue)
        {
            try
            {
                int winSensitivity = (int)Math.Round(sensitivityValue / 5.0);
                winSensitivity = Math.Max(1, Math.Min(20, winSensitivity));

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", true))
                {
                    if (key != null)
                    {
                        key.SetValue("MouseSensitivity", winSensitivity.ToString(), RegistryValueKind.String);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating system mouse sensitivity: {ex.Message}");
            }
        }

        private void SaveSettingsToRegistry()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY, true))
                {
                    if (key == null)
                    {
                        MessageBox.Show("Could not open registry for saving.", "LegitX V2", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    key.SetValue("MouseSensitivity", mousesensi.Value, RegistryValueKind.DWord);
                    key.SetValue("XAxisSensitivity", xaxissensi.Value, RegistryValueKind.DWord);
                    key.SetValue("YAxisSensitivity", yaxissensi.Value, RegistryValueKind.DWord);
                    key.SetValue("DisplayWidth", dossensi1.Value, RegistryValueKind.DWord);
                    key.SetValue("DisplayHeight", dossensi2.Value, RegistryValueKind.DWord);
                    key.SetValue("EmulatorWidth", eossensi1.Value, RegistryValueKind.DWord);
                    key.SetValue("EmulatorHeight", eossensi2.Value, RegistryValueKind.DWord);

                    UpdateAllLabels();
                    if (isActive)
                        ApplySettings();
                    else
                        UpdateSystemMouseSensitivity(mousesensi.Value);

                    MessageBox.Show("Settings saved.", "LegitX V2", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save settings.{Environment.NewLine}{ex.Message}", "LegitX V2", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void LoadSettingsFromRegistry()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY))
                {
                    if (key != null)
                    {
                        mousesensi.Value = GetRegistryValue(key, "MouseSensitivity", defaultValues["mousesensi"]);
                        xaxissensi.Value = GetRegistryValue(key, "XAxisSensitivity", defaultValues["xaxissensi"]);
                        yaxissensi.Value = GetRegistryValue(key, "YAxisSensitivity", defaultValues["yaxissensi"]);
                        int dw = GetRegistryValue(key, "DisplayWidth", defaultValues["dossensi1"]);
                        dossensi1.Value = Math.Min(dossensi1.Maximum, Math.Max(dossensi1.Minimum, dw));
                        int dh = GetRegistryValue(key, "DisplayHeight", defaultValues["dossensi2"]);
                        dossensi2.Value = Math.Min(dossensi2.Maximum, Math.Max(dossensi2.Minimum, dh));
                        int ew = GetRegistryValue(key, "EmulatorWidth", defaultValues["eossensi1"]);
                        eossensi1.Value = Math.Min(eossensi1.Maximum, Math.Max(eossensi1.Minimum, ew));
                        int eh = GetRegistryValue(key, "EmulatorHeight", defaultValues["eossensi2"]);
                        eossensi2.Value = Math.Min(eossensi2.Maximum, Math.Max(eossensi2.Minimum, eh));

                        UpdateAllLabels();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            }
        }
        private int GetRegistryValue(RegistryKey key, string valueName, int defaultValue)
        {
            object value = key.GetValue(valueName);
            if (value == null)
                return defaultValue;

            try
            {
                if (value is int i)
                    return i;
                if (value is long l)
                    return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, l));
                if (value is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                    return parsed;
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        private void ResetToDefaults()
        {
            mousesensi.Value = defaultValues["mousesensi"];
            xaxissensi.Value = defaultValues["xaxissensi"];
            yaxissensi.Value = defaultValues["yaxissensi"];
            dossensi1.Value = defaultValues["dossensi1"];
            dossensi2.Value = defaultValues["dossensi2"];
            eossensi1.Value = defaultValues["eossensi1"];
            eossensi2.Value = defaultValues["eossensi2"];

            UpdateAllLabels();

            MessageBox.Show("All settings have been reset to defaults.", "Reset Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void mainpanel_CheckedChanged(object sender, EventArgs e)
        {
            mainpanel2.Hide();
            extrapanel1.Hide();
            optpanel1.Hide();
            mainpanel1.Show();
        }

        private void optimizationpanel_CheckedChanged(object sender, EventArgs e)
        {
            mainpanel2.Hide();
            extrapanel1.Hide();
            mainpanel1.Hide();
            optpanel1.Show();
        }

        private void extrapanel_CheckedChanged(object sender, EventArgs e)
        {
            mainpanel2.Hide();
            mainpanel1.Hide();
            optpanel1.Hide();
            extrapanel1.Show();
        }

        public static bool Streaming;
        [DllImport("user32.dll")]
        public static extern uint SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);
        private void streamermode_CheckedChanged(object sender, EventArgs e)
        {
            if (streamermode.Checked)
            {
                base.ShowInTaskbar = false;
                LegitX.Streaming = true;
                LegitX.SetWindowDisplayAffinity(base.Handle, 17U);
            }
            else
            {
                base.ShowInTaskbar = true;
                LegitX.Streaming = false;
                LegitX.SetWindowDisplayAffinity(base.Handle, 0U);
            }
        }

        private void tmbypass_CheckedChanged(object sender, EventArgs e)
        {

        }



        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;
        private bool isHiddenFromAltTab = false;

        private void formbypass_CheckedChanged(object sender, EventArgs e)
        {
            if (isHiddenFromAltTab)
            {
                ShowInAltTab();
                isHiddenFromAltTab = false;
            }
            else
            {
                HideFromAltTab();
                isHiddenFromAltTab = true;
            }
        }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
        }

        private void HideFromAltTab()
        {
            IntPtr hWnd = this.Handle;
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;
            exStyle &= ~WS_EX_APPWINDOW;
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);
            this.ShowInTaskbar = false;
        }
        private void ShowInAltTab()
        {
            IntPtr hWnd = this.Handle;
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            exStyle &= ~WS_EX_TOOLWINDOW;
            exStyle |= WS_EX_APPWINDOW;
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);
            this.ShowInTaskbar = true;
        }

        private void starttoggle1_CheckedChanged(object sender, EventArgs e)
        {

        }

        private async void bluestacks4link_Click(object sender, EventArgs e)
        {
            string[] browserProcesses = { "chrome", "firefox", "msedge", "opera", "brave" };
            string url = "https://download2437.mediafire.com/d75yul12m4ag7j2oGVdEzTbUr9W-7QUmD2I4vlFMLkP-Rb2BSimHFI-nMeg77hZo7GHOEIyZWLEIM6v3gpc9R8Ctcz_4vslIuLnFKlOw5XXN2PUUn4dDPW4waZffuJKpwjh3FfuNK-79jAku8RtAjPUdRS-5CNxtRuakibN2QHG4/5q6tnq9up7preqg/BlueStacks-Installer_4.240.30.1002_amd64_native.exe";

            if (!await IsUrlAvailable(url))
            {
                MessageBox.Show("The URL is not working. Please contact the developer @subhoxx.",
                                "URL Not Available",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                return;
            }

            bool isBrowserRunning = Process.GetProcesses()
                                           .Any(p => browserProcesses.Contains(p.ProcessName.ToLower()));

            if (isBrowserRunning)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open the link. Please try again.\n" + ex.Message,
                                    "Error",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("No web browser is currently running. Please open your browser first, then click the button again to download the file.",
                                "Browser Not Running",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
            }
        }
        private async void msi4link_Click(object sender, EventArgs e)
        {
            string[] browserProcesses = { "chrome", "firefox", "msedge", "opera", "brave" };
            string url = "https://download2443.mediafire.com/qv2hg4rvbx9gNJQ2NXO6lPyU5HsH2GuTSytXajGXxQqyginyXBE-OriQgImWZQCBbe0hDMW7X1ifssaKJmFQqH1sYRdwRdNAfjpretB-xBlIz7CcCBVKxtD2P3uW-oNwiqgeWnaPUHHHSL-Ijl_8kNQr945HZdHd42xaBqPsE5dt/zo82s9nehqcb2m8/ULTRA+GOD+2+versoes.rar";

            if (!await IsUrlAvailable(url))
            {
                MessageBox.Show("The URL is not working. Please contact the developer @subhoxx.",
                                "URL Not Available",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                return;
            }

            bool isBrowserRunning = Process.GetProcesses()
                                           .Any(p => browserProcesses.Contains(p.ProcessName.ToLower()));

            if (isBrowserRunning)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open the link. Please try again.\n" + ex.Message,
                                    "Error",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("No web browser is currently running. Please open your browser first, then click the button again to download the file.",
                                "Browser Not Running",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
            }
        }

        private async Task<bool> IsUrlAvailable(string url)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);

                    var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        private async void masi5link_Click(object sender, EventArgs e)
        {
            string[] browserProcesses = { "chrome", "firefox", "msedge", "opera", "brave" };
            string url = "https://download2294.mediafire.com/apnjtek4shkgXg7UcCJZ7eW71mK25sLgycsClQG1vGAVmfvHJ4TKMG-lZ27PrXumy08Epqz0YVPigQSLWFZTGl41IomFZ2og0xtoYMXxs8QqDC_Gg7sVscYN9qgueMPaCXSoy4-e5_j_HhWRsvf6OZqvoBKos9JszX_w0_o0Vkik/txiuddc7fn53cj5/MSI-APP-Player.zip";

            if (!await IsUrlAvailable(url))
            {
                MessageBox.Show("The URL is not working. Please contact the developer @subhoxx.",
                                "URL Not Available",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                return;
            }

            bool isBrowserRunning = Process.GetProcesses()
                                           .Any(p => browserProcesses.Contains(p.ProcessName.ToLower()));

            if (isBrowserRunning)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open the link. Please try again.\n" + ex.Message,
                                    "Error",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("No web browser is currently running. Please open your browser first, then click the button again to download the file.",
                                "Browser Not Running",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
            }
        }

        private async void bluestacks5link_Click(object sender, EventArgs e)
        {
            string[] browserProcesses = { "chrome", "firefox", "msedge", "opera", "brave" };
            string url = "https://download2390.mediafire.com/np3h9bqk251geJ7MHqNUVRjaUhgc9gjN0cA2jtfLlluFNqQagum2U8dALPxy7qCJVcCrJPNYW-1PTU5mZ9buTr-lq_RdSG9tkxs0x2UbhMPc7ohVQ3NOdeuOekaOEBKjk7ynnkD6EYaXpzdu2Q09L4_M9ufy2hnN8F9gNP4fImH6/9f5r4hgptpon5gv/BlueStacksInstaller_5.21.100.1011_native_ca18f1b8e83b13075f03dd4909a5f51f_MzsxNSwwOzUsMTsxNSw0OzE1.exe";

            if (!await IsUrlAvailable(url))
            {
                MessageBox.Show("The URL is not working. Please contact the developer @subhoxx.",
                                "URL Not Available",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                return;
            }

            bool isBrowserRunning = Process.GetProcesses()
                                           .Any(p => browserProcesses.Contains(p.ProcessName.ToLower()));

            if (isBrowserRunning)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open the link. Please try again.\n" + ex.Message,
                                    "Error",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("No web browser is currently running. Please open your browser first, then click the button again to download the file.",
                                "Browser Not Running",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
            }
        }

        private void optcpu_CheckedChanged(object sender, EventArgs e)
        {
            if (optcpu.Checked)
            {
                string regFileName = "Intel CPU Priority.reg";
                string tempPath = Path.Combine(Path.GetTempPath(), regFileName);

                try
                {
                    DialogResult result = MessageBox.Show(
                        "Do you really want to merge the registry file? This may affect system settings.",
                        "Confirm Registry Merge",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning
                    );

                    if (result == DialogResult.Yes)
                    {
                        ExtractEmbeddedResource("LegitX_V2." + regFileName, tempPath);

                        MergeRegFile(tempPath);

                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }

                        MessageBox.Show("Registry settings applied successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Registry merge canceled.", "Canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExtractEmbeddedResource(string resourceName, string outputPath)
        {
            using (Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (resourceStream == null)
                {
                    throw new Exception($"Resource '{resourceName}' not found.");
                }

                using (FileStream fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    resourceStream.CopyTo(fileStream);
                }
            }
        }

        private void MergeRegFile(string regFilePath)
        {
            Process process = new Process();
            process.StartInfo.FileName = "regedit.exe";
            process.StartInfo.Arguments = $"/s \"{regFilePath}\"";
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.Verb = "runas";

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception("Failed to merge the registry file.");
            }
        }

        private void optgpu_CheckedChanged(object sender, EventArgs e)
        {
            if (optgpu.Checked)
            {
                MessageBox.Show("The remote GPU optimizer was removed from the open-source build because it downloaded and ran an unsigned batch file. Apply documented NVIDIA settings manually instead.", "Optimizer unavailable", MessageBoxButtons.OK, MessageBoxIcon.Information);
                optgpu.Checked = false;
            }
        }


        private void optram_CheckedChanged(object sender, EventArgs e)
        {
            if (optram.Checked)
            {
                string regFileName = "8GB Ram.reg";
                string tempPath = Path.Combine(Path.GetTempPath(), regFileName);

                try
                {
                    DialogResult result = MessageBox.Show(
                        "Do you really want to merge the registry file? This may affect system settings.",
                        "Confirm Registry Merge",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning
                    );

                    if (result == DialogResult.Yes)
                    {
                        ExtractEmbeddedResource("LegitX_V2." + regFileName, tempPath);

                        MergeRegFile(tempPath);

                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }

                        MessageBox.Show("Registry settings applied successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Registry merge canceled.", "Canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void optwinscal_CheckedChanged(object sender, EventArgs e)
        {
            if (optwinscal.Checked)
            {
                string regFileName = "windows_10&11fix.reg";
                string tempPath = Path.Combine(Path.GetTempPath(), regFileName);

                try
                {
                    DialogResult result = MessageBox.Show(
                        "Do you really want to merge the registry file? This may affect system settings.",
                        "Confirm Registry Merge",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning
                    );

                    if (result == DialogResult.Yes)
                    {
                        ExtractEmbeddedResource("LegitX_V2." + regFileName, tempPath);

                        MergeRegFile(tempPath);

                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }

                        MessageBox.Show("Registry settings applied successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Registry merge canceled.", "Canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void optdisplay_CheckedChanged(object sender, EventArgs e)
        {
            if (optdisplay.Checked)
            {
                string regFileName = "Monitor1ms.reg";
                string tempPath = Path.Combine(Path.GetTempPath(), regFileName);

                try
                {
                    DialogResult result = MessageBox.Show(
                        "Do you really want to merge the registry file? This may affect system settings.",
                        "Confirm Registry Merge",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning
                    );

                    if (result == DialogResult.Yes)
                    {
                        ExtractEmbeddedResource("LegitX_V2." + regFileName, tempPath);

                        MergeRegFile(tempPath);

                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }

                        MessageBox.Show("Registry settings applied successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Registry merge canceled.", "Canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void optemu_CheckedChanged(object sender, EventArgs e)
        {
            if (optemu.Checked)
            {
                string regFileName = "Optimize_Emulator_V1.reg";
                string tempRegPath = Path.Combine(Path.GetTempPath(), regFileName);

                try
                {
                    DialogResult result = MessageBox.Show(
                        "Do you really want to merge the registry file? This may affect system settings.",
                        "Confirm Registry Merge",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning
                    );

                    if (result == DialogResult.Yes)
                    {
                        ExtractEmbeddedResource("LegitX_V2." + regFileName, tempRegPath);
                        MergeRegFile(tempRegPath);
                        File.Delete(tempRegPath);
                        MessageBox.Show("Registry settings applied successfully. The unsigned batch optimizer is intentionally not included in the open-source build.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Operation canceled.", "Canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void optwin_CheckedChanged(object sender, EventArgs e)
        {
            if (optwin.Checked)
            {
                string[] regFileNames = {
            "fpsboost11.reg",
            "fpsboost10.reg",
            "fpsboost9.reg",
            "fpsboost8.reg",
            "fpsboost7.reg",
            "fpsboost6.reg",
            "fpsboost5.reg",
            "fpsboost4.reg",
            "fpsboost3.reg",
            "fpsboost2.reg",
            "fpsboost1.reg"
        };

                try
                {
                    DialogResult result = MessageBox.Show(
                        "Do you really want to merge the registry files? This may affect system settings.",
                        "Confirm Registry Merge",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning
                    );

                    if (result == DialogResult.Yes)
                    {
                        foreach (string regFileName in regFileNames)
                        {
                            string tempPath = Path.Combine(Path.GetTempPath(), regFileName);

                            ExtractEmbeddedResource("LegitX_V2." + regFileName, tempPath);

                            MergeRegFile(tempPath);

                            if (File.Exists(tempPath))
                            {
                                File.Delete(tempPath);
                            }
                        }

                        MessageBox.Show("All registry settings applied successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Registry merge canceled.", "Canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void optwlan_CheckedChanged(object sender, EventArgs e)
        {
            if (optwlan.Checked)
            {
                try
                {
                    RunNetworkOptimization();
                    MessageBox.Show("Network Optimization completed! Restart your PC for the changes to take effect.",
                                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void RunNetworkOptimization()
        {
            string[] commands = new[]
            {
                "ipconfig /release",
                "ipconfig /flushdns",
                "ipconfig /renew",
                "netsh winsock reset"
            };

            foreach (var command in commands)
            {
                ExecuteCommand(command);
            }
        }

        private void ExecuteCommand(string command)
        {
            using (Process process = new Process())
            {
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = $"/C {command}";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (!string.IsNullOrEmpty(error))
                {
                    throw new Exception($"Command failed: {command}\nError: {error}");
                }
            }
        }

    private void guna2ImageButton1_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Before applying optimizers, please ask @subhoxx if your PC is capable of it.",
                            "Important Notice",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

        }


        private void optcpuinfo_Click(object sender, EventArgs e)
        {
            MessageBox.Show("This optimizer is only for Intel CPUs. If you have an AMD CPU, please contact @subhoxx. He might help you optimize your AMD CPU.",
                            "CPU Compatibility Notice",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

        }

        private void optgpuinfo_Click(object sender, EventArgs e)
        {
            MessageBox.Show("This optimizer is only for NVIDIA. If you have an AMD GPU, please contact @subhoxx. He might help you optimize your AMD GPU.",
                            "GPU Compatibility Notice",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

        }

        private void optraminfo_Click(object sender, EventArgs e)
        {
            MessageBox.Show("This optimizer is only for PCs/laptops with 8GB RAM. If you have more than 8GB RAM, please contact @subhoxx. He might help you optimize your RAM.",
                            "RAM Compatibility Notice",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

        }

        private void optwininfo_Click(object sender, EventArgs e)
        {
            MessageBox.Show("This optimizer is only for PCs/laptops with Windows 10 or 11. If you don't have Windows 10/11, please contact @subhoxx. He might help you.",
                            "Windows Compatibility Notice",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

        }
        private void typecombobox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!starttoggle1.Checked)
            {
                MessageBox.Show("Toggle is not checked. Action will not proceed.");
                return;
            }

            MessageBox.Show("Maintenance scripts that clear logs, caches, or other forensic artifacts are not shipped in the open-source build.", "Feature unavailable", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private string ExtractEmbeddedResource(string resourceName)
        {
            string tempFilePath = Path.Combine(Path.GetTempPath(), resourceName);
            using (Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LegitX_V2." + resourceName))
            {
                if (resourceStream == null)
                {
                    MessageBox.Show($"Resource {resourceName} not found.");
                    return null;
                }

                using (FileStream fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
                {
                    resourceStream.CopyTo(fileStream);
                }
            }
            return tempFilePath;
        }

        private void stopnet_CheckedChanged(object sender, EventArgs e)
        {
            string exePath = "Path\\To\\Your\\Executable.exe";

            if (stopnet.Checked)
            {
                CreateFirewallRule(exePath);
            }
            else
            {
                RemoveFirewallRule(exePath);
            }
        }

        private void CreateFirewallRule(string exePath)
        {
            string ruleName = "BlockNetworkAccessForExecutable";

            ProcessStartInfo psi = new ProcessStartInfo("netsh", $"advfirewall firewall add rule name=\"{ruleName}\" dir=out action=block program=\"{exePath}\" enable=yes");
            psi.Verb = "runas";
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            using (Process process = Process.Start(psi))
            {
                process.WaitForExit();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                if (!string.IsNullOrEmpty(error))
                {
                    MessageBox.Show("Error creating firewall rule: " + error);
                }
            }
        }

        private void RemoveFirewallRule(string exePath)
        {
            string ruleName = "BlockNetworkAccessForExecutable";

            ProcessStartInfo psi = new ProcessStartInfo("netsh", $"advfirewall firewall delete rule name=\"{ruleName}\" program=\"{exePath}\"");
            psi.Verb = "runas";
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            using (Process process = Process.Start(psi))
            {
                process.WaitForExit();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                if (!string.IsNullOrEmpty(error))
                {
                    MessageBox.Show("Error removing firewall rule: " + error);
                }
            }
        }

        private void pinontop_CheckedChanged(object sender, EventArgs e)
        {
            if (pinontop.Checked)
            {
                this.TopMost = true;
            }
            else
            {
                this.TopMost = false;
            }
        }

        private void driver1_CheckedChanged(object sender, EventArgs e)
        {
            if (driver1.Checked)
            {
                MessageBox.Show("Driver V1 used an unsigned batch file and is not included in the open-source build.", "Feature unavailable", MessageBoxButtons.OK, MessageBoxIcon.Information);
                driver1.Checked = false;
            }
        }
        private void Speak(string message)
        {
            using (SpeechSynthesizer synthesizer = new SpeechSynthesizer())
            {
                synthesizer.Speak(message);
            }
        }

        private void driver2_CheckedChanged(object sender, EventArgs e)
        {
            if (driver2.Checked)
            {
                DialogResult result = MessageBox.Show(
                    "Do you have MSI 4 installed?",
                    "MSI 4 Installation Check",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.No)
                {
                    MessageBox.Show(
                        "To get the full potential of LegitX V2, you have to use MSI 4 Emulator.",
                        "MSI 4 Recommendation",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else if (result == DialogResult.Yes)
                {
                    try
                    {
                        string regFilePath = ExtractEmbeddedResource("CONTROL AIM.reg");
                        if (regFilePath != null)
                        {
                            MergeRegFile(regFilePath);

                            Speak("Driver V2 applied successfully. Now you can install Driver V3.");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void driver3_CheckedChanged(object sender, EventArgs e)
        {
            if (driver3.Checked)
            {
                try
                {
                    string regFilePath = ExtractEmbeddedResource("REAL RED.reg");
                    if (regFilePath != null)
                    {
                        MergeRegFile(regFilePath);
                        Speak("Driver V3 have been applied successfully.");
                    }

                    Speak("Driver V3 have been applied. Please restart your PC for the changes to take effect.");

                    DialogResult result = MessageBox.Show(
                        "Please restart your computer so driver V3 will work without giving any error or problems.",
                        "Restart Required",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information,
                        MessageBoxDefaultButton.Button1);

                    if (result == DialogResult.Yes)
                    {
                        Process.Start("shutdown", "/r /t 0");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void fixdrivers_CheckedChanged(object sender, EventArgs e)
        {
            if (fixdrivers.Checked)
            {
                string[] regFiles = { "fixdriver1.reg", "fixdriver2.reg", "fixdriver3.reg", "fixdriver4.reg", "fixdriver5.reg", "fixdriver6.reg" };
                foreach (string regFile in regFiles)
                {
                    MergeRegistryFile(regFile);
                }

                PlaySuccessMessage();
            }
        }

        private void MergeRegistryFile(string fileName)
        {
            string resourcePath = $"LegitX_V2.{fileName}";
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath))
            {
                if (stream != null)
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string tempPath = Path.GetTempFileName();
                        File.WriteAllText(tempPath, reader.ReadToEnd());

                        Process regeditProcess = new Process();
                        regeditProcess.StartInfo.FileName = "regedit.exe";
                        regeditProcess.StartInfo.Arguments = $"/s \"{tempPath}\"";
                        regeditProcess.StartInfo.UseShellExecute = false;
                        regeditProcess.StartInfo.CreateNoWindow = true;
                        regeditProcess.Start();
                        regeditProcess.WaitForExit();

                        File.Delete(tempPath);
                    }
                }
            }
        }


        private void PlaySuccessMessage()
        {
            SpeechSynthesizer synthesizer = new SpeechSynthesizer();
            synthesizer.Speak("Your all driver are fixed successfully now there will be no bugs or errors that will give you problems in your gameplay");
        }


        private bool aiMouseV1Active = false;
        private bool aiMouseV2Active = false;
        private bool aiMouseV3Active = false;

        private void aimousetype_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!aimousetype.Enabled || aimousetype.SelectedIndex == -1)
                return;

            try
            {
                DisableAllAIMouseVersions();

                string selectedVersion = aimousetype.SelectedItem.ToString();

                switch (selectedVersion)
                {
                    case "AI Mouse V1":
                        ApplyAIMouseV1();
                        break;

                    case "AI Mouse V2":
                        ApplyAIMouseV2();
                        break;

                    case "AI Mouse V3":
                        ApplyAIMouseV3();
                        break;

                    default:
                        MessageBox.Show("Unknown AI Mouse version selected.",
                                       "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in aimousetype_SelectedIndexChanged: {ex.Message}");
                MessageBox.Show("An error occurred while applying AI Mouse settings. Please try again.",
                               "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DisableAllAIMouseVersions()
        {
            try
            {
                if (aiMouseV1Active)
                    DisableAIMouseV1();

                if (aiMouseV2Active)
                    DisableAIMouseV2();

                if (aiMouseV3Active)
                    DisableAIMouseV3();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DisableAllAIMouseVersions: {ex.Message}");
            }
        }

        private void ApplyAIMouseV1()
        {
            try
            {
                aiMouseV1Active = true;

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", true))
                {
                    if (key != null)
                    {
                        SaveCurrentMouseSettings();

                        key.SetValue("MouseSensitivity", "15", RegistryValueKind.String);
                        key.SetValue("MouseSpeed", "1", RegistryValueKind.String);
                        key.SetValue("MouseThreshold1", "3", RegistryValueKind.String);
                        key.SetValue("MouseThreshold2", "5", RegistryValueKind.String);
                    }
                }

                MessageBox.Show("AI Mouse V1 activated. This version provides enhanced mouse precision with moderate sensitivity.",
                               "AI Mouse V1 Activated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                aiMouseV1Active = false;
                throw new Exception($"Failed to activate AI Mouse V1: {ex.Message}");
            }
        }

        private void DisableAIMouseV1()
        {
            try
            {
                aiMouseV1Active = false;

                RestoreOriginalMouseSettings();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to disable AI Mouse V1: {ex.Message}");
            }
        }

        private void ApplyAIMouseV2()
        {
            try
            {
                aiMouseV2Active = true;

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", true))
                {
                    if (key != null)
                    {
                        SaveCurrentMouseSettings();

                        key.SetValue("MouseSensitivity", "17", RegistryValueKind.String);
                        key.SetValue("MouseSpeed", "1", RegistryValueKind.String);
                        key.SetValue("MouseThreshold1", "0", RegistryValueKind.String);
                        key.SetValue("MouseThreshold2", "0", RegistryValueKind.String);

                        key.SetValue("MouseAccel", "1", RegistryValueKind.String);
                    }
                }

                MessageBox.Show("AI Mouse V2 activated. This version provides high precision with enhanced acceleration for quick movements.",
                              "AI Mouse V2 Activated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                aiMouseV2Active = false;
                throw new Exception($"Failed to activate AI Mouse V2: {ex.Message}");
            }
        }

        private void DisableAIMouseV2()
        {
            try
            {
                aiMouseV2Active = false;

                RestoreOriginalMouseSettings();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to disable AI Mouse V2: {ex.Message}");
            }
        }

        private void ApplyAIMouseV3()
        {
            try
            {
                aiMouseV3Active = true;

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", true))
                {
                    if (key != null)
                    {
                        SaveCurrentMouseSettings();

                        key.SetValue("MouseSensitivity", "20", RegistryValueKind.String);
                        key.SetValue("MouseSpeed", "0", RegistryValueKind.String);
                        key.SetValue("MouseThreshold1", "0", RegistryValueKind.String);
                        key.SetValue("MouseThreshold2", "0", RegistryValueKind.String);

                        key.SetValue("MouseAccel", "0", RegistryValueKind.String);
                    }
                }

                MessageBox.Show("AI Mouse V3 activated. This premium version provides maximum precision with 1:1 tracking and no acceleration.",
                              "AI Mouse V3 Activated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                aiMouseV3Active = false;
                throw new Exception($"Failed to activate AI Mouse V3: {ex.Message}");
            }
        }

        private void DisableAIMouseV3()
        {
            try
            {
                aiMouseV3Active = false;

                RestoreOriginalMouseSettings();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to disable AI Mouse V3: {ex.Message}");
            }
        }

        private Dictionary<string, string> originalMouseSettings = new Dictionary<string, string>();

        private void SaveCurrentMouseSettings()
        {
            try
            {
                if (originalMouseSettings.Count == 0)
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", false))
                    {
                        if (key != null)
                        {
                            originalMouseSettings["MouseSensitivity"] = key.GetValue("MouseSensitivity")?.ToString() ?? "10";
                            originalMouseSettings["MouseSpeed"] = key.GetValue("MouseSpeed")?.ToString() ?? "1";
                            originalMouseSettings["MouseThreshold1"] = key.GetValue("MouseThreshold1")?.ToString() ?? "6";
                            originalMouseSettings["MouseThreshold2"] = key.GetValue("MouseThreshold2")?.ToString() ?? "10";
                            originalMouseSettings["MouseAccel"] = key.GetValue("MouseAccel")?.ToString() ?? "1";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving mouse settings: {ex.Message}");
                originalMouseSettings["MouseSensitivity"] = "10";
                originalMouseSettings["MouseSpeed"] = "1";
                originalMouseSettings["MouseThreshold1"] = "6";
                originalMouseSettings["MouseThreshold2"] = "10";
                originalMouseSettings["MouseAccel"] = "1";
            }
        }

        private void RestoreOriginalMouseSettings()
        {
            try
            {
                if (originalMouseSettings.Count > 0)
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", true))
                    {
                        if (key != null)
                        {
                            foreach (var setting in originalMouseSettings)
                            {
                                key.SetValue(setting.Key, setting.Value, RegistryValueKind.String);
                            }
                        }
                    }

                    ApplyMouseSettings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restoring mouse settings: {ex.Message}");
            }
        }

        private void ApplyMouseSettings()
        {
            try
            {
                const uint SPI_SETMOUSESPEED = 0x0071;
                const uint SPIF_UPDATEINIFILE = 0x01;
                const uint SPIF_SENDCHANGE = 0x02;

                int mouseSpeed = 10;
                if (originalMouseSettings.ContainsKey("MouseSensitivity"))
                {
                    int.TryParse(originalMouseSettings["MouseSensitivity"], out mouseSpeed);
                }

                SystemParametersInfo(SPI_SETMOUSESPEED, 0, new IntPtr(mouseSpeed), SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying mouse settings: {ex.Message}");
            }
        }

        private void aimouse_CheckedChanged(object sender, EventArgs e)
        {
            aimousetype.Enabled = aimouse.Checked;

            if (aimouse.Checked)
            {
                aimousetype.Enabled = true;

                if (aimousetype.SelectedIndex == -1)
                {
                    aimousetype.SelectedIndex = 0;
                }

                MessageBox.Show("AI Mouse functionality enabled. Select a version from the dropdown.",
                               "AI Mouse Enabled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                aimousetype.Enabled = false;

                MessageBox.Show("AI Mouse functionality has been disabled.",
                               "AI Mouse Disabled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void aimouseclicking_CheckedChanged(object sender, EventArgs e)
        {
            if (aimouseclicking.Checked)
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("DoubleClickSpeed", "200", RegistryValueKind.String);

                            key.SetValue("SwapMouseButtons", "0", RegistryValueKind.String);
                        }
                    }

                    ApplyMouseSettings(200);

                    MessageBox.Show("Mouse clicking has been optimized for faster response!",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error optimizing mouse clicking: " + ex.Message,
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("DoubleClickSpeed", "500", RegistryValueKind.String);
                        }
                    }

                    ApplyMouseSettings(500);

                    MessageBox.Show("Mouse clicking settings restored to default.",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error restoring mouse settings: " + ex.Message,
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ApplyMouseSettings(int doubleClickTime)
        {
            const uint SPI_SETDOUBLECLICKTIME = 0x0020;
            const uint SPIF_UPDATEINIFILE = 0x01;
            const uint SPIF_SENDCHANGE = 0x02;

            SystemParametersInfo(SPI_SETDOUBLECLICKTIME, (uint)doubleClickTime, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        }

        private void aikeyboard_CheckedChanged(object sender, EventArgs e)
        {
            if (aikeyboard.Checked)
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Keyboard", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("KeyboardDelay", "0", RegistryValueKind.String);

                            key.SetValue("KeyboardSpeed", "31", RegistryValueKind.String);
                        }
                    }

                    ApplyKeyboardSettings(0, 31);

                    MessageBox.Show("Keyboard response time has been optimized!", "Success",
                                   MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error optimizing keyboard: " + ex.Message, "Error",
                                   MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Keyboard", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("KeyboardDelay", "1", RegistryValueKind.String);

                            key.SetValue("KeyboardSpeed", "16", RegistryValueKind.String);
                        }
                    }

                    ApplyKeyboardSettings(1, 16);

                    MessageBox.Show("Keyboard settings restored to default.", "Success",
                                   MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error restoring keyboard settings: " + ex.Message, "Error",
                                   MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ApplyKeyboardSettings(uint delay, uint speed)
        {
            const uint SPI_SETKEYBOARDDELAY = 0x0017;
            const uint SPI_SETKEYBOARDSPEED = 0x000B;
            const uint SPIF_UPDATEINIFILE = 0x01;
            const uint SPIF_SENDCHANGE = 0x02;



            SystemParametersInfo(SPI_SETKEYBOARDDELAY, delay, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

            SystemParametersInfo(SPI_SETKEYBOARDSPEED, speed, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        private void guna2ToggleSwitch4_CheckedChanged(object sender, EventArgs e)
        {
            if (guna2ToggleSwitch4.Checked)
            {
                try
                {
                    ApplyMouseSmoothing();
                    MessageBox.Show("Anti-Recoil enabled!",
                                   "Anti-Recoil Enabled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error applying mouse smoothing: {ex.Message}",
                                   "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                try
                {
                    RestoreDefaultMouseBehavior();
                    MessageBox.Show("Anti-Recoil disabled.",
                                   "Anti-Recoil Disabled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error restoring mouse settings: {ex.Message}",
                                   "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ApplyMouseSmoothing()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", true))
            {
                if (key != null)
                {
                    byte[] smoothX = new byte[]
                    {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x15, 0x6e, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x40, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x29, 0xdc, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x00, 0x00
                    };

                    byte[] smoothY = new byte[]
                    {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x15, 0x6e, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x40, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x29, 0xdc, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x00, 0x00
                    };

                    key.SetValue("SmoothMouseXCurve", smoothX, RegistryValueKind.Binary);
                    key.SetValue("SmoothMouseYCurve", smoothY, RegistryValueKind.Binary);

                    key.SetValue("MouseSensitivity", "12", RegistryValueKind.String);

                    key.SetValue("MouseUpdateRate", "2", RegistryValueKind.String);

                    key.SetValue("MouseStabilizer", "1", RegistryValueKind.String);
                    key.SetValue("MouseSmoothing", "1", RegistryValueKind.String);
                }
            }

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Input\Settings\ControllerProcessor\CursorControl"))
            {
                if (key != null)
                {
                    key.SetValue("CursorAveraging", 3, RegistryValueKind.DWord);

                    key.SetValue("JitterThreshold", 7, RegistryValueKind.DWord);

                    key.SetValue("SmoothingEnabled", 1, RegistryValueKind.DWord);

                    key.SetValue("PredictionEnabled", 1, RegistryValueKind.DWord);
                    key.SetValue("PredictionStrength", 8, RegistryValueKind.DWord);
                }
            }

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\DirectInput"))
            {
                if (key != null)
                {
                    key.SetValue("MouseSmoothing", 1, RegistryValueKind.DWord);

                    key.SetValue("SmoothingStrength", 5, RegistryValueKind.DWord);

                    key.SetValue("JitterReduction", 1, RegistryValueKind.DWord);
                }
            }

            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\mouclass\Parameters"))
            {
                if (key != null)
                {
                    key.SetValue("MouseDataQueueSize", 20, RegistryValueKind.DWord);

                    key.SetValue("SampleRate", 200, RegistryValueKind.DWord);
                }
            }

            UpdateSystemParameters();
        }

        private void RestoreDefaultMouseBehavior()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", true))
            {
                if (key != null)
                {
                    byte[] defaultX = new byte[]
                    {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x38, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x70, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0xA8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0xE0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
                    };

                    byte[] defaultY = new byte[]
                    {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x38, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x70, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0xA8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0xE0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
                    };

                    key.SetValue("SmoothMouseXCurve", defaultX, RegistryValueKind.Binary);
                    key.SetValue("SmoothMouseYCurve", defaultY, RegistryValueKind.Binary);

                    key.SetValue("MouseSensitivity", "10", RegistryValueKind.String);

                    try { key.DeleteValue("MouseUpdateRate"); } catch { }
                    try { key.DeleteValue("MouseStabilizer"); } catch { }
                    try { key.DeleteValue("MouseSmoothing"); } catch { }
                }
            }

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Input\Settings\ControllerProcessor\CursorControl", true))
            {
                if (key != null)
                {
                    try { key.DeleteValue("CursorAveraging"); } catch { }
                    try { key.DeleteValue("JitterThreshold"); } catch { }
                    try { key.DeleteValue("SmoothingEnabled"); } catch { }
                    try { key.DeleteValue("PredictionEnabled"); } catch { }
                    try { key.DeleteValue("PredictionStrength"); } catch { }
                }
            }

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\DirectInput", true))
            {
                if (key != null)
                {
                    try { key.DeleteValue("MouseSmoothing"); } catch { }
                    try { key.DeleteValue("SmoothingStrength"); } catch { }
                    try { key.DeleteValue("JitterReduction"); } catch { }
                }
            }

            UpdateSystemParameters();
        }

        private void UpdateSystemParameters()
        {
            try
            {
                const uint SPI_SETMOUSESPEED = 0x0071;
                const uint SPI_SETCURSORS = 0x0057;
                const uint SPIF_UPDATEINIFILE = 0x01;
                const uint SPIF_SENDCHANGE = 0x02;

                SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                int mouseSpeed = guna2ToggleSwitch4.Checked ? 12 : 10;
                SystemParametersInfo(SPI_SETMOUSESPEED, 0, new IntPtr(mouseSpeed), SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            }
            catch
            {
            }
        }

        private void guna2ToggleSwitch3_CheckedChanged(object sender, EventArgs e)
        {
            if (guna2ToggleSwitch3.Checked)
            {
                try
                {
                    DisableMouseAcceleration();
                    MessageBox.Show("Mouse acceleration completely disabled!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error disabling mouse acceleration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                try
                {
                    RestoreMouseAcceleration();
                    MessageBox.Show("Mouse acceleration restored to Windows default settings.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error restoring mouse acceleration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref MOUSEKEYS pvParam, uint fWinIni);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const uint SPI_GETMOUSE = 0x0003;
        private const uint SPI_SETMOUSE = 0x0004;
        private const uint SPI_GETMOUSESPEED = 0x0070;
        private const uint SPI_SETMOUSESPEED = 0x0071;
        private const uint SPI_GETMOUSEKEYS = 0x0036;
        private const uint SPI_SETMOUSEKEYS = 0x0037;
        private const uint SPIF_UPDATEINIFILE = 0x01;
        private const uint SPIF_SENDCHANGE = 0x02;

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEKEYS
        {
            public uint cbSize;
            public uint dwFlags;
            public uint iMaxSpeed;
            public uint iTimeToMaxSpeed;
            public uint iCtrlSpeed;
            public uint dwReserved1;
            public uint dwReserved2;
        }

        private void DisableMouseAcceleration()
        {
            int[] mouseParams = new int[3] { 0, 0, 0 };
            IntPtr mouseParamsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(mouseParams[0]) * mouseParams.Length);
            Marshal.Copy(mouseParams, 0, mouseParamsPtr, mouseParams.Length);
            SystemParametersInfo(SPI_SETMOUSE, 0, mouseParamsPtr, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            Marshal.FreeHGlobal(mouseParamsPtr);

            IntPtr speedParam = new IntPtr(10);
            SystemParametersInfo(SPI_SETMOUSESPEED, 0, speedParam, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

            MOUSEKEYS mouseKeys = new MOUSEKEYS();
            mouseKeys.cbSize = (uint)Marshal.SizeOf(typeof(MOUSEKEYS));
            SystemParametersInfo(SPI_GETMOUSEKEYS, mouseKeys.cbSize, ref mouseKeys, 0);
            mouseKeys.iMaxSpeed = 0;
            mouseKeys.iTimeToMaxSpeed = 0;
            mouseKeys.iCtrlSpeed = 0;
            SystemParametersInfo(SPI_SETMOUSEKEYS, mouseKeys.cbSize, ref mouseKeys, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", true))
            {
                if (key != null)
                {
                    key.SetValue("MouseSpeed", "0", RegistryValueKind.String);
                    key.SetValue("MouseThreshold1", "0", RegistryValueKind.String);
                    key.SetValue("MouseThreshold2", "0", RegistryValueKind.String);

                    key.SetValue("MouseAccel", "0", RegistryValueKind.String);

                    key.SetValue("MouseSensitivity", "10", RegistryValueKind.String);
                }
            }

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Input\Settings\ControllerProcessor\CursorSpeed"))
            {
                if (key != null)
                {
                    key.SetValue("CursorSensitivity", 0, RegistryValueKind.DWord);
                    key.SetValue("CursorUpdateInterval", 1, RegistryValueKind.DWord);
                    key.SetValue("DeviceAcceleration", 0, RegistryValueKind.DWord);
                }
            }

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Accessibility\MouseKeys"))
            {
                if (key != null)
                {
                    key.SetValue("Acceleration", "0", RegistryValueKind.String);
                    key.SetValue("MaxSpeed", "0", RegistryValueKind.String);
                    key.SetValue("TimeToMaxSpeed", "0", RegistryValueKind.String);
                }
            }

            DisableRazerMouseAcceleration();
            DisableLogitechMouseAcceleration();
            DisableGenericMouseAcceleration();
        }

        private void RestoreMouseAcceleration()
        {
            int[] mouseParams = new int[3] { 0, 0, 0 };
            SystemParametersInfo(SPI_GETMOUSE, 0, Marshal.UnsafeAddrOfPinnedArrayElement(mouseParams, 0), 0);
            mouseParams[2] = 1;
            IntPtr mouseParamsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(mouseParams[0]) * mouseParams.Length);
            Marshal.Copy(mouseParams, 0, mouseParamsPtr, mouseParams.Length);
            SystemParametersInfo(SPI_SETMOUSE, 0, mouseParamsPtr, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            Marshal.FreeHGlobal(mouseParamsPtr);

            IntPtr speedParam = new IntPtr(10);
            SystemParametersInfo(SPI_SETMOUSESPEED, 0, speedParam, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", true))
            {
                if (key != null)
                {
                    key.SetValue("MouseSpeed", "1", RegistryValueKind.String);
                    key.SetValue("MouseThreshold1", "6", RegistryValueKind.String);
                    key.SetValue("MouseThreshold2", "10", RegistryValueKind.String);
                    key.SetValue("MouseSensitivity", "10", RegistryValueKind.String);
                }
            }
        }

        private void DisableRazerMouseAcceleration()
        {
            try
            {
                if (Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Razer")))
                {
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Razer\Synapse\RzSynapse"))
                    {
                        if (key != null)
                        {
                            key.SetValue("acceleration", 0, RegistryValueKind.DWord);
                            key.SetValue("acceleration_enabled", 0, RegistryValueKind.DWord);
                        }
                    }

                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Razer\Synapse3\RazerSynapse"))
                    {
                        if (key != null)
                        {
                            key.SetValue("MouseAcceleration", 0, RegistryValueKind.DWord);
                            key.SetValue("AccelerationEnabled", 0, RegistryValueKind.DWord);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void DisableLogitechMouseAcceleration()
        {
            try
            {
                bool logitechFound = Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Logitech Gaming Software")) ||
                                    Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Logitech Gaming Software")) ||
                                    Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LGHUB")) ||
                                    Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LGHUB"));

                if (logitechFound)
                {
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Logitech\G HUB\PerDeviceSettings"))
                    {
                        if (key != null)
                        {
                            foreach (string subKeyName in key.GetSubKeyNames())
                            {
                                using (RegistryKey deviceKey = key.OpenSubKey(subKeyName, true))
                                {
                                    if (deviceKey != null)
                                    {
                                        deviceKey.SetValue("MouseAcceleration", 0, RegistryValueKind.DWord);
                                        deviceKey.SetValue("AccelerationEnabled", 0, RegistryValueKind.DWord);
                                    }
                                }
                            }
                        }
                    }

                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Logitech\Gaming Software\PerDeviceSettings"))
                    {
                        if (key != null)
                        {
                            foreach (string subKeyName in key.GetSubKeyNames())
                            {
                                using (RegistryKey deviceKey = key.OpenSubKey(subKeyName, true))
                                {
                                    if (deviceKey != null)
                                    {
                                        deviceKey.SetValue("MouseAcceleration", 0, RegistryValueKind.DWord);
                                        deviceKey.SetValue("AccelerationMode", 0, RegistryValueKind.DWord);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void DisableGenericMouseAcceleration()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\mouclass\Parameters", true))
                {
                    if (key != null)
                    {
                        key.SetValue("MouseDataQueueSize", 21, RegistryValueKind.DWord);
                        key.SetValue("MouseSamplingRate", 500, RegistryValueKind.DWord);
                    }
                }

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\DirectInput"))
                {
                    if (key != null)
                    {
                        key.SetValue("MouseAcceleration", 0, RegistryValueKind.DWord);
                        key.SetValue("MouseFilter", 0, RegistryValueKind.DWord);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void backbutton_Click(object sender, EventArgs e)
        {
            extrapanel1.Hide();
            mainpanel2.Hide();
            optpanel1.Hide();
            mainpanel1.Show();
        }

        private void savebutton1_Click(object sender, EventArgs e)
        {
            SaveSettingsToRegistry();
        }

        private void LegitX_Load(object sender, EventArgs e)
        {
            InitializeCreditsDiscordPicture();

            _uiToolTip = new ToolTip
            {
                AutoPopDelay = 8000,
                InitialDelay = 350,
                ReshowDelay = 200
            };
            _uiToolTip.SetToolTip(creditsDiscordIcon, "Open Discord — " + CreditsDiscordInviteUrl);
            _uiToolTip.SetToolTip(creditsDevLabel, "Credits");
            _uiToolTip.SetToolTip(doswidth, "Click to type display width (" + DisplayWidthMin + "–" + DisplayWidthMax + ").");
            _uiToolTip.SetToolTip(dosheight, "Click to type display height (" + DisplayHeightMin + "–" + DisplayHeightMax + ").");
            _uiToolTip.SetToolTip(eoswidth, "Click to type emulator width (" + EmulatorWidthMin + "–" + EmulatorWidthMax + ").");
            _uiToolTip.SetToolTip(eosheight, "Click to type emulator height (" + EmulatorHeightMin + "–" + EmulatorHeightMax + ").");

            RegisterHotKey(this.Handle, HOTKEY_ID, MOD_NOREPEAT, INSERT_KEY);
            settingsMonitorTimer = new System.Windows.Forms.Timer();
            settingsMonitorTimer.Interval = 1000;
            settingsMonitorTimer.Tick += SettingsMonitorTimer_Tick;

            monitorTimer = new System.Windows.Forms.Timer();
            monitorTimer.Interval = 500;
            monitorTimer.Tick += MonitorTimer_Tick;
            InitializeRegistryManager();
            InitializeSensitivitySettings();

            LayoutBottomChrome();
        }

        private void InitializeCreditsDiscordPicture()
        {
            creditsDiscordIcon.Cursor = Cursors.Hand;
            creditsDiscordIcon.SizeMode = PictureBoxSizeMode.Zoom;
            creditsDiscordIcon.BackColor = Color.FromArgb(15, 15, 15);
            creditsDiscordIcon.TabStop = false;

            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream stream = asm.GetManifestResourceStream(CreditsDiscordSvgResource))
                {
                    if (stream == null)
                        return;

                    SvgDocument doc = SvgDocument.Open<SvgDocument>(stream);
                    const int dim = 160;
                    using (Bitmap rendered = doc.Draw(dim, dim))
                    {
                        var copy = new Bitmap(rendered);
                        creditsDiscordIcon.Image?.Dispose();
                        creditsDiscordIcon.Image = copy;
                    }
                }
            }
            catch
            {
            }
        }

        private void creditsDiscordIcon_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = CreditsDiscordInviteUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                ToggleFormVisibility();
                return;
            }

            base.WndProc(ref m);
        }
        private void ToggleFormVisibility()
        {
            if (isFormHidden)
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                isFormHidden = false;
            }
            else
            {
                this.Hide();
                isFormHidden = true;
            }
        }

        private void persistAcrossAppsToggle_CheckedChanged(object sender, EventArgs e)
        {
            if (syncingPersistToggles)
                return;
            persistAcrossFullscreen = persistAcrossAppsToggle.Checked;
            syncingPersistToggles = true;
            try
            {
                if (persistacrossappstoggle2 != null && persistacrossappstoggle2.Checked != persistAcrossAppsToggle.Checked)
                    persistacrossappstoggle2.Checked = persistAcrossAppsToggle.Checked;
            }
            finally
            {
                syncingPersistToggles = false;
            }

            if (overlaySystem != null)
                overlaySystem.ApplySettings(persistAcrossFullscreen);
        }


        private const string SENSITIVITY_REGISTRY_KEY = @"SOFTWARE\LegitX V2\MouseSensitivity";

        private Dictionary<string, double> sensitivityDefaults = new Dictionary<string, double>
        {
            { "GeneralSensitivity", 1.0 },
            { "SensitivityCap", 10.0 },
            { "OffsetSensitivity", 0.0 },
            { "PreScaleX", 1.0 },
            { "PreScaleY", 1.0 },
            { "PostScaleX", 1.0 },
            { "PostScaleY", 1.0 },
            { "Acceleration", 1.0 }
        };

        private bool sensitivityControlActive = false;

        private void InitializeSensitivitySettings()
        {
            LoadSensitivitySettings();

            UpdateSensitivityUI();
        }

        private void LoadSensitivitySettings()
        {
            try
            {
                Dictionary<string, object> profile = RegistryManager.Instance.LoadSettings();

                LoadSensitivityField(profile, "MouseSensitivity", "GeneralSensitivity", "GeneralSensitivity");
                LoadSensitivityField(profile, "SensitivityCap", "SensitivityCap", "SensitivityCap");
                LoadSensitivityField(profile, "OffsetSensitivity", "OffsetSensitivity", "OffsetSensitivity");
                LoadSensitivityField(profile, "PreScaleX", "PreScaleX", "PreScaleX");
                LoadSensitivityField(profile, "PreScaleY", "PreScaleY", "PreScaleY");
                LoadSensitivityField(profile, "PostScaleX", "PostScaleX", "PostScaleX");
                LoadSensitivityField(profile, "PostScaleY", "PostScaleY", "PostScaleY");
                LoadSensitivityField(profile, "Acceleration", "Acceleration", "Acceleration");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}", "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ResetToDefaultSettings();
            }
        }

        private void LoadSensitivityField(
            Dictionary<string, object> profile,
            string profileValueName,
            string legacyValueName,
            string uiSettingName)
        {
            if (TryGetDoubleSetting(profile, profileValueName, out double v)
                || TryGetLegacySensitivityDouble(legacyValueName, out v))
            {
                UpdateSettingTextbox(uiSettingName, v);
            }
            else if (sensitivityDefaults.TryGetValue(uiSettingName, out double d))
            {
                UpdateSettingTextbox(uiSettingName, d);
            }
        }

        private static bool TryGetDoubleSetting(Dictionary<string, object> settings, string key, out double value)
        {
            value = 0;
            if (settings == null || !settings.TryGetValue(key, out object o) || o == null)
                return false;

            try
            {
                value = Convert.ToDouble(o, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryGetLegacySensitivityDouble(string valueName, out double value)
        {
            value = 0;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SENSITIVITY_REGISTRY_KEY, false))
                {
                    if (key == null)
                        return false;

                    object raw = key.GetValue(valueName);
                    if (raw == null)
                        return false;

                    string s = raw.ToString();
                    return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                           || double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
                }
            }
            catch
            {
                return false;
            }
        }

        private void ResetToDefaultSettings()
        {
            foreach (var setting in sensitivityDefaults)
            {
                UpdateSettingTextbox(setting.Key, setting.Value);
            }
        }

        private void UpdateSettingTextbox(string settingName, double value)
        {
            switch (settingName)
            {
                case "GeneralSensitivity":
                    generalsensi.Text = value.ToString("0.00");
                    break;
                case "SensitivityCap":
                    sensilimit.Text = value.ToString("0.00");
                    break;
                case "OffsetSensitivity":
                    offsetsensivity.Text = value.ToString("0.00");
                    break;
                case "PreScaleX":
                    baselinescalex.Text = value.ToString("0.00");
                    break;
                case "PreScaleY":
                    baselinescaley.Text = value.ToString("0.00");
                    break;
                case "PostScaleX":
                    subsequentscalx.Text = value.ToString("0.00");
                    break;
                case "PostScaleY":
                    subsequentscaly.Text = value.ToString("0.00");
                    break;
                case "Acceleration":
                    acceleration.Text = value.ToString("0.00");
                    break;
            }
        }

        private MovementPredictor movementPredictor;

        private readonly object _recentMovementsLock = new object();
        private Queue<Point> recentMovements = new Queue<Point>();
        private const int MovementHistorySize = 20;

        private Label aiConfidenceLabel;

        private void UpdateSensitivityUI()
        {
        }

        private double ParseAndValidateDouble(string input, string fieldName, double minValue, double maxValue)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new FormatException($"{fieldName}: enter a number.");

            input = input.Trim();
            if (!double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                && !double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                throw new FormatException($"{fieldName}: invalid number.");
            }

            if (value < minValue || value > maxValue)
                throw new ArgumentOutOfRangeException(fieldName, $"{fieldName} must be between {minValue} and {maxValue}.");

            return value;
        }

        private void playbutton_Click(object sender, EventArgs e)
        {
            try
            {
                isActive = true;

                if (overlaySystem == null)
                {
                    InitializeOverlaySystem();
                }

                if (overlaySystem != null)
                {
                    overlaySystem.Activate();
                }

                StartMouseHook69();

                if (settingsMonitorTimer == null)
                {
                    settingsMonitorTimer = new System.Windows.Forms.Timer();
                    settingsMonitorTimer.Interval = 1000;
                    settingsMonitorTimer.Tick += SettingsMonitorTimer_Tick;
                }

                settingsMonitorTimer.Start();

                if (!sensitivityControlActive)
                {
                    try
                    {
                        ValidateAllSettings();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Settings validation failed: {ex.Message}. Using defaults.");

                        MessageBox.Show(
                            "Some settings had invalid values and will be reset to defaults.",
                            "Settings Warning",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);

                        ResetToDefaultSettings();
                    }

                    SensitivitySettings settings = new SensitivitySettings();
                    try
                    {
                        settings.GeneralSensitivity = ParseAndValidateDouble(generalsensi.Text, "General Sensitivity", 0.01, 100.0);
                        settings.SensitivityCap = ParseAndValidateDouble(sensilimit.Text, "Sensitivity Cap", 0.1, 1000.0);
                        settings.OffsetSensitivity = ParseAndValidateDouble(offsetsensivity.Text, "Offset Sensitivity", -100.0, 100.0);
                        settings.PreScaleX = ParseAndValidateDouble(baselinescalex.Text, "Baseline Scale X", 0.01, 100.0);
                        settings.PreScaleY = ParseAndValidateDouble(baselinescaley.Text, "Baseline Scale Y", 0.01, 100.0);
                        settings.PostScaleX = ParseAndValidateDouble(subsequentscalx.Text, "Subsequent Scale X", 0.01, 100.0);
                        settings.PostScaleY = ParseAndValidateDouble(subsequentscaly.Text, "Subsequent Scale Y", 0.01, 100.0);
                        settings.Acceleration = ParseAndValidateDouble(acceleration.Text, "Acceleration", 0.01, 10.0);

                        settings.EnableSmoothing = true;
                        settings.SmoothingWindowSize = 3;
                        settings.SmoothingWeight = 0.5;
                        settings.EnableAntiJitter = true;
                        settings.JitterThreshold = 2.0;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating settings: {ex.Message}. Using defaults.");
                    }

                    try
                    {
                        if (sensitivityProcessor == null)
                        {
                            sensitivityProcessor = new SensitivityProcessor(settings);
                        }
                        else
                        {
                            sensitivityProcessor.UpdateSettings(settings);
                            sensitivityProcessor.Reset();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error initializing sensitivity processor: {ex.Message}");
                    }

                    try
                    {
                        if (movementPredictor == null)
                        {
                            movementPredictor = new MovementPredictor();
                        }
                        else
                        {
                            movementPredictor.Reset();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error initializing movement predictor: {ex.Message}");
                    }

                    sensitivityControlActive = true;

                    playbutton.Hide();
                    pausebutton2.Show();

                    string aiInfo = movementPredictor != null ? "\n\nAI Enhancement: Active" : "\n\nAI Enhancement: Inactive";
                    MessageBox.Show($"Mouse sensitivity control activated!{aiInfo}", "Activated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"An unexpected error occurred: {ex.Message}\n\nSome functionality may be limited.",
                    "Activation Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void pausebutton2_Click(object sender, EventArgs e)
        {
            try
            {
                if (sensitivityProcessor != null)
                {
                    sensitivityProcessor.Reset();
                }

                if (movementPredictor != null)
                {
                    movementPredictor.Reset();
                }

                lock (_recentMovementsLock)
                    recentMovements.Clear();

                StopMouseHook69();

                sensitivityControlActive = false;

                if (overlaySystem != null)
                {
                    overlaySystem.Deactivate();
                }

                if (settingsMonitorTimer != null && settingsMonitorTimer.Enabled)
                {
                    settingsMonitorTimer.Stop();
                }

                pausebutton2.Hide();
                playbutton.Show();

                MessageBox.Show("Mouse sensitivity control paused.", "Paused", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"An error occurred while pausing: {ex.Message}",
                    "Pause Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                sensitivityControlActive = false;
                lastMousePosition = Point.Empty;

                pausebutton2.Hide();
                playbutton.Show();
            }
        }

        private void savebutton2_Click(object sender, EventArgs e)
        {
            try
            {
                ValidateAllSettings();
                SaveAllSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void refreshbutton2_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "Reset all settings to default values?",
                "Confirm Reset",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                ResetToDefaultSettings();
                MessageBox.Show("Settings have been reset to defaults.", "Reset Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ValidateAllSettings()
        {
            ParseAndValidateDouble(generalsensi.Text, "General Sensitivity", 0.01, 100.0);
            ParseAndValidateDouble(sensilimit.Text, "Sensitivity Cap", 0.1, 1000.0);
            ParseAndValidateDouble(offsetsensivity.Text, "Offset Sensitivity", -100.0, 100.0);
            ParseAndValidateDouble(baselinescalex.Text, "Baseline Scale X", 0.01, 100.0);
            ParseAndValidateDouble(baselinescaley.Text, "Baseline Scale Y", 0.01, 100.0);
            ParseAndValidateDouble(subsequentscalx.Text, "Subsequent Scale X", 0.01, 100.0);
            ParseAndValidateDouble(subsequentscaly.Text, "Subsequent Scale Y", 0.01, 100.0);
            ParseAndValidateDouble(acceleration.Text, "Acceleration", 0.01, 10.0);
        }
        private EnhancedMouseHook enhancedMouseHook;

        private void StartMouseHook69()
        {
            try
            {
                if (enhancedMouseHook == null)
                {
                    enhancedMouseHook = new EnhancedMouseHook();
                    enhancedMouseHook.MouseMoved += EnhancedMouseHook_MouseMoved;
                }

                if (!enhancedMouseHook.IsActive && enhancedMouseHook.Start())
                {
                    Console.WriteLine("Enhanced mouse hook started successfully");
                }
                else if (!enhancedMouseHook.IsActive)
                {
                    MessageBox.Show(
                        "The mouse hook couldn't be initialized. This might be due to another application using a global mouse hook.\n\n" +
                        "LegitX will continue to function with limited capabilities. Try closing some applications and restarting LegitX.",
                        "Mouse Hook Warning",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing mouse hook: {ex.Message}\n\nTry running LegitX as administrator.",
                    "Hook Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopMouseHook69()
        {
            if (enhancedMouseHook != null && enhancedMouseHook.IsActive)
            {
                if (enhancedMouseHook.Stop())
                {
                    Console.WriteLine("Enhanced mouse hook stopped successfully");
                }
                else
                {
                    Console.WriteLine("Failed to properly stop enhanced mouse hook");
                }

                lastMousePosition = Point.Empty;
            }
        }

        private SensitivityProcessor sensitivityProcessor;

        private void MouseHook_MouseMoved(object sender, MouseHook.MouseHookEventArgs e)
        {
            if (!sensitivityControlActive || processingMouseMove)
                return;

            try
            {
                processingMouseMove = true;

                Point currentPosition = e.Position;

                if (lastMousePosition.IsEmpty)
                {
                    lastMousePosition = currentPosition;
                    return;
                }

                int deltaX = currentPosition.X - lastMousePosition.X;
                int deltaY = currentPosition.Y - lastMousePosition.Y;

                if (deltaX == 0 && deltaY == 0)
                    return;

                double generalSensitivity = ParseAndValidateDouble(generalsensi.Text, "General Sensitivity", 0.01, 100.0);
                double preScaleX = ParseAndValidateDouble(baselinescalex.Text, "Baseline Scale X", 0.01, 100.0);
                double preScaleY = ParseAndValidateDouble(baselinescaley.Text, "Baseline Scale Y", 0.01, 100.0);
                double postScaleX = ParseAndValidateDouble(subsequentscalx.Text, "Subsequent Scale X", 0.01, 100.0);
                double postScaleY = ParseAndValidateDouble(subsequentscaly.Text, "Subsequent Scale Y", 0.01, 100.0);

                double scaledDeltaX = deltaX * generalSensitivity * preScaleX * postScaleX;
                double scaledDeltaY = deltaY * generalSensitivity * preScaleY * postScaleY;

                int newX = lastMousePosition.X + (int)Math.Round(scaledDeltaX);
                int newY = lastMousePosition.Y + (int)Math.Round(scaledDeltaY);

                Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
                newX = Math.Max(screenBounds.Left, Math.Min(newX, screenBounds.Right - 1));
                newY = Math.Max(screenBounds.Top, Math.Min(newY, screenBounds.Bottom - 1));

                MouseHook.SetCursorPosition(newX, newY);

                lastMousePosition = new Point(newX, newY);

                if (!e.Handled)
                {
                    try
                    {
                        if (movementPredictor != null)
                            movementPredictor.PredictMovement(currentPosition, lastMousePosition);
                        UpdateRecentMovements(currentPosition);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in AI processing: {ex.Message}");
                    }
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing mouse movement: {ex.Message}");
            }
            finally
            {
                processingMouseMove = false;
            }
        }




        private void EnhancedMouseHook_MouseMoved(object sender, EnhancedMouseHook.MouseEventArgs e)
        {
            if (!sensitivityControlActive || processingMouseMove)
                return;

            try
            {
                processingMouseMove = true;

                Point currentPosition = e.Position;

                if (lastMousePosition.IsEmpty)
                {
                    lastMousePosition = currentPosition;
                    return;
                }

                int deltaX = currentPosition.X - lastMousePosition.X;
                int deltaY = currentPosition.Y - lastMousePosition.Y;

                if (deltaX == 0 && deltaY == 0)
                    return;

                double generalSensitivity = 1.0;
                double preScaleX = 1.0;
                double preScaleY = 1.0;
                double postScaleX = 1.0;
                double postScaleY = 1.0;

                try
                {
                    generalSensitivity = ParseAndValidateDouble(generalsensi.Text, "General Sensitivity", 0.01, 100.0);
                    preScaleX = ParseAndValidateDouble(baselinescalex.Text, "Baseline Scale X", 0.01, 100.0);
                    preScaleY = ParseAndValidateDouble(baselinescaley.Text, "Baseline Scale Y", 0.01, 100.0);
                    postScaleX = ParseAndValidateDouble(subsequentscalx.Text, "Subsequent Scale X", 0.01, 100.0);
                    postScaleY = ParseAndValidateDouble(subsequentscaly.Text, "Subsequent Scale Y", 0.01, 100.0);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing sensitivity values: {ex.Message}. Using defaults.");
                }

                double scaledDeltaX = deltaX * generalSensitivity * preScaleX * postScaleX;
                double scaledDeltaY = deltaY * generalSensitivity * preScaleY * postScaleY;

                int newX = lastMousePosition.X + (int)Math.Round(scaledDeltaX);
                int newY = lastMousePosition.Y + (int)Math.Round(scaledDeltaY);

                Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
                newX = Math.Max(screenBounds.Left, Math.Min(newX, screenBounds.Right - 1));
                newY = Math.Max(screenBounds.Top, Math.Min(newY, screenBounds.Bottom - 1));

                EnhancedMouseHook.SetCursorPosition(newX, newY);

                lastMousePosition = new Point(newX, newY);

                try
                {
                    if (movementPredictor != null)
                        movementPredictor.PredictMovement(currentPosition, lastMousePosition);
                    UpdateRecentMovements(currentPosition);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in AI processing: {ex.Message}");
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing mouse movement: {ex.Message}");
            }
            finally
            {
                processingMouseMove = false;
            }
        }

        private void CleanupMouseHook()
        {
            if (enhancedMouseHook != null)
            {
                enhancedMouseHook.Stop();
                enhancedMouseHook.Dispose();
                enhancedMouseHook = null;
            }
        }

        private void UpdateRecentMovements(Point position)
        {
            lock (_recentMovementsLock)
            {
                recentMovements.Enqueue(position);

                while (recentMovements.Count > MovementHistorySize)
                    recentMovements.Dequeue();
            }
        }

        private string GetProcessNameFromWindow(IntPtr hwnd)
        {
            try
            {
                int pid;
                NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
                if (pid > 0)
                {
                    using (Process process = Process.GetProcessById(pid))
                    {
                        return process.ProcessName;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting process name: {ex.Message}");
            }
            return "Unknown";
        }

        private void RunAiPeriodicDiagnostics()
        {
            if (movementPredictor == null)
                return;

            var metrics = movementPredictor.GetBehaviorMetrics();

            if (DateTime.Now.Second % 10 == 0)
            {
                string aiState = movementPredictor.GetModelState();
                Console.WriteLine($"AI State: {aiState}");

                Console.WriteLine($"User Behavior - Speed: {metrics["AverageSpeed"]:F2}, " +
                                 $"Variance: {metrics["SpeedVariance"]:F2}, " +
                                 $"Direction Changes: {metrics["DirectionChangeFrequency"]:F2}, " +
                                 $"Jitter: {metrics["JitterLevel"]:F2}");
            }
        }

        private void AddAiStatusLabel()
        {
            if (aiConfidenceLabel == null)
            {
                aiConfidenceLabel = new Label();
                aiConfidenceLabel.AutoSize = true;
                aiConfidenceLabel.Location = new Point(10, 10);
                aiConfidenceLabel.Text = "AI Confidence: Initializing...";
                aiConfidenceLabel.ForeColor = Color.Gray;

                this.Controls.Add(aiConfidenceLabel);
                aiConfidenceLabel.BringToFront();
            }
        }

        private OverlaySystem overlaySystem;

        private void InitializeOverlaySystem()
        {
            overlaySystem = new OverlaySystem();

            overlaySystem.SetPerformanceLevel(8);
        }

        private void InitializeRegistryManager()
        {
            try
            {
                RegistryManager regManager = RegistryManager.Instance;

                if (!string.IsNullOrEmpty(currentAppName))
                    regManager.LoadApplicationSettings(currentAppName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing settings: {ex.Message}", "Settings Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveAllSettings()
        {
            try
            {
                Dictionary<string, object> settings = new Dictionary<string, object>
        {
            { "MouseSensitivity", ParseAndValidateDouble(generalsensi.Text, "General Sensitivity", 0.01, 100.0) },
            { "SensitivityCap", ParseAndValidateDouble(sensilimit.Text, "Sensitivity Cap", 0.1, 1000.0) },
            { "OffsetSensitivity", ParseAndValidateDouble(offsetsensivity.Text, "Offset Sensitivity", -100.0, 100.0) },
            { "PreScaleX", ParseAndValidateDouble(baselinescalex.Text, "Baseline Scale X", 0.01, 100.0) },
            { "PreScaleY", ParseAndValidateDouble(baselinescaley.Text, "Baseline Scale Y", 0.01, 100.0) },
            { "PostScaleX", ParseAndValidateDouble(subsequentscalx.Text, "Subsequent Scale X", 0.01, 100.0) },
            { "PostScaleY", ParseAndValidateDouble(subsequentscaly.Text, "Subsequent Scale Y", 0.01, 100.0) },
            { "Acceleration", ParseAndValidateDouble(acceleration.Text, "Acceleration", 0.01, 10.0) },
        };

                bool ok = RegistryManager.Instance.SaveSettings(settings);
                if (!ok)
                {
                    MessageBox.Show("Could not save profile to registry.", "LegitX V2", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!string.IsNullOrEmpty(currentAppName) && isActive)
                {
                    Dictionary<string, object> appSettings = new Dictionary<string, object>
                    {
                        { "GeneralSensitivity", mousesensi.Value },
                        { "XSensitivity", xaxissensi.Value },
                        { "YSensitivity", yaxissensi.Value },
                        { "IsActive", isActive }
                    };

                    RegistryManager.Instance.SaveApplicationSettings(currentAppName, appSettings);
                }

                if (isActive)
                    ApplySettings();

                string profileName = RegistryManager.Instance.CurrentProfile;
                MessageBox.Show(
                    $"Saved to profile \"{profileName}\" (registry: HKCU\\SOFTWARE\\LegitX V2\\Profiles\\{profileName}).",
                    "LegitX V2",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save settings.{Environment.NewLine}{ex.Message}", "LegitX V2",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void persistacrossappstoggle2_CheckedChanged(object sender, EventArgs e)
        {
            if (syncingPersistToggles)
                return;
            var sw = sender as Guna2ToggleSwitch;
            persistAcrossFullscreen = sw != null ? sw.Checked : persistacrossappstoggle2.Checked;
            syncingPersistToggles = true;
            try
            {
                if (persistAcrossAppsToggle != null && persistAcrossAppsToggle.Checked != persistAcrossFullscreen)
                    persistAcrossAppsToggle.Checked = persistAcrossFullscreen;
            }
            finally
            {
                syncingPersistToggles = false;
            }

            if (overlaySystem != null)
                overlaySystem.ApplySettings(persistAcrossFullscreen);
        }
    }
}
