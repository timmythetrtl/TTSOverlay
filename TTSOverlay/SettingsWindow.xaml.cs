using Microsoft.Win32;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace TTSOverlay
{
    public partial class SettingsWindow : Window
    {
        private MainWindow mainWindow;
        private AppViewModel viewModel;


        public SettingsWindow(AppViewModel viewModel)
        {
            InitializeComponent();
            this.viewModel = viewModel; // <-- add this line
            DataContext = viewModel;
            mainWindow = (MainWindow)Application.Current.MainWindow;

        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void LoadIdleSprites_Click(object sender, RoutedEventArgs e)
        {
            var files = PromptForImages();
            if (files != null && files.Length > 0)
            {
                mainWindow.LoadIdleSprites(files);
            }
        }

        private void LoadTalkingSprites_Click(object sender, RoutedEventArgs e)
        {
            var files = PromptForImages();
            if (files != null && files.Length > 0)
            {
                mainWindow.LoadTalkingSprites(files);
            }
        }

        private string[] PromptForImages()
        {

            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
                Title = "Select Images"
            };

            return dialog.ShowDialog() == true
                ? dialog.FileNames.OrderBy(f => f).ToArray()
                : null;
        }

        private void SetDownbeatButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                viewModel.DownbeatFrameIndex = mainWindow.GetCurrentFrameIndex();
            }
        }

        //This is a pretty good example. A nice... simple button. Althought you might want to ask what the appviewmodel actually does...
        private void ResetToDownbeat_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.ResetToDownbeat();
            }
        }



    }
}
