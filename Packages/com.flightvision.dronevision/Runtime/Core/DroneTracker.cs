using System;

namespace DroneVision.Core
{
    public enum DroneStatus { Searching = 0, Calibrating = 1, Ready = 2 }

    /// <summary>What the player should do. The page shows these in Persian.</summary>
    public enum DroneHint
    {
        None = 0, StepBack = 1, FaceCamera = 2, ComeCloser = 3, MoveToCenter = 4,
        /// <summary>Both hands are down: raise them like wings.</summary>
        RaiseHands = 5,
        /// <summary>Only one hand is up: raise both.</summary>
        BothHands = 6,
    }

    /// <summary>Body geometry of one pose. Lengths are in frame-HEIGHT units (x aspect-corrected).</summary>
    public struct PoseMeasure
    {
        public double ShoulderWidth;
        public double ShoulderTilt;
        public bool HasTorso;
        public double TorsoLen;
        public double HipCenterX;
        public bool HasHips;
        public double HipTiltY;
        public bool HasNose;
        public double NoseX;
        public double NoseY;
        public bool HasKnees;
        public double KneeDiffY;
        public bool HasElbows;
        public double ElbowDiffY;
        public double CenterX;
        public double CenterY;
        /// <summary>Shoulder width as a fraction of frame width.</summary>
        public double Scale;
    }

    public sealed class DroneState
    {
        public DroneStatus Status = DroneStatus.Searching;
        /// <summary>Analog -1..1, + = bank to the player's own right.</summary>
        public double Roll;
        /// <summary>Analog -1..1, + = up.</summary>
        public double Pitch;
        /// <summary>Wrist height difference in shoulder widths (rest tilt removed).</summary>
        public double RawRoll;
        /// <summary>Mean wrist height above the shoulder line, in shoulder widths.</summary>
        public double RawPitch;
        /// <summary>Roll zone: -1 left / 0 / +1 right (hysteresis + debounce). = keys A / D.</summary>
        public int Side;
        /// <summary>-1 left / +1 right ONLY on the frame an anti-bounce validated lean triggers, else 0.</summary>
        public int LeanTrigger;
        /// <summary>Pitch zone: -1 down / 0 / +1 up. = keys S / W.</summary>
        public int Vert;
        /// <summary>In-place running intensity 0..1 (0 = standing still Idle, 0.35..0.68 = jog/run, 0.70..1.0 = sprint).</summary>
        public double RunIntensity;
        /// <summary>Detected in-place stepping cadence in steps/sec.</summary>
        public double StepCadence;
        /// <summary>Zone entries while locked (monotonic counters).</summary>
        public int Turns;
        public int Climbs;
        public int Dives;
        /// <summary>Wings up: both wrists usable and above the hold line.</summary>
        public bool HandsHeld;
        public DroneHint Hint = DroneHint.None;
        /// <summary>= LockProgress.</summary>
        public double RestProgress;
        public bool Tracking;
        public bool Locked;
        public double LockProgress;
        /// <summary>Index of the player among the detected poses, or -1 (set by PlayerTracker).</summary>
        public int Operator = -1;
    }

    /// <summary>
    /// Fixed-wing drone gesture ("wings"), measured on the WRISTS only. Pure: the caller
    /// passes landmarks and a timestamp. 1:1 port of the PC app's mashk/drone.py
    /// (golden: golden/drone_vectors.json).
    ///   wings  both wrists in the frame and above the hold line (how far apart does not matter).
    ///   roll   left / right = how much higher one wrist is than the other, in shoulder widths.
    ///   pitch  up / down = both wrists above (+1) / below (-1) the shoulder line.
    ///   lock   wings held for lockMs, moving or not. The resting tilt is learned afterwards,
    ///          slowly, only while no roll zone is active.
    /// Positive = the PLAYER'S OWN right / up. Landmarks: raw (unmirrored) image, 0..1.
    /// </summary>
    public sealed class DroneTracker
    {
        public readonly DroneConfig Config;
        public readonly DroneState State = new DroneState();

        private readonly OneEuroFilter _fRoll, _fLeft, _fRight;
        private bool _hasSeen;
        private double _lastSeen;
        private double _wRef;
        private bool _hasGrip;
        private double _lastGrip;
        private bool _hasGripSince;
        private double _gripSince;      // start of the current wings-up stretch (the lock timer)
        private double _tilt;           // learned resting tilt (left up - right up)
        private bool _lostLocked;
        private readonly int[] _want = new int[2];   // debounce: pending zone and how many frames it has held
        private readonly int[] _count = new int[2];

        // anti-double-trigger lean latch
        private int _lastTriggerDir;
        private double _lastTriggerMs = -1e9;
        private bool _inNeutral = true;
        private double _neutralSinceMs;
        private bool _neutralArmed = true;

        // running-in-place cadence & vigor detector state (raw Y baselines prevent shoulder-width foreshortening spikes)
        private bool _hasBob;
        private double _bobRawMean;
        private double _bobFast;
        private double _prevBobFast;
        private int _bobCycleDir;
        private double _bobExtreme;
        private bool _hasKnee;
        private double _kneeRawMean;
        private int _kneeCycleDir;
        private double _kneeExtreme;
        private bool _hasHipAlt;
        private double _hipAltRawMean;
        private bool _hasArm;
        private double _armRawMean;
        private int _armCycleDir;
        private double _armExtreme;
        private double _motionEnergy;
        private double _sustainedEnergyMs;
        private double _prevRawRoll;
        private double _suppressStepUntilMs = -1e9;
        private double _lastAnyStepMs = -1e9;
        private readonly double[] _stepTimes = new double[16];
        private int _stepCount;
        private int _stepWriteIdx;

        private struct Wrist
        {
            public bool Out;
            public bool Held;
            public double Up;
        }

        public DroneTracker(DroneConfig config = null)
        {
            Config = config ?? new DroneConfig();
            _fRoll = new OneEuroFilter(Config.minCutoff, Config.beta, Config.dCutoff);
            _fLeft = new OneEuroFilter(Config.minCutoff, Config.beta, Config.dCutoff);
            _fRight = new OneEuroFilter(Config.minCutoff, Config.beta, Config.dCutoff);
        }

        /// <summary>Call after changing <see cref="Config"/> filter fields at runtime.</summary>
        public void ApplyConfig()
        {
            foreach (OneEuroFilter f in new[] { _fRoll, _fLeft, _fRight })
            {
                f.MinCutoff = Config.minCutoff;
                f.Beta = Config.beta;
                f.DCutoff = Config.dCutoff;
            }
        }

        private void ResetFilters()
        {
            _fRoll.Reset();
            _fLeft.Reset();
            _fRight.Reset();
        }

        /// <summary>Forget the current visitor's resting tilt and lock.</summary>
        public void Recalibrate()
        {
            ResetFilters();
            _wRef = 0;
            _tilt = 0;
            _hasGrip = false;
            _hasGripSince = false;
            _lostLocked = false;
            ResetLeanLatch();
            ResetJogState();
            Release();
            State.Locked = false;
            State.LockProgress = 0;
        }

        private void ResetLeanLatch()
        {
            _lastTriggerDir = 0;
            _lastTriggerMs = -1e9;
            _inNeutral = true;
            _neutralSinceMs = 0;
            _neutralArmed = true;
        }

        private void ResetJogState()
        {
            _hasBob = false;
            _bobRawMean = 0;
            _bobFast = 0;
            _prevBobFast = 0;
            _bobCycleDir = 0;
            _bobExtreme = 0;
            _hasKnee = false;
            _kneeRawMean = 0;
            _kneeCycleDir = 0;
            _kneeExtreme = 0;
            _hasHipAlt = false;
            _hipAltRawMean = 0;
            _hasArm = false;
            _armRawMean = 0;
            _armCycleDir = 0;
            _armExtreme = 0;
            _motionEnergy = 0;
            _sustainedEnergyMs = 0;
            _prevRawRoll = 0;
            _suppressStepUntilMs = -1e9;
            _lastAnyStepMs = -1e9;
            _stepCount = 0;
            _stepWriteIdx = 0;
            State.RunIntensity = 0;
            State.StepCadence = 0;
        }

        /// <summary>The SAME player came back (recognised by the crowd tracker): lock back at once.</summary>
        public void Reclaim()
        {
            if (_lostLocked)
            {
                State.Locked = true;
                State.LockProgress = 1;
            }
            _lostLocked = false;
        }

        private void Release()
        {
            DroneState s = State;
            s.Roll = 0; s.Pitch = 0; s.RawRoll = 0; s.RawPitch = 0;
            s.Side = 0; s.Vert = 0; s.LeanTrigger = 0;
            s.RunIntensity = 0; s.StepCadence = 0;
            s.HandsHeld = false;
            _want[0] = 0; _want[1] = 0;
            _count[0] = 0; _count[1] = 0;
            ResetLeanLatch();
            ResetJogState();
        }

        private int Debounce(int axis, int cur, int want)
        {
            if (want == cur)
            {
                _count[axis] = 0;
                return cur;
            }
            if (want != _want[axis])
            {
                _want[axis] = want;
                _count[axis] = 0;
            }
            _count[axis]++;
            if (_count[axis] >= Math.Max(1, Config.zoneFrames))
            {
                _count[axis] = 0;
                return want;
            }
            return cur;
        }

        /// <summary>Schmitt trigger on a signed value: -1 / 0 / +1.</summary>
        private static int Zone(double value, int cur, double enter, double release)
        {
            if (cur > 0) return value > release ? 1 : (value < -enter ? -1 : 0);
            if (cur < 0) return value < -release ? -1 : (value > enter ? 1 : 0);
            return value > enter ? 1 : value < -enter ? -1 : 0;
        }

        private static bool Visible(Landmark p, double min)
        {
            return p.Visibility >= min;
        }

        /// <summary>Body geometry (shoulders, optional hips). False when the shoulders are not usable.</summary>
        public static bool Measure(PoseFrame frame, DroneConfig cfg, out PoseMeasure m)
        {
            m = new PoseMeasure();
            Landmark[] lm = frame.Landmarks;
            double aspect = frame.Aspect;
            Landmark ls = lm[PoseFrame.LShoulder], rs = lm[PoseFrame.RShoulder];
            if (!Visible(ls, cfg.minVisibility) || !Visible(rs, cfg.minVisibility)) return false;
            double sx = (ls.X + rs.X) / 2, sy = (ls.Y + rs.Y) / 2;
            double wx = (ls.X - rs.X) * aspect, wy = rs.Y - ls.Y;
            m.ShoulderWidth = MathUtil.Hypot(wx, wy);
            m.ShoulderTilt = wy;
            Landmark nose = lm[PoseFrame.Nose];
            if (Visible(nose, 0.35))
            {
                m.HasNose = true;
                m.NoseX = nose.X;
                m.NoseY = nose.Y;
            }
            Landmark lh = lm[PoseFrame.LHip], rh = lm[PoseFrame.RHip];
            if (Visible(lh, cfg.hipVisibility) && Visible(rh, cfg.hipVisibility))
            {
                double hx = (lh.X + rh.X) / 2, hy = (lh.Y + rh.Y) / 2;
                if (hy <= 1.02) // hips predicted below the frame edge are guesses
                {
                    double tx = (hx - sx) * aspect, ty = hy - sy;
                    double len = MathUtil.Hypot(tx, ty);
                    if (len > 1e-6) { m.HasTorso = true; m.TorsoLen = len; m.HipCenterX = hx; m.HasHips = true; m.HipTiltY = lh.Y - rh.Y; }
                }
            }
            Landmark lk = lm[PoseFrame.LKnee], rk = lm[PoseFrame.RKnee];
            if (Visible(lk, 0.40) && Visible(rk, 0.40) && Math.Max(lk.Y, rk.Y) <= 1.05)
            {
                m.HasKnees = true;
                m.KneeDiffY = lk.Y - rk.Y;
            }
            Landmark le = lm[PoseFrame.LElbow], re = lm[PoseFrame.RElbow];
            if (Visible(le, 0.40) && Visible(re, 0.40) && Math.Max(le.Y, re.Y) <= 1.04)
            {
                m.HasElbows = true;
                m.ElbowDiffY = le.Y - re.Y;
            }
            m.CenterX = sx;
            m.CenterY = sy;
            m.Scale = m.ShoulderWidth / aspect;
            return true;
        }

        private Wrist MeasureWrist(Landmark p, PoseMeasure m, double torso)
        {
            DroneConfig cfg = Config;
            Wrist w = new Wrist();
            w.Out = p.X < cfg.edgeMargin || p.X > 1 - cfg.edgeMargin || p.Y < cfg.edgeMargin || p.Y > 1 - cfg.edgeMargin;
            bool ok = !w.Out && Visible(p, cfg.handVisibility);
            w.Held = ok && p.Y - m.CenterY < cfg.holdLine * torso;
            w.Up = ok ? (m.CenterY - p.Y) / _wRef : 0.0;
            return w;
        }

        /// <param name="frame">the player's pose, or null when nobody is detected</param>
        /// <param name="tMs">monotonic timestamp in milliseconds</param>
        public DroneState Update(PoseFrame frame, double tMs)
        {
            DroneConfig cfg = Config;
            DroneState s = State;
            s.LeanTrigger = 0;

            PoseMeasure m = new PoseMeasure();
            bool hasM = frame != null && Measure(frame, cfg, out m);
            if (!hasM)
            {
                double gone = _hasSeen ? tMs - _lastSeen : double.PositiveInfinity;
                if (gone > cfg.resetMs)
                {
                    Recalibrate();
                    _hasSeen = false;
                }
                if (gone > cfg.graceMs)
                {
                    Release();
                    _hasGripSince = false;
                    s.Status = DroneStatus.Searching;
                    s.Hint = DroneHint.None;
                    s.Tracking = false;
                    // a locked player keeps the lock through a short occlusion (lockGraceMs)
                    if (!s.Locked || gone > cfg.lockGraceMs)
                    {
                        if (s.Locked) _lostLocked = true;
                        s.Locked = false;
                        s.LockProgress = 0;
                        s.RestProgress = 0;
                    }
                }
                return s;
            }

            bool hadPrev = _hasSeen;
            double prevSeen = _lastSeen;
            _hasSeen = true;
            _lastSeen = tMs;
            double dtS = hadPrev ? (tMs - prevSeen) / 1000 : 0;
            if (_wRef <= 0 || dtS <= 0) _wRef = m.ShoulderWidth;
            else _wRef += (m.ShoulderWidth - _wRef) * (1 - Math.Exp(-dtS / cfg.widthTauS));

            bool facing = !m.HasTorso || m.ShoulderWidth / m.TorsoLen >= cfg.minFacing;
            if (cfg.uprightLeanMode)
            {
                bool grip = facing && m.Scale >= cfg.minScale;
                DroneHint hint = m.Scale < cfg.minScale ? DroneHint.ComeCloser
                    : (!facing ? DroneHint.FaceCamera : DroneHint.None);

                if (grip)
                {
                    bool hadGrip = _hasGrip;
                    double prevGrip = _lastGrip;
                    _hasGrip = true;
                    _lastGrip = tMs;
                    if (!_hasGripSince) { _hasGripSince = true; _gripSince = tMs; }
                    s.HandsHeld = true;
                    if (hadGrip && tMs - prevGrip > cfg.filterResetGapMs) ResetFilters();

                    double shoulderTilt = m.ShoulderTilt / _wRef; // >0 when player's right shoulder dips (leaning right)
                    double bodyShift;
                    if (m.HasTorso)
                    {
                        double torsoShift = (m.HipCenterX - m.CenterX) * frame.Aspect / _wRef;
                        double headBonus = m.HasNose ? MathUtil.Clamp((m.CenterX - m.NoseX) * frame.Aspect / _wRef, -0.35, 0.35) : 0.0;
                        bodyShift = 0.72 * torsoShift + 0.45 * shoulderTilt + 0.15 * headBonus;
                    }
                    else
                    {
                        double headShift = m.HasNose ? MathUtil.Clamp((m.CenterX - m.NoseX) * frame.Aspect / _wRef * 1.35, -0.35, 0.35) : 0.0;
                        bodyShift = m.HasNose ? (0.95 * shoulderTilt + 0.32 * headShift) : 1.15 * shoulderTilt;
                    }
                    if (cfg.invert) bodyShift = -bodyShift;

                    int prevSide = s.Side;
                    double rawRoll = bodyShift - _tilt;
                    double t = tMs / 1000;
                    double f = _fRoll.Apply(rawRoll, t);
                    double v = f + MathUtil.Clamp(_fRoll.Dx * cfg.leadMs / 1000, -cfg.maxLead, cfg.maxLead);
                    double rel = cfg.releaseRatio;
                    int side = Zone(v, s.Side, cfg.rollEnter, cfg.rollEnter * rel);
                    if (side != 0 && side != s.Side && rawRoll * side <= cfg.rollEnter * rel) side = 0;
                    side = Debounce(0, s.Side, side);

                    // Require returning upright for leanNeutralRearmMs before firing the same direction again
                    if (side == 0)
                    {
                        if (!_inNeutral)
                        {
                            _inNeutral = true;
                            _neutralSinceMs = tMs;
                        }
                        if (tMs - _neutralSinceMs >= cfg.leanNeutralRearmMs)
                        {
                            _neutralArmed = true;
                        }
                    }
                    else
                    {
                        _inNeutral = false;
                        if (s.Locked && prevSide != side)
                        {
                            bool opposite = _lastTriggerDir != 0 && side == -_lastTriggerDir;
                            double neededCooldown = opposite ? cfg.leanCooldownMs * 0.55 : cfg.leanCooldownMs;
                            bool canFire = (tMs - _lastTriggerMs >= neededCooldown) && (opposite || _neutralArmed);
                            if (canFire)
                            {
                                _lastTriggerDir = side;
                                _lastTriggerMs = tMs;
                                _neutralArmed = false;
                                s.Turns++;
                                s.LeanTrigger = side;
                            }
                        }
                    }

                    s.Side = side;
                    s.Vert = 0;
                    s.RawRoll = rawRoll;
                    s.RawPitch = 0;
                    s.Roll = MathUtil.Clamp(f / cfg.rollFull, -1, 1);
                    s.Pitch = 0;

                    if (cfg.enableJogDetection)
                    {
                        UpdateJogCadence(in m, tMs, dtS, rawRoll, hadGrip && tMs - prevGrip <= cfg.filterResetGapMs);
                    }
                    else
                    {
                        s.RunIntensity = 0;
                        s.StepCadence = 0;
                    }

                    if (hadGrip && tMs - prevGrip <= cfg.filterResetGapMs)
                    {
                        if (!s.Locked && Math.Abs(bodyShift - _tilt) < 0.18)
                        {
                            double aFast = 1 - Math.Exp(-(tMs - prevGrip) / 1000 / Math.Max(0.3, cfg.restAdaptTauS * 0.15));
                            _tilt = MathUtil.Clamp(_tilt + (bodyShift - _tilt) * aFast, -0.15, 0.15);
                        }
                        else if (side == 0 && s.Locked && (tMs - _lastTriggerMs > 420.0) && Math.Abs(bodyShift - _tilt) < cfg.rollEnter * 0.42)
                        {
                            double a = 1 - Math.Exp(-(tMs - prevGrip) / 1000 / cfg.restAdaptTauS);
                            _tilt = MathUtil.Clamp(_tilt + (bodyShift - _tilt) * a, -cfg.restRollLimit, cfg.restRollLimit);
                        }
                    }
                }
                else if (!_hasGrip || tMs - _lastGrip > cfg.handGraceMs)
                {
                    Release();
                    _hasGripSince = false;
                    ResetFilters();
                }
                else
                {
                    hint = DroneHint.None;
                }
                s.Hint = hint;
            }
            else
            {
            double torso = m.HasTorso ? m.TorsoLen : m.ShoulderWidth / 0.68;
            Wrist L = MeasureWrist(frame.Landmarks[PoseFrame.LWrist], m, torso);
            Wrist R = MeasureWrist(frame.Landmarks[PoseFrame.RWrist], m, torso);
            bool grip = facing && L.Held && R.Held;

            DroneHint hint = m.Scale < cfg.minScale ? DroneHint.ComeCloser : DroneHint.None;
            if (!facing) hint = DroneHint.FaceCamera;
            else if (L.Out || R.Out) hint = DroneHint.StepBack;
            else if (!grip) hint = L.Held || R.Held ? DroneHint.BothHands : DroneHint.RaiseHands;

            if (grip)
            {
                double lUp = L.Up, rUp = R.Up;
                if (cfg.invert) { double tmp = lUp; lUp = rUp; rUp = tmp; }
                bool hadGrip = _hasGrip;
                double prevGrip = _lastGrip;
                _hasGrip = true;
                _lastGrip = tMs;
                if (!_hasGripSince) { _hasGripSince = true; _gripSince = tMs; }
                s.HandsHeld = true;
                if (hadGrip && tMs - prevGrip > cfg.filterResetGapMs) ResetFilters();
                double t = tMs / 1000;
                double rawRoll = (lUp - rUp) - _tilt;         // left wrist higher = bank right
                double f = _fRoll.Apply(rawRoll, t);
                double v = f + MathUtil.Clamp(_fRoll.Dx * cfg.leadMs / 1000, -cfg.maxLead, cfg.maxLead);
                double lu = _fLeft.Apply(lUp, t), ru = _fRight.Apply(rUp, t);
                double rel = cfg.releaseRatio;
                int side = Zone(v, s.Side, cfg.rollEnter, cfg.rollEnter * rel);
                double lo = Math.Min(lu, ru), hi = Math.Max(lu, ru);
                int vert;
                if (s.Vert > 0) vert = lo > cfg.upMargin * rel ? 1 : 0;
                else if (s.Vert < 0) vert = hi < -cfg.downMargin * rel ? -1 : 0;
                else vert = 0;
                if (vert == 0) vert = lo > cfg.upMargin ? 1 : hi < -cfg.downMargin ? -1 : 0;
                // a NEW zone also needs the raw wrists to be there: the filter tail of a
                // one-frame landmark glitch (a wrist jumping to a bystander) is not a gesture
                if (side != 0 && side != s.Side && rawRoll * side <= cfg.rollEnter * rel) side = 0;
                if (vert > 0 && s.Vert <= 0 && Math.Min(lUp, rUp) <= cfg.upMargin * rel) vert = 0;
                if (vert < 0 && s.Vert >= 0 && Math.Max(lUp, rUp) >= -cfg.downMargin * rel) vert = 0;
                side = Debounce(0, s.Side, side);
                vert = Debounce(1, s.Vert, vert);
                if (s.Locked)
                {
                    if (side != 0 && side != s.Side) s.Turns++;
                    if (vert != 0 && vert != s.Vert)
                    {
                        if (vert > 0) s.Climbs++;
                        else s.Dives++;
                    }
                }
                s.Side = side;
                s.Vert = vert;
                s.RawRoll = rawRoll;
                s.RawPitch = (lu + ru) / 2;
                s.Roll = MathUtil.Clamp(f / cfg.rollFull, -1, 1);
                s.Pitch = MathUtil.Clamp(s.RawPitch / cfg.pitchFull, -1, 1);
                // the resting tilt drifts in only while nothing is pressed
                if (side == 0 && s.Locked && hadGrip && tMs - prevGrip <= cfg.filterResetGapMs)
                {
                    double a = 1 - Math.Exp(-(tMs - prevGrip) / 1000 / cfg.restAdaptTauS);
                    _tilt = MathUtil.Clamp(_tilt + ((lUp - rUp) - _tilt) * a, -cfg.restRollLimit, cfg.restRollLimit);
                }
            }
            else if (!_hasGrip || tMs - _lastGrip > cfg.handGraceMs)
            {
                Release();
                _hasGripSince = false;
                ResetFilters();
            }
            else
            {
                hint = DroneHint.None; // a wrist that blurs out for a moment keeps the wings (and the held keys)
            }
            s.Hint = hint;
            }

            s.Tracking = true;
            if (!s.Locked)
            {
                // the lock only advances while the player holds the active stance
                if (!_hasGripSince) s.LockProgress = 0;
                else if (_hasGrip) s.LockProgress = MathUtil.Clamp((tMs - _gripSince) / cfg.lockMs, 0, 1);
                if (s.LockProgress >= 1) s.Locked = true;
            }
            else
            {
                s.LockProgress = 1;
            }
            s.RestProgress = s.LockProgress;
            s.Status = s.Locked ? DroneStatus.Ready : DroneStatus.Calibrating;
            return s;
        }

        private void UpdateJogCadence(in PoseMeasure m, double tMs, double dtS, double rawRoll, bool continuous)
        {
            DroneConfig cfg = Config;
            DroneState s = State;

            double yRaw = m.HasNose ? (0.65 * m.CenterY + 0.35 * m.NoseY) : m.CenterY;

            if (!continuous || dtS <= 0 || dtS > 0.75 || _wRef <= 1e-6)
            {
                _hasBob = true;
                _bobRawMean = yRaw;
                _bobFast = 0;
                _prevBobFast = 0;
                _bobCycleDir = 0;
                _bobExtreme = 0;
                if (m.HasKnees)
                {
                    _hasKnee = true;
                    _kneeRawMean = m.KneeDiffY;
                    _kneeCycleDir = 0;
                    _kneeExtreme = 0;
                }
                else _hasKnee = false;

                if (m.HasHips)
                {
                    _hasHipAlt = true;
                    _hipAltRawMean = m.HipTiltY - m.ShoulderTilt;
                }
                else _hasHipAlt = false;

                if (m.HasElbows)
                {
                    _hasArm = true;
                    _armRawMean = m.ElbowDiffY;
                    _armCycleDir = 0;
                    _armExtreme = 0;
                }
                else _hasArm = false;

                _prevRawRoll = rawRoll;
                return;
            }

            // Only suppress extreme sudden torso whip (> 0.16 per frame), NEVER suppress natural jogging weight-shift sway
            if (Math.Abs(rawRoll - _prevRawRoll) > 0.16)
                _suppressStepUntilMs = tMs + 100.0;
            _prevRawRoll = rawRoll;

            if (!_hasBob)
            {
                _hasBob = true;
                _bobRawMean = yRaw;
                _bobFast = 0;
                _prevBobFast = 0;
                _bobExtreme = 0;
            }
            // High-pass in RAW Y first, THEN divide small bounce delta by _wRef (avoids shoulder-foreshortening artifacts)
            _bobRawMean += (yRaw - _bobRawMean) * (1 - Math.Exp(-dtS / 0.32));
            double hpY = (yRaw - _bobRawMean) / _wRef;
            _prevBobFast = _bobFast;
            _bobFast += (hpY - _bobFast) * (1 - Math.Exp(-dtS / 0.035));
            double bobRate = (_bobFast - _prevBobFast) / Math.Max(0.015, dtS);

            bool suppressed = tMs < _suppressStepUntilMs;
            double bobSwing = Math.Max(0.012, 2.0 * cfg.jogStepDeadband);
            if (suppressed)
            {
                _bobExtreme = _bobFast;
            }
            else if (_bobCycleDir == 0)
            {
                if (Math.Abs(_bobFast) >= cfg.jogStepDeadband)
                {
                    _bobCycleDir = _bobFast >= 0 ? 1 : -1;
                    _bobExtreme = _bobFast;
                }
            }
            else if (_bobCycleDir > 0)
            {
                if (_bobFast > _bobExtreme) _bobExtreme = _bobFast;
                else if (_bobExtreme - _bobFast >= bobSwing)
                {
                    _bobCycleDir = -1;
                    _bobExtreme = _bobFast;
                }
            }
            else
            {
                if (_bobFast < _bobExtreme) _bobExtreme = _bobFast;
                else if (_bobFast - _bobExtreme >= bobSwing)
                {
                    _bobCycleDir = 1;
                    _bobExtreme = _bobFast;
                    RecordStep(tMs);
                }
            }

            double legInst = 0.0;
            if (m.HasKnees)
            {
                if (!_hasKnee)
                {
                    _hasKnee = true;
                    _kneeRawMean = m.KneeDiffY;
                    _kneeExtreme = 0;
                }
                _kneeRawMean += (m.KneeDiffY - _kneeRawMean) * (1 - Math.Exp(-dtS / 0.45));
                double hpKnee = (m.KneeDiffY - _kneeRawMean) / _wRef;
                legInst = Math.Min(1.6, Math.Abs(hpKnee) / 0.045);

                double kneeSwing = Math.Max(0.03, 2.0 * cfg.jogKneeDeadband);
                if (!suppressed)
                {
                    if (_kneeCycleDir == 0)
                    {
                        if (Math.Abs(hpKnee) >= cfg.jogKneeDeadband)
                        {
                            _kneeCycleDir = hpKnee >= 0 ? 1 : -1;
                            _kneeExtreme = hpKnee;
                        }
                    }
                    else if (_kneeCycleDir > 0)
                    {
                        if (hpKnee > _kneeExtreme) _kneeExtreme = hpKnee;
                        else if (_kneeExtreme - hpKnee >= kneeSwing)
                        {
                            _kneeCycleDir = -1;
                            _kneeExtreme = hpKnee;
                            RecordStep(tMs);
                        }
                    }
                    else
                    {
                        if (hpKnee < _kneeExtreme) _kneeExtreme = hpKnee;
                        else if (hpKnee - _kneeExtreme >= kneeSwing)
                        {
                            _kneeCycleDir = 1;
                            _kneeExtreme = hpKnee;
                            RecordStep(tMs);
                        }
                    }
                }
            }
            else
            {
                _hasKnee = false;
                if (m.HasHips)
                {
                    double rawHip = m.HipTiltY - m.ShoulderTilt;
                    if (!_hasHipAlt) { _hasHipAlt = true; _hipAltRawMean = rawHip; }
                    _hipAltRawMean += (rawHip - _hipAltRawMean) * (1 - Math.Exp(-dtS / 0.40));
                    double hpHip = (rawHip - _hipAltRawMean) / _wRef;
                    legInst = Math.Min(1.3, Math.Abs(hpHip) / 0.030);
                }
                else
                {
                    _hasHipAlt = false;
                }
            }

            double armInst = 0.0;
            if (m.HasElbows)
            {
                if (!_hasArm)
                {
                    _hasArm = true;
                    _armRawMean = m.ElbowDiffY;
                    _armExtreme = 0;
                }
                _armRawMean += (m.ElbowDiffY - _armRawMean) * (1 - Math.Exp(-dtS / 0.45));
                double hpArm = (m.ElbowDiffY - _armRawMean) / _wRef;
                armInst = Math.Min(1.4, Math.Abs(hpArm) / 0.065);

                double armSwing = Math.Max(0.035, 2.0 * cfg.jogArmDeadband);
                if (!suppressed)
                {
                    if (_armCycleDir == 0)
                    {
                        if (Math.Abs(hpArm) >= cfg.jogArmDeadband)
                        {
                            _armCycleDir = hpArm >= 0 ? 1 : -1;
                            _armExtreme = hpArm;
                        }
                    }
                    else if (_armCycleDir > 0)
                    {
                        if (hpArm > _armExtreme) _armExtreme = hpArm;
                        else if (_armExtreme - hpArm >= armSwing)
                        {
                            _armCycleDir = -1;
                            _armExtreme = hpArm;
                            RecordStep(tMs);
                        }
                    }
                    else
                    {
                        if (hpArm < _armExtreme) _armExtreme = hpArm;
                        else if (hpArm - _armExtreme >= armSwing)
                        {
                            _armCycleDir = 1;
                            _armExtreme = hpArm;
                            RecordStep(tMs);
                        }
                    }
                }
            }
            else
            {
                _hasArm = false;
            }

            // Combine vertical bounce + velocity + leg/hip/arm oscillation energy (with stationary noise gate)
            double bobInst = suppressed ? 0.0 : Math.Min(1.8, Math.Abs(_bobFast) / 0.018 + Math.Abs(bobRate) / 0.22);
            double limbInst = suppressed ? 0.0 : Math.Max(legInst, armInst * 0.85);
            double instEnergy = Math.Max(bobInst, Math.Max(limbInst, 0.55 * bobInst + 0.55 * limbInst));
            double eTau = instEnergy > _motionEnergy ? 0.11 : 0.24;
            _motionEnergy += (instEnergy - _motionEnergy) * (1.0 - Math.Exp(-dtS / eTau));
            if (_motionEnergy >= 0.36) _sustainedEnergyMs = Math.Min(2000.0, _sustainedEnergyMs + dtS * 1000.0);
            else _sustainedEnergyMs = 0.0;

            // Compute active cadence from recent steps within rolling 1.35s window
            int valid = 0;
            double firstMs = double.PositiveInfinity, lastMs = double.NegativeInfinity;
            for (int i = 0; i < _stepCount; i++)
            {
                double st = _stepTimes[i];
                if (tMs - st <= 1350.0)
                {
                    valid++;
                    if (st < firstMs) firstMs = st;
                    if (st > lastMs) lastMs = st;
                }
            }

            double targetRun = 0.0;
            double cadence = 0.0;
            double sinceLast = valid > 0 ? tMs - lastMs : double.PositiveInfinity;
            if (valid >= 2 && sinceLast <= cfg.jogTimeoutMs)
            {
                double spanS = Math.Max(0.32, (lastMs - firstMs) / 1000.0);
                double rateSpan = (valid - 1) / spanS;
                double rateNow = valid / Math.Max(0.45, (tMs - firstMs) / 1000.0);
                cadence = Math.Min(rateSpan, rateNow);
                if (cadence >= cfg.jogMinHz * 0.65)
                {
                    double frac = MathUtil.Clamp((cadence - cfg.jogMinHz) / Math.Max(0.35, cfg.jogSprintHz - cfg.jogMinHz), 0.0, 1.0);
                    double cadenceDrive = 0.38 + 0.62 * frac;
                    double energyBonus = MathUtil.Clamp((_motionEnergy - 0.55) / 0.85, 0.0, 1.0);
                    targetRun = Math.Max(cadenceDrive, 0.36 + 0.55 * energyBonus);
                }
            }
            else if (valid >= 1 && sinceLast <= cfg.jogTimeoutMs && _motionEnergy >= 0.40)
            {
                // Immediate bridge on very first step or single missed frame
                targetRun = MathUtil.Clamp(0.34 + 0.30 * (_motionEnergy - 0.40), 0.32, 0.65);
            }
            else if (_sustainedEnergyMs >= 200.0 && _motionEnergy >= 0.50)
            {
                // Fallback continuous motion drive for low-FPS cameras
                targetRun = MathUtil.Clamp(0.35 + 0.45 * ((_motionEnergy - 0.50) / 0.75), 0.32, 0.78);
            }

            s.StepCadence = cadence;
            double tau = targetRun > s.RunIntensity ? 0.11 : (sinceLast > cfg.jogTimeoutMs ? 0.13 : 0.22);
            double alpha = 1.0 - Math.Exp(-dtS / tau);
            s.RunIntensity = s.RunIntensity + (targetRun - s.RunIntensity) * alpha;
            if (targetRun <= 0.0 && s.RunIntensity < 0.04) s.RunIntensity = 0.0;
        }

        private void RecordStep(double tMs)
        {
            // Refractory guard prevents bob + knee + elbow from double-counting the same footstep
            if (tMs - _lastAnyStepMs < 145.0)
                return;
            _lastAnyStepMs = tMs;
            _stepTimes[_stepWriteIdx] = tMs;
            _stepWriteIdx = (_stepWriteIdx + 1) % _stepTimes.Length;
            if (_stepCount < _stepTimes.Length) _stepCount++;
        }
    }
}
