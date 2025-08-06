using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Media;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace TTSOverlay
{
    public partial class MainWindow : Window
    {
        //rendering variables

        private InputWindow inputWindow;

        SpeechSynthesizer synth;

        private TimeSpan lastFrameTime = TimeSpan.Zero;

        private Queue<string> messageQueue = new Queue<string>();

        private AppViewModel _viewModel;
        private double frameIntervalMs;

        private DispatcherTimer bpmFileTimer;

        private Stopwatch stopwatch = new Stopwatch();
        private TimeSpan lastUpdateTime = TimeSpan.Zero;

        private NotepadBpmWatcher? _notepadWatcher;

        private SpriteManager spriteManager;
        private HotkeyManager hotkeyManager;

        private string phrasesFile = "Assets/phrases.txt";
        private string variableFile = "Assets/current.txt";

        private SpeechSynthesizer synthesizer;

        public MainWindow()
        {

            InitializeComponent();
            StartHttpListener();

            _viewModel = new AppViewModel();
            DataContext = _viewModel;

            spriteManager = new SpriteManager();
            synthesizer = new SpeechSynthesizer();

            frameIntervalMs = 1000.0 / _viewModel.SpriteFPS;

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            this.Left = 0;
            this.Top = 0;
            this.Width = SystemParameters.PrimaryScreenWidth;
            this.Height = SystemParameters.PrimaryScreenHeight;

            var settingsWindow = new SettingsWindow(_viewModel);
            settingsWindow.Show();

            //Load Idle and talking sprites
            spriteManager.LoadIdleSprites(Directory.GetFiles("Assets/Idle", "*.png"));
            spriteManager.LoadTalkingSprites(Directory.GetFiles("Assets/Talking", "*.png"));

            CharacterImage.Source = spriteManager.CurrentSprite;

            // Setup TTS
            synth = new SpeechSynthesizer();
            synth.SpeakCompleted += Synth_SpeakCompleted;

            inputWindow = new InputWindow(this);
            inputWindow.Show();

            string notepadFile = @"E:\TUNA\metadatastuff.txt";
            _notepadWatcher = new NotepadBpmWatcher(notepadFile, _viewModel, Dispatcher);

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

                synth.Dispose();

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
                CharacterImage.Source = spriteManager.AdvanceFrame();
                accumulatedMs -= interval;
            }
        }

        private bool ContainsJapanese(string text)
        {
            foreach (char c in text)
            {
                if ((c >= 0x3040 && c <= 0x309F) || // Hiragana
                    (c >= 0x30A0 && c <= 0x30FF) || // Katakana
                    (c >= 0x4E00 && c <= 0x9FBF))   // Kanji (CJK Unified Ideographs)
                {
                    return true;
                }
            }
            return false;
        }



        private void SpeakWithAnimation(string text)
        {
            SpeechText.Text = text;
            SpeechText.Visibility = Visibility.Visible;

            spriteManager.SetSpeakingState(true);


            // Replace later when you do selectable voices.
            if (ContainsJapanese(text))
            {
                try
                {
                    synth.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Adult, 0, new System.Globalization.CultureInfo("ja-JP"));
                }
                catch
                {
                    MessageBox.Show("Japanese voice not installed. Please install a Japanese TTS voice.");
                }
            }
            else
            {
                synth.SelectVoice("Microsoft David Desktop");

            }

            synth.SpeakAsync(text);
        }

        private void Synth_SpeakCompleted(object sender, SpeakCompletedEventArgs e)
        {
            SpeechText.Visibility = Visibility.Collapsed;
            Dispatcher.Invoke(() =>
            {
                spriteManager.SetSpeakingState(false);
            });
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        public void ReceiveExternalInput(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var chunks = SplitTextIntoChunks(text, 120);
            foreach (var chunk in chunks)
            {
                messageQueue.Enqueue(chunk);
            }
            
            TrySpeakNextMessage();
        }

        private List<string> SplitTextIntoChunks(string text, int maxChunkLength)
        {
            var chunks = new List<string>();
            int start = 0;

            while (start < text.Length)
            {
                int length = Math.Min(maxChunkLength, text.Length - start);

                // Try to avoid splitting in the middle of a word
                if (start + length < text.Length)
                {
                    int lastSpace = text.LastIndexOf(' ', start + length, length);
                    if (lastSpace > start)
                    {
                        length = lastSpace - start;
                    }
                }

                string chunk = text.Substring(start, length).Trim();
                if (!string.IsNullOrEmpty(chunk))
                    chunks.Add(chunk);

                start += length;
            }
            return chunks;
        }


        private void TrySpeakNextMessage()
        {
            if (spriteManager.IsSpeaking || messageQueue.Count == 0)
                return;

            string nextMessage = messageQueue.Dequeue();
            SpeakWithAnimation(nextMessage);
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_viewModel.CharacterX) ||
                e.PropertyName == nameof(_viewModel.CharacterY) ||
                e.PropertyName == nameof(_viewModel.CharacterWidth) ||
                e.PropertyName == nameof(_viewModel.CharacterHeight) ||
                e.PropertyName == nameof(_viewModel.SpeechX) ||
                e.PropertyName == nameof(_viewModel.SpeechY) ||
                e.PropertyName == nameof(_viewModel.SpeechMaxWidth))
            {

            }
            if (e.PropertyName == nameof(_viewModel.SpriteFPS) ||
                e.PropertyName == nameof(_viewModel.PlaybackSpeedMode) ||
                e.PropertyName == nameof(_viewModel.IdleSpriteCount))
            {
                // Update the frame interval whenever the FPS changes
                frameIntervalMs = (1000.0 / _viewModel.SpriteFPS) / _viewModel.PlaybackSpeedMultiplier;
            }


        }
        public int GetCurrentFrameIndex()
        {
            return spriteManager.GetCurrentFrameIndex();
        }

        public void LoadIdleSprites(string[] files)
        {
            spriteManager.LoadIdleSprites(files);
            CharacterImage.Source = spriteManager.CurrentSprite;

            _viewModel.IdleSpriteCount = files.Length;
        }

        public void LoadTalkingSprites(string[] files)
        {
            spriteManager.LoadTalkingSprites(files);
        }

        public void ResetToDownbeat()
        {
            spriteManager.ResetToDownbeat(_viewModel.DownbeatFrameIndex);
        }

        private void OnHotkeyPressed(int id)
        {
            switch (id)
            {
                case 9000: OnHotKeyPressed(); break;
                case 9001: TriggerRandomLineFromFile("Assets/Hotkeys/f1.txt"); break;
                case 9002: TriggerRandomLineFromFile("Assets/Hotkeys/f2.txt"); break;
                case 9003: TriggerRandomLineFromFile("Assets/Hotkeys/f3.txt"); break;
                case 9004: TriggerRandomLineFromFile("Assets/Hotkeys/f4.txt"); break;
                case 9005: TriggerRandomLineFromFile("Assets/Hotkeys/f5.txt"); break;
                case 9006: spriteManager.ResetToDownbeat(_viewModel.DownbeatFrameIndex); break;
            }
        }
        private void OnHotKeyPressed()
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
                if (!File.Exists(filePath)) return;

                var lines = File.ReadAllLines(filePath);
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
                MessageBox.Show($"Error reading file {filePath}: {ex.Message}");
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
                MessageBox.Show($"Failed to start HTTP listener: {ex.Message}");
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

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ReceiveExternalInput(message);
                        });

                        context.Response.StatusCode = 200;
                        context.Response.Close();
                    }
                    catch (Exception ex)
                    {
                        // Log or handle exceptions inside the listener loop
                        // Example: ignore and continue
                        // Or you can log somewhere for debugging
                    }
                }
            });
        }
    }
}
