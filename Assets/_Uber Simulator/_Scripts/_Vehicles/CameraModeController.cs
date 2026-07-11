using Unity.Cinemachine;
using UnityEngine;

namespace DeliverySim
{
    public enum CameraMode
    {
        ThirdPerson,
        FirstPerson
    }

    /// <summary>
    /// Toggles between the third-person chase camera and the first-person cockpit
    /// camera when the player presses Tab.
    ///
    /// IMPORTANT: Both CinemachineCamera GameObjects must stay ACTIVE (enabled) at
    /// all times in the Hierarchy. We never call SetActive(false) on them.
    ///
    /// Per Unity's official Cinemachine documentation, a disabled CinemachineCamera
    /// enters the "Disabled" state and stops tracking its target entirely. When
    /// re-enabled, its position is stale, so Cinemachine "warps" (instantly snaps)
    /// to it before blending - this caused the "smooth then suddenly locks" bug.
    ///
    /// Instead, we call Prioritize() on the camera we want to become live. Since
    /// both cameras stay active, the non-live one remains in "Standby" state,
    /// continuously tracking its target in the background. This produces a fully
    /// smooth blend with no snapping.
    /// </summary>
    public class CameraModeController : MonoBehaviour
    {
        [Header("Camera References")]
        public CinemachineCamera thirdPersonCamera;
        public CinemachineCamera firstPersonCamera;

        public CameraMode CurrentMode { get; private set; } = CameraMode.ThirdPerson;

        private void Start()
        {
            ApplyMode();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                CurrentMode = CurrentMode == CameraMode.ThirdPerson
                    ? CameraMode.FirstPerson
                    : CameraMode.ThirdPerson;

                ApplyMode();
            }
        }

        private void ApplyMode()
        {
            if (CurrentMode == CameraMode.ThirdPerson)
            {
                if (thirdPersonCamera != null)
                {
                    thirdPersonCamera.Prioritize();
                }
            }
            else
            {
                if (firstPersonCamera != null)
                {
                    firstPersonCamera.Prioritize();
                }
            }
        }
    }
}