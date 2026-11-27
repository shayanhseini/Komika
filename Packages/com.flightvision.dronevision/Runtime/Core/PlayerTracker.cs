using System.Collections.Generic;

namespace DroneVision.Core
{
    /// <summary>
    /// OperatorTracker (who is the player, in a crowd) + DroneTracker (their wings,
    /// tilt and lock). 1:1 port of the PC app's mashk/player.py.
    /// </summary>
    public sealed class PlayerTracker
    {
        public readonly DroneTracker Drone;
        public readonly OperatorTracker Operator;
        public DroneConfig Config { get { return Drone.Config; } }
        public DroneState State { get { return Drone.State; } }

        public PlayerTracker(DroneConfig config = null)
        {
            Drone = new DroneTracker(config);
            Operator = new OperatorTracker(Drone.Config); // one shared config object
        }

        public void ApplyConfig() { Drone.ApplyConfig(); }

        /// <summary>New player: forget identity, tilt and lock.</summary>
        public void Recalibrate()
        {
            Operator.Reset();
            Drone.Recalibrate();
        }

        public DroneState Update(IList<PoseFrame> poses, IList<ColorSig> sigs, double aspect, double tMs)
        {
            OperatorResult r = Operator.Update(poses, sigs, aspect, tMs);
            // the same player came back: their lock at once; a different person: own lock
            if (r.Acquired)
            {
                if (r.Returned) Drone.Reclaim();
                else Drone.Recalibrate();
            }
            DroneState s = Drone.Update(r.Index >= 0 ? poses[r.Index] : null, tMs);
            s.Operator = r.Index;
            if (r.Index < 0 && r.Hint != OperatorHint.None && !s.Tracking)
                s.Hint = r.Hint == OperatorHint.MoveToCenter ? DroneHint.MoveToCenter : DroneHint.ComeCloser;
            return s;
        }
    }
}
