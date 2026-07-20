using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Flip recovery: pressing the reset key uprights the vehicle in place
    /// (keeps its yaw heading), lifts it slightly and zeroes all velocities and
    /// wheel spin. Lifesaver after rollovers — without it a flipped car ends the run.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class VehicleReset : MonoBehaviour
    {
        [SerializeField] private KeyCode resetKey = KeyCode.R;
        [Tooltip("How high above the current position the vehicle is dropped from.")]
        [SerializeField] private float liftHeight = 1.5f;

        private VehicleController controller;
        private Rigidbody rb;

        private void Awake()
        {
            controller = GetComponent<VehicleController>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(resetKey))
            {
                ResetUpright();
            }
        }

        public void ResetUpright()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
                if (rb == null)
                {
                    return;
                }
            }

            float yaw = transform.eulerAngles.y;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = transform.position + Vector3.up * liftHeight;
            rb.rotation = Quaternion.Euler(0f, yaw, 0f);

            if (controller != null)
            {
                if (controller.engine != null)
                {
                    controller.engine.ResetState();
                }

                if (controller.wheels != null)
                {
                    foreach (VehicleWheel wheel in controller.wheels)
                    {
                        wheel.angularVelocity = 0f;
                        wheel.slip = 0f;
                        wheel.input = Vector2.zero;
                    }
                }
            }

            NotificationService.Raise("Araç düzeltildi.");
        }
    }
}
