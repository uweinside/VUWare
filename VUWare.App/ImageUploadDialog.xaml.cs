using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace VUWare.App
{
    /// <summary>
    /// Interaction logic for ImageUploadDialog.xaml
    /// </summary>
    public partial class ImageUploadDialog : Window
    {
        private readonly string _dialUid;
        private readonly string _dialDisplayName;
        private readonly VUWare.Lib.VU1Controller? _vu1Controller;
        private string? _selectedFilePath;
        private byte[]? _processedImageData;

        public ImageUploadDialog(string dialUid, string dialDisplayName, VUWare.Lib.VU1Controller? vu1Controller)
        {
            InitializeComponent();
            
            _dialUid = dialUid;
            _dialDisplayName = dialDisplayName;
            _vu1Controller = vu1Controller;
            
            DialogTitle.Text = $"Upload Image for {dialDisplayName}";
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Dial Face Image",
                Filter = "Image Files|*.png;*.bmp;*.jpg;*.jpeg|PNG Files|*.png|BMP Files|*.bmp|JPEG Files|*.jpg;*.jpeg|All Files|*.*",
                CheckFileExists = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadAndPreviewImage(openFileDialog.FileName);
            }
        }

        private void LoadAndPreviewImage(string filePath)
        {
            try
            {
                StatusText.Text = "Loading and processing image...";
                StatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow);
                
                _selectedFilePath = filePath;
                FilePathTextBox.Text = Path.GetFileName(filePath);

                // Load and process the image for the dial (200x144, 1-bit)
                _processedImageData = VUWare.Lib.ImageProcessor.LoadImageFile(filePath);

                // Create a preview bitmap for display
                var previewBitmap = CreatePreviewBitmap(_processedImageData);
                PreviewImage.Source = previewBitmap;

                // Enable upload button
                UploadButton.IsEnabled = true;
                
                var fileInfo = new FileInfo(filePath);
                StatusText.Text = $"Image loaded and processed successfully ({fileInfo.Length:N0} bytes source, {_processedImageData.Length} bytes processed)";
                StatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGreen);
            }
            catch (Exception ex)
            {
                _processedImageData = null;
                UploadButton.IsEnabled = false;
                PreviewImage.Source = null;
                FilePathTextBox.Text = "No file selected";
                
                StatusText.Text = $"Error loading image: {ex.Message}";
                StatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                
                MessageBox.Show(
                    $"Failed to load image:\n\n{ex.Message}",
                    "Image Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private BitmapSource CreatePreviewBitmap(byte[] packedData)
        {
            // Convert packed 1-bit data back to a displayable bitmap
            const int width = 200;
            const int height = 144;
            
            var pixels = new byte[width * height];
            
            // Unpack the 1-bit vertical data
            int byteIndex = 0;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y += 8)
                {
                    byte packedByte = packedData[byteIndex++];
                    for (int bit = 0; bit < 8 && (y + bit) < height; bit++)
                    {
                        bool isWhite = (packedByte & (1 << (7 - bit))) != 0;
                        pixels[(y + bit) * width + x] = (byte)(isWhite ? 255 : 0);
                    }
                }
            }

            // Create bitmap from grayscale pixels
            var bitmap = BitmapSource.Create(
                width, height,
                96, 96,
                System.Windows.Media.PixelFormats.Gray8,
                null,
                pixels,
                width);

            return bitmap;
        }

        private async void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_vu1Controller == null || !_vu1Controller.IsConnected)
            {
                MessageBox.Show(
                    "VU1 Hub is not connected. Please ensure the hub is connected before uploading images.",
                    "VU1 Not Connected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_processedImageData == null)
            {
                MessageBox.Show(
                    "No image has been loaded. Please select an image first.",
                    "No Image",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Disable buttons during upload
                UploadButton.IsEnabled = false;
                BrowseButton.IsEnabled = false;
                CancelButton.IsEnabled = false;
                
                StatusText.Text = $"Uploading image to {_dialDisplayName}...";
                StatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow);

                // Upload the image
                bool success = await _vu1Controller.SetDisplayImageAsync(_dialUid, _processedImageData);

                if (success)
                {
                    StatusText.Text = $"Image successfully uploaded to {_dialDisplayName}.";
                    StatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGreen);
                    
                    // Hide Cancel button and change Upload to Done
                    CancelButton.Visibility = Visibility.Collapsed;
                    UploadButton.Content = "Done";
                    UploadButton.IsEnabled = true;
                    
                    // Rewire Done button to close with success
                    UploadButton.Click -= UploadButton_Click;
                    UploadButton.Click += (s, args) =>
                    {
                        DialogResult = true;
                        Close();
                    };
                }
                else
                {
                    StatusText.Text = $"Failed to upload image to {_dialDisplayName}";
                    StatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                    
                    MessageBox.Show(
                        $"Failed to upload image to {_dialDisplayName}. Please try again.",
                        "Upload Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    
                    // Re-enable buttons
                    UploadButton.IsEnabled = true;
                    BrowseButton.IsEnabled = true;
                    CancelButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                StatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                
                MessageBox.Show(
                    $"An error occurred while uploading:\n\n{ex.Message}",
                    "Upload Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                // Re-enable buttons
                UploadButton.IsEnabled = true;
                BrowseButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
            }
        }
    }
}
