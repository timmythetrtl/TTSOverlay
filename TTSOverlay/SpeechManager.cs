using System;
using System.Collections.Generic;
using System.Speech.Synthesis;
using System.Windows.Threading;
using TTSOverlay;

public class SpeechManager
{
    private readonly SpeechSynthesizer _synth;
    private readonly SpriteManager _spriteManager;
    private readonly Dispatcher _dispatcher;

    private Queue<string> _messageQueue = new Queue<string>();
    private bool _isSpeaking = false;

    public SpeechManager(SpriteManager spriteManager, Dispatcher dispatcher)
    {
        _spriteManager = spriteManager;
        _dispatcher = dispatcher;

        _synth = new SpeechSynthesizer();
        _synth.SpeakCompleted += Synth_SpeakCompleted;
    }

    public void EnqueueAndSpeak(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var chunks = SplitTextIntoChunks(text, 120);
        foreach (var chunk in chunks)
            _messageQueue.Enqueue(chunk);

        TrySpeakNextMessage();
    }

    private void TrySpeakNextMessage()
    {
        if (_isSpeaking || _messageQueue.Count == 0)
            return;

        var nextMessage = _messageQueue.Dequeue();

        _dispatcher.Invoke(() =>
        {
            _spriteManager.SetSpeakingState(true);
            SelectVoice(nextMessage);
            _synth.SpeakAsync(nextMessage);
        });

        _isSpeaking = true;
    }

    private void Synth_SpeakCompleted(object? sender, SpeakCompletedEventArgs e)
    {
        _dispatcher.Invoke(() =>
        {
            _spriteManager.SetSpeakingState(false);
        });

        _isSpeaking = false;
        TrySpeakNextMessage();
    }

    private void SelectVoice(string text)
    {
        try
        {
            if (ContainsJapanese(text))
            {
                _synth.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Adult, 0, new System.Globalization.CultureInfo("ja-JP"));
            }
            else
            {
                _synth.SelectVoice("Microsoft David Desktop");
            }
        }
        catch
        {
            // Optionally handle missing voice
        }
    }

    private bool ContainsJapanese(string text)
    {
        foreach (char c in text)
        {
            if ((c >= 0x3040 && c <= 0x309F) || // Hiragana
                (c >= 0x30A0 && c <= 0x30FF) || // Katakana
                (c >= 0x4E00 && c <= 0x9FBF))   // Kanji
                return true;
        }
        return false;
    }

    private List<string> SplitTextIntoChunks(string text, int maxChunkLength)
    {
        var chunks = new List<string>();
        int start = 0;

        while (start < text.Length)
        {
            int length = Math.Min(maxChunkLength, text.Length - start);

            if (start + length < text.Length)
            {
                int lastSpace = text.LastIndexOf(' ', start + length, length);
                if (lastSpace > start)
                    length = lastSpace - start;
            }

            string chunk = text.Substring(start, length).Trim();
            if (!string.IsNullOrEmpty(chunk))
                chunks.Add(chunk);

            start += length;
        }

        return chunks;
    }

    public void Dispose()
    {
        _synth.Dispose();
    }
}
