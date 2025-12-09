using System.Configuration;
using System.Data;
using System.Windows;
using VUWare.App.Services;

namespace VUWare.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private SystemTrayManager? _trayManager;

        public SystemTrayManager? TrayManager => _trayManager;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize tray manager early so it's ready
            _trayManager = new SystemTrayManager();
            System.Diagnostics.Debug.WriteLine("[App] Tray manager created");
            
            // Handle Windows shutdown/logoff events
            SessionEnding += App_SessionEnding;
        }

        private void App_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Windows session ending: {e.ReasonSessionEnding}");
            
            // Trigger graceful shutdown through MainWindow if it exists
            if (MainWindow is MainWindow mainWindow)
            {
                System.Diagnostics.Debug.WriteLine("[App] Triggering MainWindow shutdown sequence");
                mainWindow.PerformGracefulShutdown();
            }
            
            // Don't cancel the session ending
            e.Cancel = false;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[App] Application exiting");
            
            // Clean up tray manager
            _trayManager?.Dispose();
            
            base.OnExit(e);
        }
    }

}
