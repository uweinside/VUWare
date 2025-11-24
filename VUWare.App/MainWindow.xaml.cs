using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VUWare.App.Models;
using VUWare.App.Services;

namespace VUWare.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DialsConfiguration? _config;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            try
            {
                string configPath = ConfigManager.GetDefaultConfigPath();
                _config = ConfigManager.LoadDefault();

                if (_config == null)
                {
                    MessageBox.Show(
                        $"No dial configuration found.\n\n" +
                        $"Checked location: {configPath}\n\n" +
                        $"Please ensure dials-config.json exists in either:\n" +
                        $"1. AppData\\VUWare\\ directory, or\n" +
                        $"2. The application's Config\\ directory",
                        "Configuration Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Validate configuration
                if (!_config.Validate(out var errors))
                {
                    string errorMessage = string.Join(Environment.NewLine, errors);
                    MessageBox.Show(
                        $"Configuration validation failed:\n\n{errorMessage}",
                        "Configuration Invalid",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // Successfully loaded and validated
                Title = $"VUWare - {_config.Dials.Count} dial(s) configured";

                if (_config.AppSettings.DebugMode)
                {
                    MessageBox.Show(
                        $"✓ Configuration loaded successfully\n\n" +
                        $"Dials: {_config.Dials.Count}\n" +
                        $"Config file: {configPath}",
                        "Debug: Configuration Loaded",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load configuration:\n\n{ex.GetType().Name}: {ex.Message}\n\n" +
                    $"Base directory: {AppDomain.CurrentDomain.BaseDirectory}",
                    "Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}