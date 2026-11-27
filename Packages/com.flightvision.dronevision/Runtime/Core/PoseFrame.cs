using System;

namespace DroneVision.Core
{
    /// <summary>One MediaPipe pose landmark, normalized 0..1 in the raw (unmirrored) image.</summary>
    [Serializable]
    public struct Landmark
    {
        public double X;
        public double Y;
        public double Visibility;

        public Landmark(double x, double y, double visibility)
        {
            X = x;
            Y = y;
            Visibility = visibility;
        }
    }

    /// <summary>A single detected pose (33 landmarks) plus the frame aspect (width / height).</summary>
    public sealed class PoseFrame
    {
        public const int Count = 33;
        public const int Nose = 0, LShoulder = 11, RShoulder = 12, LElbow = 13, RElbow = 14, LWrist = 15, RWrist = 16, LHip = 23, RHip = 24, LKnee = 25, RKnee = 26, LAnkle = 27, RAnkle = 28;

        public readonly Landmark[] Landmarks = new Landmark[Count];
        public double Aspect = 4.0 / 3.0;
    }
}
