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
    /// camera when the player presses Tab. Cinemachine Brain automatically blends
    /// between them based on which CinemachineCamera GameObject is active
    /// (SetActive is used instead of Priority values so this works reliably
    /// across Cinemachine versions).
    /// </summary>
    public class CameraModeController : MonoBehaviour
    {
        [Header("Camera References")]
        [Tooltip("The CinemachineCamera GameObject used for third-person chase view.")]
        public GameObject thirdPersonCameraObject;

        [Tooltip("The CinemachineCamera GameObject used for first-person cockpit view.")]
        public GameObject firstPersonCameraObject;

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

            if (thirdPersonCameraObject != null)
            {
                thirdPersonCameraObject.SetActive(isThirdPerson);
            }

            if (firstPersonCameraObject != null)
            {
                firstPersonCameraObject.SetActive(!isThirdPerson);
            }
        }
    }
}