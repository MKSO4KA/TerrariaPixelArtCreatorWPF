
using Microsoft.Win32;
using System.Collections.Concurrent;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Xml.Linq;
using WPFFolderBrowser;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using System.Windows.Media;
using WpfApp1.MainScript;
namespace WpfApp1
{
    public class WidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double windowWidth)
            {
                return windowWidth * 0.6; // 75% от ширины окна
            }
            return 400; // Значение по умолчанию
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class HeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double height)
                return height * 0.30; // Пример: 50% высоты окна
            return 300;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public partial class MainWindow : Window
    {
        private string _errorSub = String.Empty;
        private string ErrorSub
        {
            get 
            {
                string errtex = _errorSub;
                _errorSub = String.Empty;
                return errtex;
            }
            set { _errorSub += value; }
        }
        private byte _maxDegreeOfParallelism = 5;
        public byte MaxDegreeOfParallelism
        {
            get
            {
                return _maxDegreeOfParallelism;
            }
            set
            {
                _maxDegreeOfParallelism = value;
            }
        }
        private bool _isProcessing = false;
        private int _selectedParallelTasks;
        private string _outputPath = String.Empty;
        public string OutputPath
        {
            get
            {
                return _outputPath;
            }
            set
            {
                _outputPath = value;
            }
        }
        private string _imagePath = String.Empty;
        public string ImagePath
        {
            get
            {
                return _imagePath;
            }
            set
            {
                _imagePath = value;
            }
        }
        private string _videoPath = String.Empty;
        public string VideoPath
        {
            get
            {
                return _videoPath;
            }
            set
            {
                _videoPath = value;
            }
        }
        private byte _targetFrameRate = 1;
        public byte TargetFrameRate
        {
            get
            {
                return _targetFrameRate;
            }
            set
            {
                _targetFrameRate = value;
            }
        }
        private static string _userTilesPath = String.Empty;
        public static bool UserTiles = false;
        public static string UserTilesPath
        {
            get
            {
                return _userTilesPath;
            }
            set
            {
                if (File.Exists(value))
                {
                    UserTiles = true;
                }
                _userTilesPath = value;
            }
        }
        public MainWindow()
        {
            InitializeComponent();
            for (int i = 1; i <= 50; i++)
            {
                parallelTasksCombo.Items.Add(new ComboBoxItem { Content = i.ToString() });
            }
            InitializeControls();
        }
        private void SetValueFromComboBox(ComboBox comboBox, out byte value, byte defaultValue)
        {
            if (!byte.TryParse(comboBox.Text, out value))
            {
                ErrorSub = $"\n {comboBox.Name} - задан некорректно";
                value = defaultValue;
                comboBox.Text = defaultValue.ToString();
            }
        }
        private void SetValueFromTextBox(TextBox textBox, out byte value, byte defaultValue)
        {
            if (!byte.TryParse(textBox.Text, out value))
            {
                ErrorSub = $"\n {textBox.Name} - задан некорректно";
                value = defaultValue;
                textBox.Text = defaultValue.ToString();
            }
        }
        private void InitializeControls()
        {
            parallelTasksCombo.SelectedIndex = 4; // Выбор значения 5 по умолчанию
            fpsTextBox.Text = "30";
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            outputPathTextBox.Text = OutputPath;
            settingsPopup.IsOpen = !settingsPopup.IsOpen;
        }

        private void BrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;
            string _cacheTiles = userTilesTextBox.Text;
            var folderDialog = new WPFFolderBrowserDialog();
            folderDialog.Title = "Select Output Folder";
            folderDialog.InitialDirectory = Environment.SpecialFolder.Desktop.ToString();
            settingsPopup.IsOpen = !settingsPopup.IsOpen;
            if (folderDialog.ShowDialog() == true)
            {
                
                outputPathTextBox.Text = folderDialog.FileName;
                settingsPopup.IsOpen = !settingsPopup.IsOpen;
                userTilesTextBox.Text = _cacheTiles;
            }

        }
        private void BrowseUserTiles_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;
            string _cacheOut = outputPathTextBox.Text;
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Tile file (*.txt)|*.txt|XML files (*.xml)|*.xml|All files (*.*)|*.*",
                Title = "Select Text or XML File"
            };
            settingsPopup.IsOpen = !settingsPopup.IsOpen;
            if (openFileDialog.ShowDialog() == true)
            {
                userTilesTextBox.Text = openFileDialog.FileName;
                
                settingsPopup.IsOpen = !settingsPopup.IsOpen;
                outputPathTextBox.Text = _cacheOut;
            }

        }
        private void settingsPopup_Closed(object sender, EventArgs e)
        {
            outputPathTextBox.Text = OutputPath;
            userTilesTextBox.Text = UserTilesPath;
        }
        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            // Сохранение настроек (заглушка)
            settingsPopup.IsOpen = false;
            OutputPath = outputPathTextBox.Text;
            UserTilesPath = userTilesTextBox.Text; // TODO РЕАЛИЗОВАТЬ!!!
            
            //MessageBox.Show("Settings saved!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        
        private void BrowseImage_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg)|*.png;*.jpg|All files (*.*)|*.*",
                Title = "Select Image File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                imagePathTextBox.Text = openFileDialog.FileName;
                ImagePath = openFileDialog.FileName;
            }
        }

        private void BrowseVideo_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Video files (*.mp4)|*.mp4|All files (*.*)|*.*",
                Title = "Select Video File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                videoPathTextBox.Text = openFileDialog.FileName;
                VideoPath = openFileDialog.FileName;
            }
        }

        private async void ProcessImage_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;

            try
            {
                ToggleProcessingState(true);
                UpdateProgressBar(imageProgress, true);

                if (string.IsNullOrWhiteSpace(imagePathTextBox.Text))
                {
                    MessageService.ShowError("Please select an image file!" + $"\n{ErrorSub}", "Validation Error");
                    return;
                }

                if (string.IsNullOrWhiteSpace(outputPathTextBox.Text))
                {
                    MessageService.ShowError("Please select output folder!" + $"\n{ErrorSub}", "Validation Error");
                    return;
                }
                // Имитация обработки изображений
                await ProcessImagesAsync();
                UpdateProgressBar(imageProgress, false);
                MessageService.ShowSuccess("Image processing completed!", "Success");
                
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
            finally
            {

                ToggleProcessingState(false);
                UpdateProgressBar(imageProgress, false);
            }
        }

        private async void ProcessVideo_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;

            try
            {
                ToggleProcessingState(true);
                UpdateProgressBar(videoProgress, true);
                
                SetValueFromComboBox(parallelTasksCombo, out _maxDegreeOfParallelism, 1);
                MaxDegreeOfParallelism = _maxDegreeOfParallelism;

                SetValueFromTextBox(fpsTextBox, out _targetFrameRate, 30);
                TargetFrameRate = _targetFrameRate;
                if (string.IsNullOrWhiteSpace(videoPathTextBox.Text))
                {
                    MessageService.ShowError("Please select a video file!" + $"\n{ErrorSub}", "Validation Error");
                    return;
                }

                if (string.IsNullOrWhiteSpace(outputPathTextBox.Text))
                {
                    MessageService.ShowError("Please select output folder!" + $"\n{ErrorSub}", "Validation Error");
                    return;
                }
                _errorSub = String.Empty;
                // Имитация обработки видео
                await ProcessVideoAsync();
                ToggleProcessingState(false);
                MessageService.ShowSuccess("Video processing completed!", "Success");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
            finally
            {
                ToggleProcessingState(false);
                UpdateProgressBar(videoProgress, false);
            }
        }

        private async Task ProcessImagesAsync()
        {
            // Заглушка для реальной логики
            var processor = new PhotoProcessor(
                ImagePath,
                OutputPath
                
            );
            await processor.ProcessFramesAsync();
        }

        private async Task ProcessVideoAsync()
        {
            // Заглушка для реальной логики
            var processor = new VideoProcessor(
                VideoPath,
                OutputPath,
                TargetFrameRate,
                MaxDegreeOfParallelism
            );
            await processor.ProcessFramesAsync();
        }

        private void ToggleProcessingState(bool isProcessing)
        {
            _isProcessing = isProcessing;
            processImageButton.IsEnabled = !isProcessing;
            settingsButton.IsEnabled = !isProcessing;
            processVideoButton.IsEnabled = !isProcessing;
            browseImageButton.IsEnabled = !isProcessing;
            browseVideoButton.IsEnabled = !isProcessing;
            parallelTasksCombo.IsEnabled = !isProcessing;
            fpsTextBox.IsEnabled = !isProcessing;
            settingsButton.IsEnabled = !isProcessing;
        }

        private void UpdateProgressBar(System.Windows.Controls.ProgressBar progressBar, bool isActive)
        {
            progressBar.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
            progressBar.IsIndeterminate = isActive;
        }
        private void ParallelTasksCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (parallelTasksCombo.SelectedItem is ComboBoxItem selectedItem)
            {
                int tasks = int.Parse(selectedItem.Content.ToString() ?? "1");
                // Сохраните выбранное значение (например, в свойстве класса)
                _selectedParallelTasks = tasks;
            }
        }

        
    }
    /*
    #region Заглушки обработчиков
    public class PhotoProcessor
    {
        public PhotoProcessor(string inputPath, string outputPath, int maxParallelism) { }
        public async Task ProcessFramesAsync() => await Task.Delay(2000);
    }

    public class VideoProcessor
    {
        public VideoProcessor(string inputPath, string outputPath, uint frameRate, int maxParallelism) { }
        public async Task ProcessFramesAsync() => await Task.Delay(3000);
    }
    #endregion
    */
}
