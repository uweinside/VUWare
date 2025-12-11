using System.Windows.Controls;
using VUWare.App.ViewModels;

namespace VUWare.App
{
    /// <summary>
    /// Interaction logic for DialConfigurationPanel.xaml
    /// </summary>
    public partial class DialConfigurationPanel : UserControl
    {
        private VUWare.Lib.VU1Controller? _vu1Controller;

        public DialConfigurationPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Sets the DataContext for this panel.
        /// </summary>
        public void SetViewModel(DialConfigurationViewModel viewModel)
        {
            DataContext = viewModel;
        }

        /// <summary>
        /// Sets the VU1 controller for image uploads.
        /// </summary>
        public void SetVU1Controller(VUWare.Lib.VU1Controller? controller)
        {
            _vu1Controller = controller;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void UploadImageButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var viewModel = DataContext as DialConfigurationViewModel;
            if (viewModel == null)
            {
                System.Windows.MessageBox.Show(
                    "Dial configuration not available.",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return;
            }

            // Open the image upload dialog
            var dialog = new ImageUploadDialog(
                viewModel.DialUid,
                viewModel.DisplayName,
                _vu1Controller);

            dialog.Owner = System.Windows.Window.GetWindow(this);
            dialog.ShowDialog();
        }
    }
}
