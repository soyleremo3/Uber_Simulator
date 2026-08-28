using TMPro;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Active-target marker visual for a pickup/delivery point: a bobbing +
    /// spinning icon shape, a soft pulsing ground ring, and a camera-facing
    /// distance label. Lives under <see cref="InteractionPoint.markerVisual"/>
    /// and is switched on only while this point is the current order target
    /// (OrderManager toggles the GameObject). Pure cosmetic — no gameplay.
    /// </summary>
    public class DeliveryPointMarker : MonoBehaviour
    {
        [SerializeField] private Transform iconShape;
        [SerializeField] private Transform ring;
        [SerializeField] private TextMeshPro distanceLabel;

        [Header("Motion")]
        [SerializeField] private float bobHeight = 0.35f;
        [SerializeField] private float bobSpeed = 2.2f;
        [SerializeField] private float spinSpeed = 55f;
        [SerializeField] private float pulseSpeed = 2.4f;
        [SerializeField] private float pulseAmount = 0.14f;

        private Transform player;
        private Vector3 iconBasePos;
        private Vector3 ringBaseScale;

        private void Awake()
        {
            if (iconShape != null)
            {
                iconBasePos = iconShape.localPosition;
            }

            if (ring != null)
            {
                ringBaseScale = ring.localScale;
            }
        }

        private void OnEnable()
        {
            ResolvePlayer();
        }

        private void ResolvePlayer()
        {
            if (player != null)
            {
                return;
            }

            VehicleController vehicle = FindFirstObjectByType<VehicleController>();
            if (vehicle != null)
            {
                player = vehicle.transform;
            }
        }

        private void LateUpdate()
        {
            float t = Time.time;

            if (iconShape != null)
            {
                iconShape.localPosition = iconBasePos + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobHeight);
                iconShape.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            }

            if (ring != null)
            {
                float pulse = 1f + Mathf.Sin(t * pulseSpeed) * pulseAmount;
                ring.localScale = new Vector3(ringBaseScale.x * pulse, ringBaseScale.y, ringBaseScale.z * pulse);
            }

            if (distanceLabel == null)
            {
                return;
            }

            ResolvePlayer();
            Camera cam = Camera.main;

            if (player != null)
            {
                int metres = Mathf.RoundToInt(Vector3.Distance(player.position, transform.position));
                distanceLabel.text = metres + " m";
            }

            if (cam != null)
            {
                distanceLabel.transform.rotation =
                    Quaternion.LookRotation(distanceLabel.transform.position - cam.transform.position);
            }
        }
    }
}
