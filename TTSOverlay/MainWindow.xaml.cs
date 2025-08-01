using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
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

        private const int HOTKEY_ID = 9000;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_I = 0x49;

        private HwndSource _source;
        private InputWindow inputWindow;


        List<BitmapImage> talkingSprites = new List<BitmapImage>();
        List<BitmapImage> idleSprites = new List<BitmapImage>();

        DispatcherTimer talkingTimer;
        DispatcherTimer idleTimer;
        DispatcherTimer idlePauseTimer;

        int idleLoopPauseMs = 1000; // How long to linger on first frame (in ms)
        bool isIdlePaused = false;

        int idleFrameIndex = 0;
        int talkingFrameIndex = 0;

        SpeechSynthesizer synth;
        bool isWaitingToReturnToIdle = false;

        bool isSpeaking = false;

        private Queue<string> messageQueue = new Queue<string>();



        public MainWindow()
        {
            InitializeComponent();
            StartHttpListener();




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

            // Setup idle animation
            idleTimer = new DispatcherTimer();
            idleTimer.Interval = TimeSpan.FromMilliseconds(100);
            idleTimer.Tick += (s, e) =>
            {
                if (isIdlePaused)
                    return;

                // Show current frame
                CharacterImage.Source = idleSprites[idleFrameIndex];

                // Advance frame
                idleFrameIndex++;

                // When loop completes, pause on frame 0
                if (idleFrameIndex >= idleSprites.Count)
                {
                    idleFrameIndex = 0;
                    isIdlePaused = true;


                    idlePauseTimer = new DispatcherTimer();
                    idlePauseTimer.Interval = TimeSpan.FromMilliseconds(idleLoopPauseMs);
                    idlePauseTimer.Tick += (s2, e2) =>
                    {
                        isIdlePaused = false;
                        idlePauseTimer.Stop();

                        CharacterImage.Source = idleSprites[0];
                    };
                    idlePauseTimer.Start();
                }
            };
            idleTimer.Start();


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
            };

            Closing += (s, e) =>
            {
                _source.RemoveHook(HwndHook);
                var helper = new WindowInteropHelper(this);
                UnregisterHotKey(helper.Handle, HOTKEY_ID);
            };


        }

        private BitmapImage LoadImage(string path)
        {
            return new BitmapImage(new Uri(Path.GetFullPath(path)));
        }


        /*
        private void HiddenInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {

                if (isSpeaking)
                    return;

                string text = HiddenInput.Text.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    HiddenInput.Clear();
                    if (isSpeaking)
                    {
                        messageQueue.Enqueue(text);
                    }
                    else
                    {
                        isSpeaking = true;
                        SpeakWithAnimation(text);
                    }
                }

            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            HiddenInput.Focus();
        }
        */

        private void SpeakWithAnimation(string text)
        {
            SpeechText.Text = text;
            SpeechText.Visibility = Visibility.Visible;

            talkingFrameIndex = 0;
            idleTimer.Stop();

            talkingTimer = new DispatcherTimer();
            talkingTimer.Interval = TimeSpan.FromMilliseconds(100);
            talkingTimer.Tick += (s, e) =>
            {
                CharacterImage.Source = talkingSprites[talkingFrameIndex];
                talkingFrameIndex++;

                if (talkingFrameIndex >= talkingSprites.Count)
                {
                    talkingFrameIndex = 0;

                    if (isWaitingToReturnToIdle)
                    {
                        talkingTimer.Stop();
                        SpeechText.Visibility = Visibility.Collapsed;

                        idleFrameIndex = 0;
                        CharacterImage.Source = idleSprites[0];
                        idleTimer.Start();
                        isWaitingToReturnToIdle = false;

                        isSpeaking = false;
                        TrySpeakNextMessage(); // Attempt to speak the next message
                    }
                }
            };

            talkingTimer.Start();
            synth.SpeakAsync(text);
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




        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID)
                {
                    OnHotKeyPressed();
                    handled = true;
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
