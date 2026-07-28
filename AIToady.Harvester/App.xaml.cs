using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace AIToady.Harvester
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += (s, ex) =>
            {
                LogCrash("DispatcherUnhandledException", ex.Exception);
                ex.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
                LogCrash("AppDomain.UnhandledException", ex.ExceptionObject as Exception);

            TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                LogCrash("UnobservedTaskException", ex.Exception);
                ex.SetObserved();
            };
        }

        private static void LogCrash(string source, Exception ex)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                string entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{source}] {ex?.GetType().Name}: {ex?.Message}\n{ex?.StackTrace}\n\n";
                File.AppendAllText(logPath, entry);
            }
            catch { }
        }
    }
}
