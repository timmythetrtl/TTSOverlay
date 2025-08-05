using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
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



        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);



        //Focus Hotkey
        private const int HOTKEY_ID = 9000;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_I = 0x49;

        //Phrase Hotkeys
        private const int HOTKEY_F1 = 9001;
        private const int HOTKEY_F2 = 9002;
        private const int HOTKEY_F3 = 9003;
        private const int HOTKEY_F4 = 9004;
        private const int HOTKEY_F5 = 9005;

        private const uint VK_F1 = 0x70;
        private const uint VK_F2 = 0x71;
        private const uint VK_F3 = 0x72;
        private const uint VK_F4 = 0x73;
        private const uint VK_F5 = 0x74;

        //Image restart hotkey
        private const int HOTKEY_F6 = 9006;

        private const uint VK_F6 = 0x75;


        //rendering variables


        private HwndSource _source;
        private InputWindow inputWindow;


        List<BitmapImage> talkingSprites = new List<BitmapImage>();
        List<BitmapImage> idleSprites = new List<BitmapImage>();

        int idleFrameIndex = 0;
        int talkingFrameIndex = 0;

        

        SpeechSynthesizer synth;
        bool isWaitingToReturnToIdle = false;

        bool isSpeaking = false;

       

        private TimeSpan lastFrameTime = TimeSpan.Zero;

        private Queue<string> messageQueue = new Queue<string>();

        private AppViewModel _viewModel;
        private double frameIntervalMs;

        public MainWindow()
        {

            InitializeComponent();
            StartHttpListener();

            _viewModel = new AppViewModel();
            DataContext = _viewModel;

            frameIntervalMs = 1000.0 / _viewModel.SpriteFPS;

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            this.Left = 0;
            this.Top = 0;
            this.Width = SystemParameters.PrimaryScreenWidth;
            this.Height = SystemParameters.PrimaryScreenHeight;


            var settingsWindow = new SettingsWindow(_viewModel);
            settingsWindow.Show();

            //AdjustWindowSizeToFitContent();


            // Load idle sprites
            string idlePath = "Assets/Idle";
            var idleFiles = Directory.GetFiles(idlePath, "*.png");
            foreach (var file in idleFiles)
            {
                idleSprites.Add(LoadImage(file));
            }

            // Load talking sprites
            string talkingPath = "Assets/Talking";
            var talkingFiles = Directory.GetFiles(talkingPath, "*.png");
            foreach (var file in talkingFiles)
            {
                talkingSprites.Add(LoadImage(file));
            }

            CharacterImage.Source = idleSprites[0];

            


            // Setup TTS
            synth = new SpeechSynthesizer();
            synth.SpeakCompleted += Synth_SpeakCompleted;

            // Auto-focus hidden input
            //Loaded += (s, e) => HiddenInput.Focus();

            inputWindow = new InputWindow(this);
            inputWindow.Show();


            Loaded += (s, e) =>
            {
                var helper = new WindowInteropHelper(this);
                _source = HwndSource.FromHwnd(helper.Handle);
                _source.AddHook(HwndHook);

                // Register Ctrl + Shift + I
                RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_I);

                // The others
                RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_I);
                RegisterHotKey(helper.Handle, HOTKEY_F1, 0, VK_F1);
                RegisterHotKey(helper.Handle, HOTKEY_F2, 0, VK_F2);
                RegisterHotKey(helper.Handle, HOTKEY_F3, 0, VK_F3);
                RegisterHotKey(helper.Handle, HOTKEY_F4, 0, VK_F4);
                RegisterHotKey(helper.Handle, HOTKEY_F5, 0, VK_F5);
                RegisterHotKey(helper.Handle, HOTKEY_F6, 0, VK_F6);

                CompositionTarget.Rendering += OnRendering;

            };

            Closing += (s, e) =>
            {
                _source.RemoveHook(HwndHook);
                var helper = new WindowInteropHelper(this);

                // Unregister Ctrl + Shift + I
                UnregisterHotKey(helper.Handle, HOTKEY_ID);

                //The others
                UnregisterHotKey(helper.Handle, HOTKEY_ID);
                UnregisterHotKey(helper.Handle, HOTKEY_F1);
                UnregisterHotKey(helper.Handle, HOTKEY_F2);
                UnregisterHotKey(helper.Handle, HOTKEY_F3);
                UnregisterHotKey(helper.Handle, HOTKEY_F4);
                UnregisterHotKey(helper.Handle, HOTKEY_F5);
                UnregisterHotKey(helper.Handle, HOTKEY_F6);

                CompositionTarget.Rendering -= OnRendering;
                synth.Dispose();

            };


        }

        private BitmapImage LoadImage(string path)
        {
            return new BitmapImage(new Uri(Path.GetFullPath(path)));
        }

        public void LoadIdleSprites(string[] filePaths)
        {
            idleSprites.Clear();
            foreach (var file in filePaths)
                idleSprites.Add(LoadImage(file));

            idleFrameIndex = 0;
            if (idleSprites.Count > 0)
                CharacterImage.Source = idleSprites[0];

            _viewModel.IdleSpriteCount = idleSprites.Count;

        }

        public void LoadTalkingSprites(string[] filePaths)
        {
            talkingSprites.Clear();
            foreach (var file in filePaths)
                talkingSprites.Add(LoadImage(file));

        }

        private void OnRendering(object? sender, EventArgs e)
        {
            var renderingArgs = (RenderingEventArgs)e;
            var currentTime = renderingArgs.RenderingTime;

            if (lastFrameTime == TimeSpan.Zero)
            {
                lastFrameTime = currentTime;
                return;
            }

            while ((currentTime - lastFrameTime).TotalMilliseconds >= frameIntervalMs)
            {
                lastFrameTime += TimeSpan.FromMilliseconds(frameIntervalMs);

                if (isSpeaking)
                    AdvanceTalkingFrame();
                else
                    AdvanceIdleFrame();
            }
        }



        private void AdvanceIdleFrame()
        {
            if (idleSprites.Count == 0) return;

            idleFrameIndex = (idleFrameIndex + 1) % idleSprites.Count;
            CharacterImage.Source = idleSprites[idleFrameIndex];
        }

        private void AdvanceTalkingFrame()
        {
            if (talkingSprites.Count == 0) return;

            talkingFrameIndex = (talkingFrameIndex + 1) % talkingSprites.Count;
            CharacterImage.Source = talkingSprites[talkingFrameIndex];

            // When animation loops once, check if we need to stop
            if (talkingFrameIndex == talkingSprites.Count - 1 && isWaitingToReturnToIdle)
            {
                isWaitingToReturnToIdle = false;
                StopSpeaking();
                TrySpeakNextMessage();
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

            talkingFrameIndex = 0;
            isSpeaking = true;
            isWaitingToReturnToIdle = false;

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

        private void StopSpeaking()
        {
            isSpeaking = false;
            idleFrameIndex = 0;
            CharacterImage.Source = idleSprites[idleFrameIndex];
            SpeechText.Visibility = Visibility.Collapsed;
        }


        private void Synth_SpeakCompleted(object sender, SpeakCompletedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                isWaitingToReturnToIdle = true;
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
            if (isSpeaking || messageQueue.Count == 0)
                return;

            string nextMessage = messageQueue.Dequeue();
            isSpeaking = true;
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
                //AdjustWindowSizeToFitContent();
                //SpeakWithAnimation("Hello World!");
            }
            if (e.PropertyName == nameof(_viewModel.SpriteFPS) 
                || e.PropertyName == nameof(_viewModel.PlaybackSpeedMode))
            {
                // Update the frame interval whenever the FPS changes
                frameIntervalMs = (1000.0 / _viewModel.SpriteFPS) / _viewModel.PlaybackSpeedMultiplier;
            }


        }


        public void ResetToDownbeat()
        {
            int frameIndex = _viewModel.DownbeatFrameIndex;

            if (isSpeaking)
            {
                if (talkingSprites.Count > 0)
                {
                    talkingFrameIndex = frameIndex % talkingSprites.Count;
                    CharacterImage.Source = talkingSprites[talkingFrameIndex];
                }
            }
            else
            {
                if (idleSprites.Count > 0)
                {
                    idleFrameIndex = frameIndex % idleSprites.Count;
                    CharacterImage.Source = idleSprites[idleFrameIndex];
                }
            }
        }


        public int GetCurrentFrameIndex()
        {
            return isSpeaking ? talkingFrameIndex : idleFrameIndex;
        }




        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                handled = true;

                switch (id)
                {
                    //The first one is the Ctrl + Shift + I
                    case HOTKEY_ID:
                        OnHotKeyPressed();
                        break;
                    case HOTKEY_F1:
                        TriggerRandomLineFromFile("Assets/Hotkeys/f1.txt");
                        break;
                    case HOTKEY_F2:
                        TriggerRandomLineFromFile("Assets/Hotkeys/f2.txt");
                        break;
                    case HOTKEY_F3:
                        TriggerRandomLineFromFile("Assets/Hotkeys/f3.txt");
                        break;
                    case HOTKEY_F4:
                        TriggerRandomLineFromFile("Assets/Hotkeys/f4.txt");
                        break;
                    case HOTKEY_F5:
                        TriggerRandomLineFromFile("Assets/Hotkeys/f5.txt");
                        break;
                    case HOTKEY_F6:
                        ResetToDownbeat();
                        break;

                        break;
                    default:
                        handled = false;
                        break;
                }
            }


            return IntPtr.Zero;
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
