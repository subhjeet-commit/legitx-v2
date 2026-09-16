using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Security.Principal;
using System.Threading;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace LegitX_V2
{
    internal static class Program
    {
        private const string SingleInstanceMutexName = @"Global\LegitX_V2_SingleInstance_Mutex";

        private static Mutex mutex;

        private const int Net481MinimumReleaseDword = 533320;

        private const string DotNet481DownloadPageUrl = "https://dotnet.microsoft.com/en-us/download/dotnet-framework/net481";

        [STAThread]
        static void Main()
        {
            bool ownsMutex = false;
            try
            {
                if (!VerifyRuntimePrerequisites())
                    return;

                mutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);
                if (!createdNew)
                {
                    mutex.Dispose();
                    mutex = null;
                    MessageBox.Show("Another instance is already running.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ownsMutex = true;

                if (!IsAdministrator())
                {
                    MessageBox.Show("Run as administrator.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new LegitX());
            }
            catch (Exception ex)
            {
                try
                {
                    string plain = ex.ToString();
                    File.WriteAllText("LegitX_last_error.txt", plain);
                }
                catch { }

                string msg = "Application encountered an unexpected error." + Environment.NewLine + Environment.NewLine
                    + ex.GetType().Name + ": " + ex.Message + Environment.NewLine + Environment.NewLine
                    + "Details were written to LegitX_last_error.txt next to the executable.";
                MessageBox.Show(msg, "LegitX V2", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (ownsMutex && mutex != null)
                {
                    try { mutex.ReleaseMutex(); }
                    catch (ApplicationException) { }
                }
                mutex?.Dispose();
                mutex = null;
            }
        }

        private static bool VerifyRuntimePrerequisites()
        {
            if (!IsDotNet481OrLaterInstalled())
            {
                ShowMissingRequirementDialog(
                    "Microsoft .NET Framework 4.8.1 is not installed (or is too old).",
                    "LegitX V2 is built for .NET Framework 4.8.1. Install that runtime from Microsoft, then run this program again.",
                    "Open download page",
                    () => TryOpenUrl(DotNet481DownloadPageUrl));
                return false;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
            if (!TryLoadGunaUi2Assembly())
            {
                ShowMissingRequirementDialog(
                    "The Guna.UI2 user interface library could not be loaded.",
                    "This build embeds dependencies inside LegitX V2.exe. If you see this message, the executable may be damaged or incomplete. Reinstall from your original package, or run from the folder produced by a full Release build.",
                    "Open app folder",
                    () => TryOpenFolderInExplorer(baseDir));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Confirms Guna.UI2 is available (either next to the exe or embedded via Costura).
        /// A plain file check is wrong for single-file / Fody-embedded builds.
        /// </summary>
        private static bool TryLoadGunaUi2Assembly()
        {
            try
            {
                Assembly.Load(
                    "Guna.UI2, Version=2.0.4.6, Culture=neutral, PublicKeyToken=8b9d14aa5142e261");
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (BadImageFormatException)
            {
                return false;
            }
        }

        private static bool IsDotNet481OrLaterInstalled()
        {
            int best = 0;
            foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                try
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (RegistryKey key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
                    {
                        if (key == null)
                            continue;
                        object v = key.GetValue("Release");
                        if (v is int i)
                            best = Math.Max(best, i);
                        else if (v != null)
                            best = Math.Max(best, Convert.ToInt32(v, CultureInfo.InvariantCulture));
                    }
                }
                catch
                {
                }
            }

            return best >= Net481MinimumReleaseDword;
        }

        private static void ShowMissingRequirementDialog(string summary, string details, string primaryButtonText, Action primaryAction)
        {
            try
            {
                using (Form form = new Form())
                {
                    form.Text = "LegitX V2 — required component";
                    form.StartPosition = FormStartPosition.CenterScreen;
                    form.FormBorderStyle = FormBorderStyle.FixedDialog;
                    form.MinimizeBox = false;
                    form.MaximizeBox = false;
                    form.ShowInTaskbar = true;
                    form.ClientSize = new Size(440, 168);

                    Label label = new Label
                    {
                        AutoSize = false,
                        Location = new Point(16, 12),
                        Size = new Size(408, 88),
                        Text = summary + Environment.NewLine + Environment.NewLine + details
                    };

                    Button primary = new Button
                    {
                        Text = primaryButtonText,
                        Location = new Point(16, 112),
                        Size = new Size(180, 30),
                        DialogResult = DialogResult.None
                    };
                    primary.Click += (s, e) =>
                    {
                        try { primaryAction?.Invoke(); }
                        catch { }
                        form.Close();
                    };

                    Button close = new Button
                    {
                        Text = "Close",
                        Location = new Point(328, 112),
                        Size = new Size(96, 30),
                        DialogResult = DialogResult.Cancel
                    };
                    close.Click += (s, e) => form.Close();

                    form.Controls.Add(label);
                    form.Controls.Add(primary);
                    form.Controls.Add(close);
                    form.CancelButton = close;
                    form.AcceptButton = primary;

                    form.ShowDialog();
                }
            }
            catch
            {
                try
                {
                    MessageBox.Show(summary + Environment.NewLine + Environment.NewLine + details, "LegitX V2",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch { }
            }
        }

        private static void TryOpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                try
                {
                    MessageBox.Show(
                        "Could not open your browser automatically. Copy this link:" + Environment.NewLine + Environment.NewLine + url,
                        "LegitX V2",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch { }
            }
        }

        private static void TryOpenFolderInExplorer(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return;
            string path = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(path))
                return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + path + "\"",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

    }
}
