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

        private CinemachineOrbitalFollow orbitalFollow;

        private void Awake()
        {
            orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
        }

        private void Update()
        {
            // GetAxisRaw already represents actual mouse movement since the last
            // frame - it does NOT need to be multiplied by Time.deltaTime (doing
            // so would make the camera feel slower at low framerates, which is
            // the wrong direction).
            float mouseX = Input.GetAxisRaw("Mouse X");
            float mouseY = Input.GetAxisRaw("Mouse Y");

            float horizontalDelta = mouseX * panSensitivity;
            float verticalDelta = -mouseY * tiltSensitivity;

            orbitalFollow.HorizontalAxis.Value += horizontalDelta;
            orbitalFollow.VerticalAxis.Value = Mathf.Clamp(
                orbitalFollow.VerticalAxis.Value + verticalDelta,
                minVerticalAngle,
                maxVerticalAngle
            );
        }
    }
}