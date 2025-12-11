using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;

namespace VUWare.App
{
    /// <summary>
    /// Interaction logic for AboutDialog.xaml
    /// </summary>
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
            
            // Set version from assembly
            SetVersionInfo();
            
            // Set hyperlink URLs
            SetHyperlinkUrls();
        }

        /// <summary>
        /// Sets the version information from the assembly.
        /// </summary>
        private void SetVersionInfo()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                
                if (version != null)
                {
                    VersionText.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
                }
            }
            catch
            {
                VersionText.Text = "Version 1.0.0";
            }
        }

        /// <summary>
        /// Sets the NavigateUri for all hyperlinks.
        /// </summary>
        private void SetHyperlinkUrls()
        {
            GitHubLink.NavigateUri = new Uri("https://github.com/uweinside/VUWare");
            VUDialsLink.NavigateUri = new Uri("https://vudials.com");
            HWiNFOLink.NavigateUri = new Uri("https://www.hwinfo.com");
            OriginalAuthorLink.NavigateUri = new Uri("https://vudials.com");
        }

        /// <summary>
        /// Handles hyperlink navigation by opening in default browser.
        /// </summary>
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to open link:\n{e.Uri.AbsoluteUri}\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles title bar drag to move window.
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        /// <summary>
        /// Closes the dialog.
        /// </summary>
        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Closes the dialog.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
