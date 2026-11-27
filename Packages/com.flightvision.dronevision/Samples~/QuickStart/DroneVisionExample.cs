using DroneVision;
using UnityEngine;

namespace DroneVision.Samples
{
    /// <summary>
    /// Minimal drone controller. Two ways to read the body, pick one:
    ///   useKeys = true : the same as the PC app (D / A / W / S held) through DroneVisionInput.GetKey,
    ///                    so existing keyboard code needs only "Input." → "DroneVisionInput.".
    ///   useKeys = false: analog Roll / Pitch (-1..1), smoother.
    /// The flight pauses while no player is locked.
    /// </summary>
    public class DroneVisionExample : MonoBehaviour
    {
        public bool useKeys = true;
        public float turnSpeed = 90f;     // degrees per second
        public float climbSpeed = 3f;     // units per second
        public float forwardSpeed = 5f;

        private bool _flying;
        private float _yaw;

        private void Start()
        {
            DroneVisionInput v = DroneVisionInput.Instance;
            if (v == null) { Debug.LogError("Add DroneVisionInput to the first scene"); return; }
            v.OnLockChanged += OnLock;
            _flying = v.IsLocked;
        }

        private void OnDestroy()
        {
            if (DroneVisionInput.Instance != null) DroneVisionInput.Instance.OnLockChanged -= OnLock;
        }

        private void OnLock(bool locked)
        {
            _flying = locked; // green frame: fly; player lost: pause
        }

        private void Update()
        {
            if (!_flying) return;
            float turn, climb;
            if (useKeys)
            {
                turn = (DroneVisionInput.GetKey(KeyCode.D) ? 1f : 0f) - (DroneVisionInput.GetKey(KeyCode.A) ? 1f : 0f);
                climb = (DroneVisionInput.GetKey(KeyCode.W) ? 1f : 0f) - (DroneVisionInput.GetKey(KeyCode.S) ? 1f : 0f);
            }
            else
            {
                DroneVisionInput v = DroneVisionInput.Instance;
                turn = v.WingsOpen ? v.Roll : 0f;
                climb = v.WingsOpen ? v.Pitch : 0f;
            }
            float dt = Time.deltaTime;
            _yaw += turn * turnSpeed * dt;
            transform.localRotation = Quaternion.Euler(0f, _yaw, -turn * 25f);
            Vector3 p = transform.position;
            float rad = _yaw * Mathf.Deg2Rad;
            p.x += Mathf.Sin(rad) * forwardSpeed * dt;
            p.z += Mathf.Cos(rad) * forwardSpeed * dt;
            p.y += climb * climbSpeed * dt;
            transform.position = p;
        }
    }
}
