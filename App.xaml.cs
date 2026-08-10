using System;
using System.IO;
using System.Windows;
using YtDlpGui.Services;
using Wpf.Ui.Appearance;

namespace YtDlpGui
{
    public partial class App : Application
    {
#if CANDY_PLUS
        public static bool IsPlusVersion { get; } = true;
#else
        public static bool IsPlusVersion { get; } = false;
#endif

        protected override void OnStartup(StartupEventArgs e)
        {
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
