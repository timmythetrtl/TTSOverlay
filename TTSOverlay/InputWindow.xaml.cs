using System;
using System.Windows;
using System.Windows.Input;

namespace TTSOverlay
{
    public partial class InputWindow : Window
    {
        private MainWindow main;

        public InputWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            main = mainWindow;
        }

        private void UserInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string text = UserInputBox.Text.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    main.ReceiveExternalInput(text);
                    UserInputBox.Clear();
                }
            }
        }

        public void FocusTextBox()
        {
            UserInputBox.Focus();
        }

        

    }
}
