using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace VUWare.App.Services
{
    /// <summary>
    /// Manages system tray icon and interactions for minimizing/restoring the application window.
    /// Uses WinForms NotifyIcon for true system tray functionality.
    /// </summary>
    public class SystemTrayManager : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private Window? _mainWindow;

        /// <summary>
        /// Initializes the system tray with the specified main window.
        /// </summary>
        public void Initialize(Window mainWindow)
        {
            _mainWindow = mainWindow;

            // Create the NotifyIcon (system tray icon)
            _notifyIcon = new NotifyIcon
            {
                Text = "VU1 Dial Sensor Monitor",
                Visible = false
            };

            // Try to load custom icon
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VU1_Icon.ico");
                if (File.Exists(iconPath))
                {
                    _notifyIcon.Icon = new Icon(iconPath);
                }
                else
                {
                    _notifyIcon.Icon = SystemIcons.Application;
                }
            }
            catch
            {
                _notifyIcon.Icon = SystemIcons.Application;
            }

            // Create context menu
            var contextMenu = new ContextMenuStrip();

            // Show/Restore menu item
            var showItem = new ToolStripMenuItem("Show");
            showItem.Click += (s, e) => ShowWindow();
            contextMenu.Items.Add(showItem);

            // Settings menu item
            var settingsItem = new ToolStripMenuItem("Settings");
            settingsItem.Click += (s, e) => OpenSettings();
            contextMenu.Items.Add(settingsItem);

            // Separator
            contextMenu.Items.Add(new ToolStripSeparator());

            // Exit menu item
            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => ExitApplication();
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;

            // Handle double-click to restore window
            _notifyIcon.MouseDoubleClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ShowWindow();
                }
            };

            System.Diagnostics.Debug.WriteLine("[SystemTray] Initialized successfully with NotifyIcon");
        }

        /// <summary>
        /// Shows and restores the main window from tray.
        /// </summary>
        public void ShowWindow()
        {
            if (_mainWindow == null)
                return;

            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            _mainWindow.Focus();
            _mainWindow.ShowInTaskbar = true;

            System.Diagnostics.Debug.WriteLine("[SystemTray] Window shown and activated");
        }

        /// <summary>
        /// Hides the main window to the system tray.
        /// </summary>
        public void HideToTray()
        {
            if (_mainWindow == null)
                return;

            _mainWindow.Hide();
            _mainWindow.ShowInTaskbar = false;

            System.Diagnostics.Debug.WriteLine("[SystemTray] Window hidden to tray");
        }

        /// <summary>
        /// Shows the tray icon.
        /// </summary>
        public void ShowIcon()
        {
            if (_notifyIcon != null && !_notifyIcon.Visible)
            {
                _notifyIcon.Visible = true;
                System.Diagnostics.Debug.WriteLine("[SystemTray] Tray icon shown");
            }
        }

        /// <summary>
        /// Hides the tray icon.
        /// </summary>
        public void HideIcon()
        {
            if (_notifyIcon != null && _notifyIcon.Visible)
            {
                _notifyIcon.Visible = false;
                System.Diagnostics.Debug.WriteLine("[SystemTray] Tray icon hidden");
            }
        }

        /// <summary>
        /// Opens the settings window.
        /// </summary>
        private void OpenSettings()
        {
            ShowWindow();
            if (_mainWindow != null)
            {
                SettingsWindow settingsWindow = new SettingsWindow();
                settingsWindow.Owner = _mainWindow;
                settingsWindow.ShowDialog();
            }
        }

        /// <summary>
        /// Exits the application.
        /// </summary>
        private void ExitApplication()
        {
            if (_mainWindow != null)
            {
                _mainWindow.Close();
            }
        }

        /// <summary>
        /// Removes the tray icon and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
    }
}
