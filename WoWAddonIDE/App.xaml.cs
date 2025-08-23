using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace WoWAddonIDE
{
    public partial class App : Application
    {
        public App()
        {
            // log unhandled errors so “nothing shows” becomes actionable
            this.DispatcherUnhandledException += (s, e) =>
            {
                LogCrash("Dispatcher", e.Exception);
                MessageBox.Show(e.Exception.Message, "Unhandled UI Exception");
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex) LogCrash("AppDomain", ex);
            };
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogCrash("TaskScheduler", e.Exception);
                e.SetObserved();
            };
        }

        private static void LogCrash(string kind, Exception ex)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                       "WoWAddonIDE");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "crash.log");
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {kind}: {ex}\n\n");
            }
            catch { /* ignore */ }
        }
    }
}