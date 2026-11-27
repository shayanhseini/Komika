using System.Collections.Generic;
using DroneVision.Core;

namespace DroneVision
{
    /// <summary>
    /// Supplies pose landmarks on native builds (Windows / Android / iOS), where the
    /// browser engine is not available. Implement it on a MonoBehaviour that wraps
    /// your pose estimator (e.g. the MediaPipe Unity Plugin's PoseLandmarker) and
    /// assign it to <see cref="DroneVisionInput.landmarkSource"/>.
    ///
    /// Landmarks must be MediaPipe's 33-point topology, normalized 0..1 in the RAW
    /// (unmirrored) camera image, with visibility filled in.
    /// </summary>
    public interface ILandmarkSource
    {
        /// <summary>
        /// Returns true once per NEW camera frame. Fill <paramref name="poses"/> with
        /// every detected person (empty = nobody) and return the frame timestamp.
        /// </summary>
        bool TryGetLatest(List<PoseFrame> poses, out double timestampMs);
    }
}
