using System.Collections.Generic;

namespace DroneVision.Core
{
    /// <summary>
    /// Layout of the float buffer the page hands to Unity every frame: raw
    /// landmarks and clothing-colour signatures of up to 3 people, plus status.
    /// Twin of the WebGL template's dronevision/feed-buffer.js.
    /// </summary>
    public static class FeedBuffer
    {
        public const int Version = 0, Status = 1, Seq = 2, THi = 3, TLo = 4, Aspect = 5, Poses = 6,
            Fps = 7, InferMs = 8, LatencyMs = 9, Paused = 10, Gain = 11, Lum = 12, Focus = 13,
            PoseData = 16, MaxPoses = 3, Stride = 33 * 4,
            SigData = PoseData + MaxPoses * Stride, SigStride = 4;
        public const int Length = SigData + MaxPoses * SigStride; // 424
        public const int BufferVersion = 3;
        /// <summary>Feed status code while the camera and model are running.</summary>
        public const int StatusRunning = 2;

        /// <summary>Frame timestamp in milliseconds (split in two floats for precision).</summary>
        public static double Timestamp(float[] b)
        {
            return b[THi] * 65536.0 + b[TLo];
        }

        /// <summary>
        /// Decodes the poses into <paramref name="pool"/> (reused, no allocation), lists them in
        /// <paramref name="into"/> and their clothing signatures in <paramref name="sigs"/>.
        /// </summary>
        public static int ReadPoses(float[] b, PoseFrame[] pool, List<PoseFrame> into, List<ColorSig> sigs)
        {
            into.Clear();
            if (sigs != null) sigs.Clear();
            int count = (int)b[Poses];
            if (count > pool.Length) count = pool.Length;
            if (count > MaxPoses) count = MaxPoses;
            double aspect = b[Aspect];
            for (int p = 0; p < count; p++)
            {
                PoseFrame f = pool[p];
                f.Aspect = aspect;
                int basePos = PoseData + p * Stride;
                for (int i = 0; i < PoseFrame.Count; i++)
                {
                    int o = basePos + i * 4;
                    f.Landmarks[i] = new Landmark(b[o], b[o + 1], b[o + 3]);
                }
                into.Add(f);
                if (sigs != null)
                {
                    int so = SigData + p * SigStride;
                    sigs.Add(b[so + 3] > 0.5f ? new ColorSig(b[so], b[so + 1], b[so + 2]) : default(ColorSig));
                }
            }
            return count;
        }
    }
}
