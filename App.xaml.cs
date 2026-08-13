using System;
using System.IO;
using System.Windows;
using YtDlpGui.Services;
using Wpf.Ui.Appearance;

namespace YtDlpGui
{
    public partial class App : Application
    {
        private System.Threading.Mutex? _mutex;

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_RESTORE = 9;

#if CANDY_PLUS
        public static bool IsPlusVersion { get; } = true;
#else
        public static bool IsPlusVersion { get; } = false;
#endif

        protected override void OnStartup(StartupEventArgs e)
        {
            string mutexName = IsPlusVersion ? "CandyPlus_SingleInstanceMutex_App" : "Candy_SingleInstanceMutex_App";
            _mutex = new System.Threading.Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                string windowTitle = IsPlusVersion ? "Candy Plus" : "Candy";
                IntPtr hWnd = FindWindow(null, windowTitle);

                if (hWnd != IntPtr.Zero)
                {
                    if (IsIconic(hWnd))
                    {
                        ShowWindow(hWnd, SW_RESTORE);
                    }
                    SetForegroundWindow(hWnd);
                }

                Application.Current.Shutdown();
                return;
            }

            base.OnStartup(e);
            
            // Apply saved theme
            var settingsService = new SettingsService();
            var settings = settingsService.Load();
            ApplyTheme(settings.Theme);

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                File.WriteAllText("crash.log", ex?.ToString() ?? "Unknown error");
            };
            DispatcherUnhandledException += (s, args) =>
            {
                File.WriteAllText("crash.log", args.Exception.ToString());
                args.Handled = false;
            };
        }

        public static void ApplyTheme(string theme)
        {
            switch (theme)
            {
                case "Light":
                    ApplicationThemeManager.Apply(ApplicationTheme.Light);
                    break;
                case "Dark":
                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    break;
                case "System":
                default:
                    ApplicationThemeManager.ApplySystemTheme();
                    break;
            }
        }
    }
}
