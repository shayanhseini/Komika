using System;
using System.Collections.Generic;

namespace DroneVision.Core
{
    public enum OperatorHint { None = 0, ComeCloser = 1, MoveToCenter = 2 }

    public struct OperatorResult
    {
        /// <summary>Index of the player among the poses, or -1.</summary>
        public int Index;
        public OperatorHint Hint;
        /// <summary>A player was (re)acquired this frame.</summary>
        public bool Acquired;
        /// <summary>The (re)acquired player is the remembered previous player (keep their lock).</summary>
        public bool Returned;
    }

    /// <summary>
    /// Who is the player, and keep it the same person in a crowd.
    /// 1:1 port of web/src/core/operator-tracker.js and of the PC app's mashk/crowd.py (golden: drone_vectors.json, crowd scenarios).
    ///   Acquire: only in the central band (stageZone); with several people there, only
    ///            someone near enough (minOperatorScale). A lone person: any distance.
    ///   Track:   constant-velocity prediction + body size + clothing-colour signature.
    ///   Reject:  too-far jumps, size changes, clearly different clothes (identity swaps).
    ///   Release: after reacquireMs without a match, but REMEMBER the player (clothing colour +
    ///            size) until resetMs: if they come back they are the same player (Returned).
    /// </summary>
    public sealed class OperatorTracker
    {
        private readonly DroneConfig _cfg;
        private readonly List<PoseMeasure> _ms = new List<PoseMeasure>(4);
        private readonly List<bool> _ok = new List<bool>(4);
        private readonly double[] _sig = new double[3];

        public bool Active { get; private set; }
        private double _cx, _cy, _sw, _vx, _vy, _lastT;
        private bool _hasSig;
        private int _sigFrames;
        // the player who was let go, remembered until resetMs
        private bool _hasMem;
        private readonly double[] _memSig = new double[3];
        private double _memSw, _memT;
        private int _memSigFrames;

        public OperatorTracker(DroneConfig cfg)
        {
            _cfg = cfg;
            Reset();
        }

        /// <summary>Forget the player completely (a new visitor).</summary>
        public void Reset()
        {
            Drop();
            _hasMem = false;
        }

        private void Drop()
        {
            Active = false;
            _cx = _cy = _sw = _vx = _vy = _lastT = 0;
            _hasSig = false;
            _sigFrames = 0;
        }

        /// <summary>Distance between two clothing-colour signatures (r,g,b 0..1): chromaticity + a little brightness.</summary>
        public static double ColorDistance(double ar, double ag, double ab, double br, double bg, double bb)
        {
            double sa = ar + ag + ab + 0.001, sb = br + bg + bb + 0.001;
            double dr = ar / sa - br / sb, dg = ag / sa - bg / sb;
            double la = sa / 3 + 0.05, lb = sb / 3 + 0.05;
            return Math.Sqrt(dr * dr + dg * dg) * 2 + Math.Abs(Math.Log(la / lb)) * 0.3;
        }

        private void Take(PoseMeasure m, ColorSig sig, double t, bool fresh)
        {
            if (fresh)
            {
                _vx = 0; _vy = 0;
                _sw = m.ShoulderWidth;
                _hasSig = sig.Valid;
                if (sig.Valid) { _sig[0] = sig.R; _sig[1] = sig.G; _sig[2] = sig.B; }
                _sigFrames = sig.Valid ? 1 : 0;
            }
            else
            {
                double dt = t - _lastT;
                if (dt > 0)
                {
                    _vx = 0.5 * _vx + 0.5 * (m.CenterX - _cx) / dt;
                    _vy = 0.5 * _vy + 0.5 * (m.CenterY - _cy) / dt;
                }
                _sw = 0.6 * _sw + 0.4 * m.ShoulderWidth;
                if (sig.Valid)
                {
                    if (_hasSig)
                    {
                        _sig[0] = 0.9 * _sig[0] + 0.1 * sig.R;
                        _sig[1] = 0.9 * _sig[1] + 0.1 * sig.G;
                        _sig[2] = 0.9 * _sig[2] + 0.1 * sig.B;
                    }
                    else { _sig[0] = sig.R; _sig[1] = sig.G; _sig[2] = sig.B; _hasSig = true; }
                    _sigFrames++;
                }
            }
            _cx = m.CenterX;
            _cy = m.CenterY;
            _lastT = t;
        }

        /// <param name="sigs">clothing signatures, same order as poses (may be null)</param>
        public OperatorResult Update(IList<PoseFrame> poses, IList<ColorSig> sigs, double aspect, double t)
        {
            DroneConfig cfg = _cfg;
            int n = poses.Count;
            _ms.Clear();
            _ok.Clear();
            for (int i = 0; i < n; i++)
            {
                PoseMeasure m;
                bool ok = DroneTracker.Measure(poses[i], cfg, out m);
                _ms.Add(m);
                _ok.Add(ok);
            }

            OperatorResult r = new OperatorResult { Index = -1, Hint = OperatorHint.None, Acquired = false, Returned = false };
            if (!Active)
            {
                if (_hasMem && t - _memT > cfg.resetMs) _hasMem = false;
                // the player who just left comes back: recognised by clothing colour and size
                if (_hasMem)
                {
                    int back = -1;
                    double bestC = double.PositiveInfinity;
                    bool waiting = false;
                    for (int i = 0; i < n; i++)
                    {
                        if (!_ok[i]) continue;
                        PoseMeasure m = _ms[i];
                        if (Math.Abs(m.CenterX - 0.5) > cfg.stageZone / 2) continue;
                        double ratio = m.ShoulderWidth / _memSw;
                        if (ratio < cfg.scaleMin || ratio > cfg.scaleMax) continue;
                        ColorSig sig = SigAt(sigs, i);
                        if (!sig.Valid) { waiting = true; continue; } // colour not measured on this frame
                        double c = ColorDistance(sig.R, sig.G, sig.B, _memSig[0], _memSig[1], _memSig[2]);
                        if (c <= cfg.colorVeto && c < bestC) { bestC = c; back = i; }
                    }
                    if (back >= 0)
                    {
                        _hasMem = false;
                        Active = true;
                        Take(_ms[back], SigAt(sigs, back), t, true);
                        _sig[0] = _memSig[0]; _sig[1] = _memSig[1]; _sig[2] = _memSig[2];
                        _hasSig = true;
                        _sigFrames = _memSigFrames;
                        r.Index = back;
                        r.Acquired = true;
                        r.Returned = true;
                        return r;
                    }
                    // someone of the right size whose colour was not measured on this frame: wait for it
                    if (waiting) return r;
                }
                int inZone = 0;
                for (int i = 0; i < n; i++)
                    if (_ok[i] && Math.Abs(_ms[i].CenterX - 0.5) <= cfg.stageZone / 2) inZone++;
                bool sizeGate = inZone > 1; // the distance rule only separates people in a crowd
                int best = -1;
                double bestScore = double.NegativeInfinity;
                bool far = false, off = false;
                for (int i = 0; i < n; i++)
                {
                    if (!_ok[i]) continue;
                    PoseMeasure m = _ms[i];
                    if (Math.Abs(m.CenterX - 0.5) > cfg.stageZone / 2) { off = true; continue; }
                    if (sizeGate && m.Scale < cfg.minOperatorScale) { far = true; continue; }
                    double score = m.Scale * (1 - Math.Min(1, 1.2 * Math.Abs(m.CenterX - 0.5)));
                    if (score > bestScore) { bestScore = score; best = i; }
                }
                if (best < 0)
                {
                    r.Hint = off ? OperatorHint.MoveToCenter : far ? OperatorHint.ComeCloser : OperatorHint.None;
                    return r;
                }
                _hasMem = false; // a different person takes over
                Active = true;
                Take(_ms[best], SigAt(sigs, best), t, true);
                r.Index = best;
                r.Acquired = true;
                return r;
            }

            double dt = t - _lastT;
            double px = _cx + _vx * dt, py = _cy + _vy * dt;
            double gate = cfg.jumpBase + cfg.jumpPerSec * dt / 1000;
            bool useColor = _hasSig && _sigFrames >= 5;
            int pick = -1;
            double bestCost = double.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                if (!_ok[i]) continue;
                PoseMeasure m = _ms[i];
                double dx = (m.CenterX - px) * aspect, dy = m.CenterY - py;
                double d = Math.Sqrt(dx * dx + dy * dy) / _sw;
                if (d > gate) continue;
                double ratio = m.ShoulderWidth / _sw;
                if (ratio < cfg.scaleMin || ratio > cfg.scaleMax) continue;
                double cost = d + Math.Abs(Math.Log(ratio)) * 2;
                ColorSig sig = SigAt(sigs, i);
                if (useColor && sig.Valid)
                {
                    double c = ColorDistance(sig.R, sig.G, sig.B, _sig[0], _sig[1], _sig[2]);
                    if (c > cfg.colorVeto) continue;
                    cost += c * cfg.colorWeight;
                }
                if (cost < bestCost) { bestCost = cost; pick = i; }
            }
            if (pick < 0)
            {
                if (t - _lastT > cfg.reacquireMs)
                {
                    // let go, but remember who it was (only with a reliable colour signature)
                    bool remember = _hasSig && _sigFrames >= 5;
                    if (remember)
                    {
                        _memSig[0] = _sig[0]; _memSig[1] = _sig[1]; _memSig[2] = _sig[2];
                        _memSw = _sw;
                        _memT = _lastT;
                        _memSigFrames = _sigFrames;
                    }
                    Drop();
                    _hasMem = remember;
                }
                return r;
            }
            Take(_ms[pick], SigAt(sigs, pick), t, false);
            r.Index = pick;
            return r;
        }

        private static ColorSig SigAt(IList<ColorSig> sigs, int i)
        {
            return sigs != null && i < sigs.Count ? sigs[i] : default(ColorSig);
        }
    }

    /// <summary>Mean clothing colour of a person's torso (0..1), from the page.</summary>
    public struct ColorSig
    {
        public bool Valid;
        public double R, G, B;

        public ColorSig(double r, double g, double b)
        {
            Valid = true;
            R = r; G = g; B = b;
        }
    }
}
