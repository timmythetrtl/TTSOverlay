using System.Collections.Generic;
using System.Globalization;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Controls;

namespace TTSOverlay
{
    public class TtsManager
    {
        private readonly SpeechSynthesizer _synth;
        private readonly Queue<string> _messageQueue = new Queue<string>();
        private readonly SpriteManager _spriteManager;
        private readonly SpriteGroup _spriteGroup;
        private readonly TextBlock _speechText;

        public TtsManager(SpriteGroup spriteGroup, TextBlock speechText)

        {
            _spriteGroup = spriteGroup;
            _speechText = speechText;

            _synth = new SpeechSynthesizer();
            _synth.SpeakCompleted += Synth_SpeakCompleted;
        }

        public void EnqueueText(string text)
        {
            _messageQueue.Enqueue(text);
            TrySpeakNextMessage();
        }

        private void TrySpeakNextMessage()
        {
            if (_spriteGroup.IsSpeaking || _messageQueue.Count == 0)
                return;

            string nextMessage = _messageQueue.Dequeue();
            SpeakWithAnimation(nextMessage);
        }

        private void SpeakWithAnimation(string text)
        {
            _speechText.Text = text;
            _speechText.Visibility = Visibility.Visible;
            _spriteGroup.IsSpeaking = true;

            // Set voice
            if (ContainsJapanese(text))
            {
                try
                {
                    _synth.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Adult, 0, new CultureInfo("ja-JP"));
                }
                catch
                {
                    MessageBox.Show("Japanese voice not installed. Please install a Japanese TTS voice.");
                }
            }
            else
            {
                _synth.SelectVoice("Microsoft David Desktop");
            }

            _synth.SpeakAsync(text);
        }

        private void Synth_SpeakCompleted(object sender, SpeakCompletedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _speechText.Visibility = Visibility.Collapsed;
                _spriteGroup.IsSpeaking = false;
                TrySpeakNextMessage(); // Call internal method instead of external callback
            });
        }


        private bool ContainsJapanese(string text)
        {
            foreach (char c in text)
            {
                if ((c >= 0x3040 && c <= 0x309F) || // Hiragana
                    (c >= 0x30A0 && c <= 0x30FF) || // Katakana
                    (c >= 0x4E00 && c <= 0x9FBF))   // Kanji
                {
                    return true;
                }
            }
            return false;
        }

        public void Dispose()
        {
            _synth?.Dispose();
        }
    }
}
