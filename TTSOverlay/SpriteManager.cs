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
    public class SpriteManager
    {
        List<BitmapImage> talkingSprites = new List<BitmapImage>();
        List<BitmapImage> idleSprites = new List<BitmapImage>();

        private int idleFrameIndex = 0;
        private int talkingFrameIndex = 0;

        private MainWindow mainWindow;

        public BitmapImage CurrentSprite => IsSpeaking
            ? GetTalkingFrame()
            : GetIdleFrame();

        public bool IsSpeaking { get; private set; }

        public int IdleSpriteCount => idleSprites.Count;
        public int TalkingSpriteCount => talkingSprites.Count;

        public int GetCurrentFrameIndex() => IsSpeaking ? talkingFrameIndex : idleFrameIndex;

        public void SetSpeakingState(bool speaking)
        {
            IsSpeaking = speaking;
            ResetFrameIndex();
        }

        public void LoadIdleSprites(string[] filePaths)
        {
            idleSprites.Clear();
            foreach (var file in filePaths)
                idleSprites.Add(LoadImage(file));
            idleFrameIndex = 0;
        }

        public void LoadTalkingSprites(string[] filePaths)
        {
            talkingSprites.Clear();
            foreach (var file in filePaths)
                talkingSprites.Add(LoadImage(file));
            talkingFrameIndex = 0;
        }

        public BitmapImage AdvanceFrame()
        {
            if (IsSpeaking)
            {
                talkingFrameIndex = (talkingFrameIndex + 1) % talkingSprites.Count;
                return talkingSprites[talkingFrameIndex];
            }
            else
            {
                idleFrameIndex = (idleFrameIndex + 1) % idleSprites.Count;


                return idleSprites[idleFrameIndex];
            }
        }

        public void ResetToDownbeat(int frameIndex)
        {
            if (IsSpeaking && talkingSprites.Count > 0)
            {
                talkingFrameIndex = frameIndex % talkingSprites.Count;
            }
            else if (idleSprites.Count > 0)
            {
                idleFrameIndex = frameIndex % idleSprites.Count;
            }
        }

        private void ResetFrameIndex()
        {
            if (IsSpeaking)
                talkingFrameIndex = 0;
            else
                idleFrameIndex = 0;
        }

        private BitmapImage GetIdleFrame()
        {
            return idleSprites.Count > 0 ? idleSprites[idleFrameIndex] : null!;
        }

        private BitmapImage GetTalkingFrame()
        {
            return talkingSprites.Count > 0 ? talkingSprites[talkingFrameIndex] : null!;
        }

        private BitmapImage LoadImage(string path)
        {
            return new BitmapImage(new Uri(Path.GetFullPath(path)));
        }

    }
}
