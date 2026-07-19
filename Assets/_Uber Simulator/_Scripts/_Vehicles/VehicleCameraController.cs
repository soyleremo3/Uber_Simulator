using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Code-based 3rd person follow camera — the full-control fallback to the
    /// Cinemachine setup. Designed against the classic follow-camera bugs:
    ///
    ///  1. JITTER      -> all camera movement runs in LateUpdate (after physics +
    ///                    animation), and the followed Rigidbody should use
    ///                    Interpolate (see MANUAL_STEPS). SmoothDamp decouples the
    ///                    camera from FixedUpdate stepping.
    ///  2. WALL CLIP   -> spherecast from target to desired position pulls the
    ///                    camera in front of obstacles.
    ///  3. WHIPLASH    -> separate, tunable smoothing for position (SmoothDamp)
    ///                    and rotation (Slerp) in CameraSettings.
    ///  4. HIGH SPEED  -> optional speed-based FOV boost + look-ahead toward the
    ///                    movement direction.
    ///  5. RESPAWN     -> ResetToTarget() snaps the camera instantly (call after
    ///                    teleports/scene loads to avoid a long fly-in).
    ///  6. REVERSE SPIN-> below a dead-zone speed the camera ignores velocity
    ///                    direction, so parking/reversing doesn't whip it around.
    ///
    /// Attach to the Camera object. Disable this component when the Cinemachine
    /// camera is active (only one system should drive the camera).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class VehicleCameraController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Vehicle root to follow. Auto-found via VehicleController if empty.")]
        [SerializeField] private Transform target;
        [Tooltip("All tunables live in this asset so feel can be adjusted without code.")]
        [SerializeField] private CameraSettings settings;

        private Camera cam;
        private Rigidbody targetRb;
        private Vector3 positionVelocity;      // SmoothDamp state
        private Vector3 currentLookAhead;      // Smoothed look-ahead offset

        private void Awake()
        {
            cam = GetComponent<Camera>();

            if (settings == null)
            {
                // Defensive: run with defaults instead of null-ref spam.
                settings = ScriptableObject.CreateInstance<CameraSettings>();
                Debug.LogWarning("[VehicleCameraController] CameraSettings atanmadı, geçici varsayılanlar kullanılıyor.");
            }
        }

        private void Start()
        {
            if (target == null)
            {
                VehicleController vehicle = FindFirstObjectByType<VehicleController>();
                if (vehicle != null)
                {
                    target = vehicle.transform;
                }
            }

            CacheTargetRigidbody();
            ResetToTarget();
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            CacheTargetRigidbody();
            ResetToTarget();
        }

        /// <summary>
        /// Bug #5: snaps position/rotation/state directly behind the target.
        /// Call after respawn, teleport or scene transition.
        /// </summary>
        public void ResetToTarget()
        {
            if (target == null)
            {
                return;
            }

            positionVelocity = Vector3.zero;
            currentLookAhead = Vector3.zero;
            transform.position = DesiredPosition();
            transform.rotation = Quaternion.LookRotation(LookPoint() - transform.position, Vector3.up);
            if (cam != null)
            {
                cam.fieldOfView = settings.baseFov;
            }
        }

        // Bug #1: LateUpdate — camera moves after physics interpolation and animation.
        private void LateUpdate()
        {
            if (target == null || cam == null)
            {
                return;
            }

            float speedKph = targetRb != null ? targetRb.linearVelocity.magnitude * 3.6f : 0f;

            // ----- Position (bug #3: SmoothDamp with its own tunable) -----
            Vector3 desired = DesiredPosition();

            // Bug #2: pull the camera in front of obstacles between target and camera.
            if (settings.collisionEnabled)
            {
                desired = ResolveCollision(desired);
            }

            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref positionVelocity, settings.positionSmoothTime);

            // ----- Look-ahead (bug #4 + #6) -----
            Vector3 lookTarget = LookPoint();
            if (settings.speedEffectsEnabled && targetRb != null && speedKph > settings.deadZoneSpeedKph)
            {
                // Only chase velocity direction above the dead zone (bug #6):
                // while parking/reversing the camera stays calmly behind the car.
                Vector3 flatVelocity = targetRb.linearVelocity;
                flatVelocity.y = 0f;

                float speedT = Mathf.Clamp01(speedKph / settings.speedForMaxFov);
                Vector3 aheadOffset = flatVelocity.normalized * (settings.lookAheadDistance * speedT);
                currentLookAhead = Vector3.Lerp(
                    currentLookAhead, aheadOffset, Time.deltaTime * settings.lookAheadSmoothSpeed);
            }
            else
            {
                currentLookAhead = Vector3.Lerp(
                    currentLookAhead, Vector3.zero, Time.deltaTime * settings.lookAheadSmoothSpeed);
            }

            lookTarget += currentLookAhead;

            // ----- Rotation (bug #3: Slerp with its own tunable) -----
            Vector3 lookDirection = lookTarget - transform.position;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, desiredRotation, Time.deltaTime * settings.rotationSmoothSpeed);
            }

            // ----- Speed FOV (bug #4) -----
            if (settings.speedEffectsEnabled)
            {
                float fovT = Mathf.Clamp01(speedKph / settings.speedForMaxFov);
                float targetFov = Mathf.Lerp(settings.baseFov, settings.maxFov, fovT);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.deltaTime * 4f);
            }
        }

        private Vector3 DesiredPosition()
        {
            // Offset in the target's YAW space only — pitch/roll from suspension
            // must not tilt the camera (same idea as VehicleCameraRig).
            Vector3 flatForward = target.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f)
            {
                flatForward = Vector3.forward;
            }

            Quaternion yawRotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
            return target.position + yawRotation * settings.followOffset;
        }

        private Vector3 LookPoint()
        {
            return target.position + Vector3.up * settings.lookAtHeight;
        }

        private Vector3 ResolveCollision(Vector3 desiredPosition)
        {
            Vector3 origin = LookPoint();
            Vector3 toCamera = desiredPosition - origin;
            float distance = toCamera.magnitude;

            if (distance < 0.001f)
            {
                return desiredPosition;
            }

            if (Physics.SphereCast(origin, settings.collisionRadius, toCamera.normalized,
                    out RaycastHit hit, distance, settings.collisionMask, QueryTriggerInteraction.Ignore))
            {
                float safeDistance = Mathf.Max(settings.minDistance, hit.distance - settings.collisionPadding);
                return origin + toCamera.normalized * safeDistance;
            }

            return desiredPosition;
        }

        private void CacheTargetRigidbody()
        {
            targetRb = target != null ? target.GetComponentInParent<Rigidbody>() : null;
        }
    }
}
