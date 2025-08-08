using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms; // Requires a reference to System.Windows.Forms
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static System.Net.WebRequestMethods;



namespace TTSOverlay
{
    public partial class MainWindow : Window
    {
        //Other Classes
        private InputWindow inputWindow;
        private AppViewModel _viewModel;



        //Function Managers
        private TtsManager ttsManager;
        private SpriteManager spriteManager;
        private HotkeyManager hotkeyManager;

        private Stopwatch stopwatch = new Stopwatch();
        private TimeSpan lastUpdateTime = TimeSpan.Zero;

        private NotepadBpmWatcher? _notepadWatcher;

        //Dragging stuff
        private Point _mouseOffset;
        private bool _isDragging = false;

        public MainWindow()
        {

            InitializeComponent();
            StartHttpListener();

            _viewModel = new AppViewModel();
            DataContext = _viewModel;

            spriteManager = new SpriteManager();

            var allScreensBounds = Screen.AllScreens
                .Select(s => s.Bounds)
                .Aggregate((b1, b2) => System.Drawing.Rectangle.Union(b1, b2));

            Left = allScreensBounds.Left;
            Top = allScreensBounds.Top;
            Width = allScreensBounds.Width;
            Height = allScreensBounds.Height;


            //spriteManager.LoadIdleSprites(Directory.GetFiles("Assets/Idle", "*.png"));
            //spriteManager.LoadTalkingSprites(Directory.GetFiles("Assets/Talking", "*.png"));

            //Load Idle and talking 
            _viewModel.AddFirstGroup();
            _viewModel.IdleSpriteCount = (Directory.GetFiles("Assets/Idle", "*.png").Length);


            //CharacterImage.Source = spriteManager.CurrentSprite;

            CharacterImage.Source = _viewModel.SpriteGroups[0].CurrentFrame;




            //Makes the other windows appear
            inputWindow = new InputWindow(this);
            inputWindow.Show();

            var settingsWindow = new SettingsWindow(_viewModel);
            settingsWindow.Show();



            string notepadFile = @"E:\TUNA\metadatastuff.txt";
            _notepadWatcher = new NotepadBpmWatcher(notepadFile, _viewModel, Dispatcher);


            // Text to speech stuff
            // So it's looking like this is something that you'll have to update EVERYTIME SpriteGroups is called
            ttsManager = new TtsManager(_viewModel.SpriteGroups[0], SpeechText);

            Loaded += (s, e) =>
            {
                hotkeyManager = new HotkeyManager(this);
                hotkeyManager.HotkeyPressed += OnHotkeyPressed;
                hotkeyManager.RegisterHotkeys();




                // Remove CompositionTarget.Rendering or DispatcherTimer
                CompositionTarget.Rendering += OnRendering;
                stopwatch.Start();
            };

            Closing += (s, e) =>
            {
                hotkeyManager.UnregisterHotkeys();

                stopwatch.Stop();
                CompositionTarget.Rendering -= OnRendering;

                ttsManager.Dispose();

                _notepadWatcher?.Dispose();
            };
        }

        private double accumulatedMs = 0;
        private void OnRendering(object? sender, EventArgs e)
        {
            var now = stopwatch.Elapsed;
            double elapsedMs = (now - lastUpdateTime).TotalMilliseconds;
            lastUpdateTime = now;

            accumulatedMs += elapsedMs;

            double interval = (1000.0 / _viewModel.SpriteFPS) / _viewModel.PlaybackSpeedMultiplier;

            while (accumulatedMs >= interval)
            {
                CharacterImage.Source = _viewModel.SpriteGroups[0].AdvanceFrame();
                accumulatedMs -= interval;
            }
        }
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _mouseOffset = e.GetPosition(this);
            this.CaptureMouse();
        }

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging && _viewModel.IsEditMode)
            {
                var mousePos = e.GetPosition(this);
                var deltaX = mousePos.X - _mouseOffset.X;
                var deltaY = mousePos.Y - _mouseOffset.Y;

                _viewModel.CharacterX += deltaX;
                _viewModel.CharacterY += deltaY;

                _mouseOffset = mousePos;
            }
        }


        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            this.ReleaseMouseCapture();
        }



        public void ReceiveExternalInput(string text)
        {
            ttsManager.EnqueueText(text);
        }
        public int GetCurrentFrameIndex()
        {
            return _viewModel.SpriteGroups[0].GetCurrentFrameIndex();
        }

        public void LoadIdleSprites(string[] files)
        {

            _viewModel.SpriteGroups[0].LoadIdleSprites(files);
            CharacterImage.Source = _viewModel.SpriteGroups[0].CurrentFrame; _viewModel.IdleSpriteCount = files.Length;
            if (files.Length > 0)
                AdjustCharacterHeightToAspect(files[0]);
        }

        public void LoadTalkingSprites(string[] files)
        {
            _viewModel.SpriteGroups[0].LoadTalkingSprites(files);
            if (files.Length > 0)
                AdjustCharacterHeightToAspect(files[0]);
        }

        public void ResetToDownbeat()
        {
            _viewModel.SpriteGroups[0].ResetToDownbeat(_viewModel.DownbeatFrameIndex);
        }

        private void AdjustCharacterHeightToAspect(string imagePath)
        {
            var firstImage = new BitmapImage(new Uri(Path.GetFullPath(imagePath)));
            double aspectRatio = firstImage.PixelHeight > 0
                ? (double)firstImage.PixelWidth / firstImage.PixelHeight
                : 1.0;

            _viewModel.aspectRatio = aspectRatio;
            _viewModel.CharacterHeight = _viewModel.CharacterWidth / aspectRatio;
        }


        private void OnHotkeyPressed(int id)
        {
            switch (id)
            {
                case 9000: FocusWindowHotKey(); break;
                case 9001: TriggerRandomLineFromFile("Assets/Hotkeys/f1.txt"); break;
                case 9002: TriggerRandomLineFromFile("Assets/Hotkeys/f2.txt"); break;
                case 9003: TriggerRandomLineFromFile("Assets/Hotkeys/f3.txt"); break;
                case 9004: TriggerRandomLineFromFile("Assets/Hotkeys/f4.txt"); break;
                case 9005: TriggerRandomLineFromFile("Assets/Hotkeys/f5.txt"); break;
                case 9006: _viewModel.SpriteGroups[0].ResetToDownbeat(_viewModel.DownbeatFrameIndex); break;
            }
        }
        public void FocusWindowHotKey()
        {
            Dispatcher.Invoke(() =>
            {
                if (inputWindow == null || !inputWindow.IsVisible)
                {
                    inputWindow = new InputWindow(this);
                    inputWindow.Show();
                }

                if (inputWindow.WindowState == WindowState.Minimized)
                    inputWindow.WindowState = WindowState.Normal;

                inputWindow.Activate();
                inputWindow.Topmost = true;  // force on top
                inputWindow.Topmost = false; // allow normal topmost behavior

                inputWindow.Focus();

                // Focus the TextBox inside InputWindow
                inputWindow.FocusTextBox();
            });
        }
        private void TriggerRandomLineFromFile(string filePath)
        {
            try
            {
                if (!System.IO.File.Exists(filePath)) return;

                var lines = System.IO.File.ReadAllLines(filePath);
                var validLines = Array.FindAll(lines, line => !string.IsNullOrWhiteSpace(line));

                if (validLines.Length > 0)
                {
                    var random = new Random();
                    string line = validLines[random.Next(validLines.Length)];
                    ReceiveExternalInput(line); // This queues and speaks it
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error reading file {filePath}: {ex.Message}");
            }
        }
        private void StartHttpListener()
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://+:8000/");

            try
            {
                listener.Start();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to start HTTP listener: {ex.Message}");
                return;
            }

            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        var context = listener.GetContext();
                        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                        string message = reader.ReadToEnd();

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            ReceiveExternalInput(message);
                        });

                        context.Response.StatusCode = 200;
                        context.Response.Close();
                    }
                    catch (Exception ex)
                    {
                        
                    }
                }
            });
        }
    }
}
