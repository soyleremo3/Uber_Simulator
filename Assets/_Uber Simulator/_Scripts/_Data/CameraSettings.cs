using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// All tunable parameters for VehicleCameraController, so camera feel can be
    /// adjusted in the Editor without touching code. One asset per camera style.
    /// </summary>
    [CreateAssetMenu(fileName = "CameraSettings", menuName = "DeliverySim/Camera Settings")]
    public class CameraSettings : ScriptableObject
    {
        [Header("Framing")]
        [Tooltip("Camera offset from the target, in the target's local space (x=side, y=up, z=back; z should be negative).")]
        public Vector3 followOffset = new Vector3(0f, 3f, -7f);
        [Tooltip("Extra world-space height added to the look-at point.")]
        public float lookAtHeight = 1.5f;

        [Header("Smoothing (bug #3: separate position/rotation smoothing)")]
        [Tooltip("SmoothDamp time for position. Lower = snappier.")]
        public float positionSmoothTime = 0.18f;
        [Tooltip("Slerp speed for rotation. Higher = snappier.")]
        public float rotationSmoothSpeed = 8f;

        [Header("Speed FOV + look-ahead (bug #4)")]
        public bool speedEffectsEnabled = true;
        public float baseFov = 60f;
        public float maxFov = 72f;
        [Tooltip("Speed (km/h) at which FOV reaches maxFov.")]
        public float speedForMaxFov = 120f;
        [Tooltip("How far (meters) the look-at point shifts toward the movement direction at high speed.")]
        public float lookAheadDistance = 3f;
        [Tooltip("How fast the look-ahead point adapts.")]
        public float lookAheadSmoothSpeed = 4f;

        [Header("Collision (bug #2)")]
        public bool collisionEnabled = true;
        [Tooltip("Radius of the spherecast used to detect obstacles between target and camera.")]
        public float collisionRadius = 0.3f;
        [Tooltip("How far in front of the hit surface the camera is placed.")]
        public float collisionPadding = 0.15f;
        [Tooltip("Minimum distance the camera can be pulled toward the target.")]
        public float minDistance = 1.5f;
        [Tooltip("Layers treated as camera obstacles. Exclude the vehicle's own layer!")]
        public LayerMask collisionMask = ~0;

        [Header("Reverse / parking dead zone (bug #6)")]
        [Tooltip("Below this speed (km/h) the camera stops chasing velocity direction and settles behind the car — prevents spinning while parking/reversing.")]
        public float deadZoneSpeedKph = 8f;

        private void OnValidate()
        {
            if (positionSmoothTime < 0.01f) positionSmoothTime = 0.01f;
            if (speedForMaxFov < 1f) speedForMaxFov = 1f;
            if (minDistance < 0.5f) minDistance = 0.5f;
            if (maxFov < baseFov) maxFov = baseFov;
        }
    }
}
