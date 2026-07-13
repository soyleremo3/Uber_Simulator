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
    /// all times in the Hierarchy - we never call SetActive(false) on them. We use
    /// Prioritize() to choose which one is live (see notes below).
    ///
    /// ADDITIONAL FIX: While the third-person camera is not the active mode, we
    /// disable its SmoothMouseLook script. Without this, mouse movement keeps
    /// changing the third-person camera's orbit angles in the background even
    /// while it is not being viewed. This creates an unstable "moving target" for
    /// Cinemachine's blend system, which is a known trigger for snap/jump bugs
    /// when switching cameras. Freezing the orbit angles the instant we leave
    /// third-person mode keeps that camera in a stable, predictable state for
    /// blending, both when leaving it and when returning to it.
    /// </summary>
    public class CameraModeController : MonoBehaviour
    {
        [Header("Camera References")]
        public CinemachineCamera thirdPersonCamera;
        public CinemachineCamera firstPersonCamera;

        [Header("Third-Person Mouse Look")]
        [Tooltip("The SmoothMouseLook component on the third-person camera. Automatically disabled while not in third-person mode.")]
        public SmoothMouseLook thirdPersonMouseLook;

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
            bool isThirdPerson = CurrentMode == CameraMode.ThirdPerson;

            if (isThirdPerson)
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

            // Freeze/unfreeze mouse-driven orbit changes based on which mode is active.
            if (thirdPersonMouseLook != null)
            {
                thirdPersonMouseLook.enabled = isThirdPerson;
            }
        }
    }
}