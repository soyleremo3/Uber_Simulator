using Unity.Cinemachine;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Converts raw mouse movement into orbit angle changes on Orbital Follow's
    /// Horizontal and Vertical axes.
    ///
    /// IMPORTANT DESIGN NOTE: This script deliberately does NOT smooth the input
    /// itself anymore. Cinemachine's own Position Damping (on the Orbital Follow
    /// component) and the Rotation Composer's damping are responsible for ALL
    /// positional/rotational smoothing. Having two independent smoothing systems
    /// stacked on top of each other (this script's old SmoothDamp + Cinemachine's
    /// damping) caused compounding lag and jitter. Tune the feel by adjusting
    /// sensitivity here AND Position Damping / Rotation Composer Damping in the
    /// Inspector - never add smoothing back into this script.
    /// </summary>
    [RequireComponent(typeof(CinemachineOrbitalFollow))]
    public class SmoothMouseLook : MonoBehaviour
    {
        [Header("Sensitivity")]
        [Tooltip("Higher = camera orbits further per unit of raw mouse movement.")]
        public float panSensitivity = 3f;
        public float tiltSensitivity = 3f;

        [Header("Vertical Limits (Degrees)")]
        [Tooltip("Check the Vertical Axis Range shown on Cinemachine Orbital Follow and match these to it.")]
        public float minVerticalAngle = 5f;
        public float maxVerticalAngle = 60f;

        [Header("Auto Recenter (pro driving-cam behavior)")]
        [Tooltip("After the mouse is idle, drift the camera back behind the car. Requires Orbital Follow binding = Lock To Target With World Up (Setup menu 7 configures it).")]
        public bool autoRecenter = true;
        [Tooltip("Seconds of mouse inactivity before recentering starts.")]
        public float recenterDelay = 1.2f;
        [Tooltip("How fast the camera drifts back (higher = faster).")]
        public float recenterLerpSpeed = 2.5f;
        [Tooltip("Vertical angle the camera settles at (matches the default driving angle).")]
        public float defaultVerticalAngle = 17.5f;
        [Tooltip("Mouse movement below this is treated as idle (ignores sensor noise).")]
        public float idleThreshold = 0.01f;

        private CinemachineOrbitalFollow orbitalFollow;
        private float idleTimer;

        private void Awake()
        {
            orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
        }

        private void OnEnable()
        {
            // Re-enabled after a camera-mode switch: start the idle clock fresh.
            idleTimer = 0f;
        }

        private void Update()
        {
            // GetAxisRaw already represents actual mouse movement since the last
            // frame - it does NOT need to be multiplied by Time.deltaTime (doing
            // so would make the camera feel slower at low framerates, which is
            // the wrong direction).
            float mouseX = Input.GetAxisRaw("Mouse X");
            float mouseY = Input.GetAxisRaw("Mouse Y");

            bool mouseActive = Mathf.Abs(mouseX) > idleThreshold || Mathf.Abs(mouseY) > idleThreshold;

            if (mouseActive)
            {
                idleTimer = 0f;

                float horizontalDelta = mouseX * panSensitivity;
                float verticalDelta = -mouseY * tiltSensitivity;

                orbitalFollow.HorizontalAxis.Value += horizontalDelta;
                orbitalFollow.VerticalAxis.Value = Mathf.Clamp(
                    orbitalFollow.VerticalAxis.Value + verticalDelta,
                    minVerticalAngle,
                    maxVerticalAngle
                );
                return;
            }

            if (!autoRecenter)
            {
                return;
            }

            // Recenter: with LockToTargetWithWorldUp binding, horizontal value 0 means
            // "directly behind the (yaw-smoothed) camera rig", so drifting the axes back
            // gives the classic 'look around, release, camera settles behind the car'
            // feel. Implemented manually (not via Cinemachine's recentering) for full
            // control and to avoid fighting with this script's direct axis writes.
            idleTimer += Time.deltaTime;
            if (idleTimer < recenterDelay)
            {
                return;
            }

            float t = 1f - Mathf.Exp(-recenterLerpSpeed * Time.deltaTime); // Framerate-independent lerp
            orbitalFollow.HorizontalAxis.Value = Mathf.LerpAngle(orbitalFollow.HorizontalAxis.Value, 0f, t);
            orbitalFollow.VerticalAxis.Value = Mathf.Lerp(
                orbitalFollow.VerticalAxis.Value,
                Mathf.Clamp(defaultVerticalAngle, minVerticalAngle, maxVerticalAngle),
                t);
        }
    }
}