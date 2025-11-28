using System.Windows.Controls;
using VUWare.App.ViewModels;

namespace VUWare.App
{
    /// <summary>
    /// Interaction logic for DialConfigurationPanel.xaml
    /// </summary>
    public partial class DialConfigurationPanel : UserControl
    {
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
    }
}
