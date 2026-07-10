using Unity.Cinemachine;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Reads raw mouse input, smooths it using SmoothDamp (so a sudden fast mouse
    /// flick does not cause an instant large camera jump), and feeds the smoothed
    /// value into the CinemachinePanTilt component.
    ///
    /// This script is the ONLY thing driving PanTilt's rotation. Do not also add
    /// a Cinemachine Input Axis Controller to the same camera — having two
    /// systems drive the same values at the same time causes exactly the
    /// "fighting for control" feeling this script is designed to fix.
    /// </summary>
    [RequireComponent(typeof(CinemachinePanTilt))]
    public class SmoothMouseLook : MonoBehaviour
    {
        [Header("Sensitivity")]
        [Tooltip("Higher = camera turns further per mouse movement unit.")]
        public float panSensitivity = 3f;
        public float tiltSensitivity = 3f;

        [Header("Smoothing")]
        [Tooltip("How long (in seconds) it takes the smoothed input to catch up to the raw mouse input. Lower = snappier, higher = smoother/heavier.")]
        public float smoothTime = 0.08f;

        [Header("Tilt Limits (Degrees)")]
        [Tooltip("How far down the camera can look.")]
        public float minTiltAngle = -30f;
        [Tooltip("How far up the camera can look.")]
        public float maxTiltAngle = 40f;

        private CinemachinePanTilt panTilt;
        private Vector2 smoothedInput;
        private Vector2 inputVelocity;

        private void Awake()
        {
            panTilt = GetComponent<CinemachinePanTilt>();
        }

        private void Update()
        {
            Vector2 rawMouseInput = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));

            // SmoothDamp filters out sudden spikes in the raw mouse delta,
            // producing a gradual, controlled turn instead of an instant snap.
            smoothedInput = Vector2.SmoothDamp(smoothedInput, rawMouseInput, ref inputVelocity, smoothTime);

            float panDelta = smoothedInput.x * panSensitivity;
            float tiltDelta = -smoothedInput.y * tiltSensitivity;

            panTilt.PanAxis.Value += panDelta;
            panTilt.TiltAxis.Value = Mathf.Clamp(panTilt.TiltAxis.Value + tiltDelta, minTiltAngle, maxTiltAngle);
        }
    }
}