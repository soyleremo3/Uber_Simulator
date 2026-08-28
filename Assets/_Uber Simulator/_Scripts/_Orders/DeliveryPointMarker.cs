using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Active-target marker for a pickup/delivery point: a single flat ground ring
    /// that gently pulses (scales up and down). Lives under
    /// <see cref="InteractionPoint.markerVisual"/> and is switched on only while
    /// this point is the current order target. Pure cosmetic.
    /// </summary>
    public class DeliveryPointMarker : MonoBehaviour
    {
        [SerializeField] private Transform ring;
        [SerializeField] private float pulseSpeed = 2.2f;
        [SerializeField] private float minScale = 0.8f;
        [SerializeField] private float maxScale = 1.18f;

        private Vector3 ringBaseScale;

        private void Awake()
        {
            if (ring != null)
            {
                ringBaseScale = ring.localScale;
            }
        }

        private void LateUpdate()
        {
            if (ring == null)
            {
                return;
            }

            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float s = Mathf.Lerp(minScale, maxScale, t);
            ring.localScale = new Vector3(ringBaseScale.x * s, ringBaseScale.y, ringBaseScale.z * s);
        }
    }
}
