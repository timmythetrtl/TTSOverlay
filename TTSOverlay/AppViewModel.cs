using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace TTSOverlay
{

    public enum PlaybackSpeedMode
    {
        Regular,
        HalfTime,
        QuarterTime
    }


    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return false;
        }
    }

    public class AppViewModel : INotifyPropertyChanged
    {
        private double _characterX, _characterY, _characterWidth = 150, _characterHeight = 250;
        private double _speechX, _speechY, _speechFontSize = 40, _speechMaxWidth = 500;

        public double CharacterX { get => _characterX; set { _characterX = value; OnPropertyChanged(nameof(CharacterX)); } }
        public double CharacterY { get => _characterY; set { _characterY = value; OnPropertyChanged(nameof(CharacterY)); } }
        public double CharacterWidth { get => _characterWidth; set { _characterWidth = value; OnPropertyChanged(nameof(CharacterWidth)); } }
        public double CharacterHeight { get => _characterHeight; set { _characterHeight = value; OnPropertyChanged(nameof(CharacterHeight)); } }
        public double SpeechX { get => _speechX; set { _speechX = value; OnPropertyChanged(nameof(SpeechX)); } }
        public double SpeechY { get => _speechY; set { _speechY = value; OnPropertyChanged(nameof(SpeechY)); } }
        public double SpeechFontSize { get => _speechFontSize; set { _speechFontSize = value; OnPropertyChanged(nameof(SpeechFontSize)); } }
        public double SpeechMaxWidth { get => _speechMaxWidth; set { _speechMaxWidth = value; OnPropertyChanged(nameof(SpeechMaxWidth)); } }

        private double _spriteFPS = 10.00;
        public double SpriteFPS
        {
            get => _spriteFPS;
            set
            {
                if (!IsBpmMode)
                {
                    _spriteFPS = Math.Round(value, 2);
                    InputBPM = Math.Round((_spriteFPS * IdleSpriteCount) / 60.0, 2);
                    OnPropertyChanged(nameof(InputBPM));
                }
                else
                {
                    _spriteFPS = Math.Round(value, 2); // allow BPM mode override
                }

                OnPropertyChanged(nameof(SpriteFPS));
                OnPropertyChanged(nameof(SpriteSpeedDisplay));
            }
        }


        private int _idleSpriteCount = 1;
        public int IdleSpriteCount
        {
            get => _idleSpriteCount;
            set
            {
                _idleSpriteCount = value < 1 ? 1 : value; // Avoid divide-by-zero
                OnPropertyChanged(nameof(IdleSpriteCount));
                OnPropertyChanged(nameof(SpriteSpeedDisplay));
            }
        }

        public string SpriteSpeedDisplay
        {
            get
            {
                double bpm = 60.0 * SpriteFPS / IdleSpriteCount;
                return IsBpmMode
                    ? $"BPM: {InputBPM:F2} (FPS: {SpriteFPS:F2})"
                    : $"FPS: {SpriteFPS:F2} (BPM: {bpm:F2})";
            }
        }



        private int _downbeatFrameIndex = 0;
        public int DownbeatFrameIndex
        {
            get => _downbeatFrameIndex;
            set
            {
                int clamped = Math.Max(1, Math.Min(value, IdleSpriteCount));
                if (_downbeatFrameIndex != clamped)
                {
                    _downbeatFrameIndex = clamped;
                    OnPropertyChanged(nameof(DownbeatFrameIndex));
                }
            }
        }


        private bool _isBpmMode = false;
        public bool IsBpmMode
        {
            get => _isBpmMode;
            set
            {
                _isBpmMode = value;
                OnPropertyChanged(nameof(IsBpmMode));
                OnPropertyChanged(nameof(SpriteSpeedDisplay));
            }
        }

        private double _inputBpm = 120.0;
        public double InputBPM
        {
            get => _inputBpm;
            set
            {
                _inputBpm = Math.Round(value, 2);
                if (IsBpmMode && IdleSpriteCount > 0)
                {
                    SpriteFPS = (_inputBpm * IdleSpriteCount) / 60.0;
                }
                OnPropertyChanged(nameof(InputBPM));
                OnPropertyChanged(nameof(SpriteSpeedDisplay));
            }
        }

        private PlaybackSpeedMode _playbackSpeedMode = PlaybackSpeedMode.Regular;
        public PlaybackSpeedMode PlaybackSpeedMode
        {
            get => _playbackSpeedMode;
            set
            {
                if (_playbackSpeedMode != value)
                {
                    _playbackSpeedMode = value;
                    OnPropertyChanged(nameof(PlaybackSpeedMode));
                }
            }
        }

        public double PlaybackSpeedMultiplier
        {
            get
            {
                return PlaybackSpeedMode switch
                {
                    PlaybackSpeedMode.HalfTime => 0.5,
                    PlaybackSpeedMode.QuarterTime => 0.25,
                    _ => 1.0,
                };
            }
        }






        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    

}
