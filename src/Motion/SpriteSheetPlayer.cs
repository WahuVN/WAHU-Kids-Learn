using System;

namespace WAHU.Motion
{
    public sealed class SpriteSheetPlayer
    {
        public SpriteSheetPlayer(int frameCount, double framesPerSecond, bool loop)
        {
            if (frameCount <= 0) throw new ArgumentOutOfRangeException("frameCount");
            if (framesPerSecond <= 0) throw new ArgumentOutOfRangeException("framesPerSecond");
            FrameCount = frameCount;
            FramesPerSecond = framesPerSecond;
            Loop = loop;
        }

        public int FrameCount { get; private set; }
        public double FramesPerSecond { get; private set; }
        public bool Loop { get; private set; }

        public int FrameAt(double elapsedMs)
        {
            if (elapsedMs <= 0) return 0;
            var frame = (int)Math.Floor(elapsedMs * FramesPerSecond / 1000.0);
            if (Loop) return frame % FrameCount;
            return Math.Min(FrameCount - 1, frame);
        }
    }
}
