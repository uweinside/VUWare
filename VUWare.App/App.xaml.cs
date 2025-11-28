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
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Clean up tray manager
            _trayManager?.Dispose();
            
            base.OnExit(e);
        }
    }

}
