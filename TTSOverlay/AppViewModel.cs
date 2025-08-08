using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Forms;


namespace TTSOverlay
{


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

        private bool _isUpdatingCharacterSize = false;


        public double CharacterX { get => _characterX; set { _characterX = value; OnPropertyChanged(nameof(CharacterX)); } }
        public double CharacterY { get => _characterY; set { _characterY = value; OnPropertyChanged(nameof(CharacterY)); } }
        

        public double CharacterWidth
        {
            get => _characterWidth;
            set
            {
                if (_characterWidth != value)
                {
                    _characterWidth = value;

                    if (IsAspectRatioScalingMode && !_isUpdatingCharacterSize)
                    {
                        _isUpdatingCharacterSize = true;
                        _characterHeight = _characterWidth / aspectRatio;
                        OnPropertyChanged(nameof(CharacterHeight));
                        _isUpdatingCharacterSize = false;
                    }

                    OnPropertyChanged(nameof(CharacterWidth));
                }
            }
        }

        public double CharacterHeight
        {
            get => _characterHeight;
            set
            {
                if (_characterHeight != value)
                {
                    _characterHeight = value;

                    if (IsAspectRatioScalingMode && !_isUpdatingCharacterSize)
                    {
                        _isUpdatingCharacterSize = true;
                        _characterWidth = _characterHeight * aspectRatio;
                        OnPropertyChanged(nameof(CharacterWidth));
                        _isUpdatingCharacterSize = false;
                    }

                    OnPropertyChanged(nameof(CharacterHeight));
                }
            }
        }

        public double SpeechX { get => _speechX; set { _speechX = value; OnPropertyChanged(nameof(SpeechX)); } }
        public double SpeechY { get => _speechY; set { _speechY = value; OnPropertyChanged(nameof(SpeechY)); } }
        public double SpeechFontSize { get => _speechFontSize; set { _speechFontSize = value; OnPropertyChanged(nameof(SpeechFontSize)); } }
        public double SpeechMaxWidth { get => _speechMaxWidth; set { _speechMaxWidth = value; OnPropertyChanged(nameof(SpeechMaxWidth)); } }

        private double _spriteFPS = 10.00;

        public double aspectRatio = 1.0;


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
                int newCount = value < 1 ? 1 : value; // avoid divide-by-zero
                if (_idleSpriteCount != newCount)
                {
                    _idleSpriteCount = newCount;

                    if (IsBpmMode)
                    {
                        // Recalculate FPS to maintain the current BPM
                        SpriteFPS = (InputBPM * _idleSpriteCount) / 60.0;
                    }
                    else
                    {
                        // Recalculate BPM to match the new FPS and frame count
                        InputBPM = Math.Round((SpriteFPS * _idleSpriteCount) / 60.0, 2);
                    }

                    OnPropertyChanged(nameof(IdleSpriteCount));
                    OnPropertyChanged(nameof(SpriteSpeedDisplay));
                }
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



        private int _downbeatFrameIndex = 1;
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


        private bool _isBpmMode = true;
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

        private bool _isEditMode = true;
        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                _isEditMode = value;
                OnPropertyChanged(nameof(IsEditMode));
            }
        }

        private bool _isAspectRatioScalingMode = true;
        public bool IsAspectRatioScalingMode
        {
            get => _isAspectRatioScalingMode;
            set 
            {
                _isAspectRatioScalingMode = value;
                OnPropertyChanged(nameof(IsAspectRatioScalingMode));
            }
        }

        // REMEMBER THESE ARE OPTIONS!!! 
        // IMPORTANT!!!
        public int TotalScreenWidth { get; private set; }
        public int TotalScreenHeight { get; private set; }

        public AppViewModel()
        {
            UpdateScreenBounds();
        }

        public void UpdateScreenBounds()
        {
            var bounds = Screen.AllScreens
                .Select(s => s.Bounds)
                .Aggregate((b1, b2) => System.Drawing.Rectangle.Union(b1, b2));

            TotalScreenWidth = bounds.Width;
            TotalScreenHeight = bounds.Height;

            OnPropertyChanged(nameof(TotalScreenWidth));
            OnPropertyChanged(nameof(TotalScreenHeight));
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
                else
                {
                    _inputBpm = 60.0 * SpriteFPS / IdleSpriteCount;
                }
                    OnPropertyChanged(nameof(InputBPM));
                OnPropertyChanged(nameof(SpriteSpeedDisplay));
            }
        }

        private int _playbackSpeedMode = 1;
        public int PlaybackSpeedMode
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
                return 1.0/(double)PlaybackSpeedMode;
            }
        }




        private bool _isSpeechBoxVisible = true;
        public bool IsSpeechBoxVisible
        {
            get => _isSpeechBoxVisible;
            set
            {
                if (_isSpeechBoxVisible != value)
                {
                    _isSpeechBoxVisible = value;
                    OnPropertyChanged(nameof(IsSpeechBoxVisible));
                }
            }
        }

        public ObservableCollection<SpriteGroup> SpriteGroups { get; set; } = new ObservableCollection<SpriteGroup>();

        //This one is JUST for the first one. That's why the sprite group is zero
        public void AddFirstGroup()
        {
            SpriteGroups.Add(new SpriteGroup
            {
                //Try to make this add all of the variables. Update in real time, OnPropertyChanged -- change it for currently active SpriteGroup within the SpriteGroup class!!
                X = 100,
                Y = 100,
                Width = 100,
                Height = 100,
                Name = "Placeholder",
                
            });
            SpriteGroups[0].LoadIdleSprites(Directory.GetFiles("Assets/Idle", "*.png"));
            SpriteGroups[0].LoadTalkingSprites(Directory.GetFiles("Assets/Talking", "*.png"));
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    

}
