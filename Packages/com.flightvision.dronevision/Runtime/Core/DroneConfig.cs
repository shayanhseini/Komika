// DroneVision.Core — pure C# (no UnityEngine), C# 7.3, netstandard2.x.
// 1:1 port of the PC app's DroneConfig (Mashk/mashk/drone.py + churn.py Config, the
// fields the drone and the crowd tracker read). Both are checked against
// golden/drone_vectors.json. Change them together.
using System;

namespace DroneVision.Core
{
    [Serializable]
    public class DroneConfig
    {
        // gesture mode: upright body lean (runner left/right) vs legacy open wings
        public bool uprightLeanMode = true;
        // roll (left / right): normalized torso lean + shoulder tilt (or wrist diff in wing mode)
        public double rollEnter = 0.24;
        /// <summary>Difference that maps to Roll = ±1 (analog output).</summary>
        public double rollFull = 0.55;
        // pitch (up / down): both wrists above / below the shoulder line by this much (unused in uprightLeanMode)
        public double upMargin = 0.25;
        public double downMargin = 0.3;
        /// <summary>Mean wrist height that maps to Pitch = ±1 (analog output).</summary>
        public double pitchFull = 1.0;
        /// <summary>A zone is left only below enter × this (no flicker).</summary>
        public double releaseRatio = 0.48;
        /// <summary>A zone change must hold this many frames (exhibition light jitter).</summary>
        public int zoneFrames = 3;
        /// <summary>Minimum cooldown (ms) between consecutive triggers in the same direction.</summary>
        public double leanCooldownMs = 340;
        /// <summary>Minimum upright time (ms) required before re-arming the same lean direction.</summary>
        public double leanNeutralRearmMs = 110;
        // running-in-place cadence & vigor detection
        public bool enableJogDetection = true;
        public double jogStepDeadband = 0.0085;
        public double jogKneeDeadband = 0.025;
        public double jogArmDeadband = 0.032;
        public double jogTimeoutMs = 620;
        public double jogMinHz = 1.15;
        public double jogSprintHz = 2.45;
        // wings: both wrists above this fraction of the shoulder → hip distance (hanging arms ≈ 1.12-1.19)
        public double holdLine = 0.9;
        public double handVisibility = 0.35;
        public double handGraceMs = 250;
        public double edgeMargin = 0.01;
        // smoothing + latency
        public double minCutoff = 1.5;
        public double beta = 1.5;
        public double dCutoff = 1.0;
        public double leadMs = 30;
        public double maxLead = 0.15;
        public double widthTauS = 1.0;
        // body quality gates
        public double minVisibility = 0.5;
        public double hipVisibility = 0.5;
        public double minFacing = 0.35;
        public double minScale = 0.06;
        // resting tilt (people and cameras are not level), learned after the lock
        public double restAdaptTauS = 4.0;
        public double restRollLimit = 0.14;
        /// <summary>Standing/gesture held this long → locked (green frame).</summary>
        public double lockMs = 350;
        // presence
        public double graceMs = 250;
        /// <summary>A LOCKED player may be hidden this long without losing the lock.</summary>
        public double lockGraceMs = 1200;
        /// <summary>Gone this long = a new visitor; until then a returning player is recognised.</summary>
        public double resetMs = 2500;
        public double filterResetGapMs = 200;
        public bool invert = false;
        // crowd: who becomes the player, and keeping that same person (OperatorTracker)
        public double stageZone = 0.8;
        public double minOperatorScale = 0.04;
        public double jumpBase = 0.8;
        public double jumpPerSec = 3.0;
        public double scaleMin = 0.6;
        public double scaleMax = 1.65;
        public double colorWeight = 3;
        public double colorVeto = 0.18;
        public double reacquireMs = 1000;

        public DroneConfig Clone()
        {
            return (DroneConfig)MemberwiseClone();
        }
    }
}
