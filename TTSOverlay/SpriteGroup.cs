using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.IO;

namespace TTSOverlay
{
    public class SpriteGroup : INotifyPropertyChanged
    {
        public string Name { get; set; } = "Sprite Group";
        public List<BitmapImage> IdleSprites { get; set; } = new List<BitmapImage>();
        public List<BitmapImage> TalkingSprites { get; set; } = new List<BitmapImage>();

        private int idleFrameIndex = 0;
        private int talkingFrameIndex = 0;
        private bool isSpeaking;

        //So I'm thinking these are for saving and loading that into viewmodel. The most important part!
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 150;
        public double Height { get; set; } = 250;

        public double SpriteFPS { get; set; } = 10;
        public double PlaybackSpeedMultiplier { get; set; } = 1;

        public int IdleSpriteCount => IdleSprites.Count;
        public int TalkingSpriteCount => TalkingSprites.Count;

        public string CurrentText { get; set; } = "";

        public bool IsSpeaking
        {
            get => isSpeaking;
            set
            {
                isSpeaking = value;
                ResetFrameIndex();
                OnPropertyChanged(nameof(IsSpeaking));
            }
        }

        public BitmapImage CurrentFrame =>
            IsSpeaking ? GetTalkingFrame() : GetIdleFrame();

        public int GetCurrentFrameIndex() => IsSpeaking ? talkingFrameIndex : idleFrameIndex;


        public BitmapImage AdvanceFrame()
        {
            if (IsSpeaking && TalkingSprites.Count > 0)
            {
                talkingFrameIndex = (talkingFrameIndex + 1) % TalkingSprites.Count;
                OnPropertyChanged(nameof(CurrentFrame));
                return TalkingSprites[talkingFrameIndex];
            }
            else if (IdleSprites.Count > 0)
            {
                idleFrameIndex = (idleFrameIndex + 1) % IdleSprites.Count;
                OnPropertyChanged(nameof(CurrentFrame));
                return IdleSprites[idleFrameIndex];
            }

            return null;
        }

        public void ResetToDownbeat(int frameIndex)
        {
            if (IsSpeaking && TalkingSprites.Count > 0)
            {
                talkingFrameIndex = frameIndex % TalkingSprites.Count;
            }
            else if (IdleSprites.Count > 0)
            {
                idleFrameIndex = frameIndex % IdleSprites.Count;
            }
        }

        private void ResetFrameIndex()
        {
            idleFrameIndex = 0;
            talkingFrameIndex = 0;
        }

        public void LoadIdleSprites(string[] filePaths)
        {

            IdleSprites.Clear();
            foreach (var file in filePaths)
                IdleSprites.Add(LoadImage(file));
            idleFrameIndex = 0;
        }

        public void LoadTalkingSprites(string[] filePaths)
        {
            TalkingSprites.Clear();
            foreach (var file in filePaths)
                TalkingSprites.Add(LoadImage(file));
            talkingFrameIndex = 0;
        }

        private BitmapImage LoadImage(string path)
        {
            return new BitmapImage(new Uri(Path.GetFullPath(path)));
        }


        private BitmapImage GetIdleFrame() => IdleSprites.Count > 0 ? IdleSprites[idleFrameIndex] : null;
        private BitmapImage GetTalkingFrame() => TalkingSprites.Count > 0 ? TalkingSprites[talkingFrameIndex] : null;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
